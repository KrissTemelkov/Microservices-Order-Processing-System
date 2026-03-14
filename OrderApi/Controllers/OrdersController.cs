using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderApi.Data;
using OrderApi.DTOs;
using OrderApi.Models;
using OrderApi.Services;
using SharedModels;

namespace OrderApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]

    public class OrdersController : ControllerBase
    {
        private readonly OrderDbContext _context;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(
            OrderDbContext context,
            IRabbitMQService rabbitMQService,
            ILogger<OrdersController> logger)
        {
         _context = context;
         _rabbitMQService = rabbitMQService;
         _logger = logger;   
        }

        // GET: /orders
        [HttpGet]
        public async Task<ActionResult<IEnumerable<OrderResponseDto>>> GetOrders()
        {
            var orders = await _context.Orders
                .Select(o => new OrderResponseDto
                {
                    Id = o.Id,
                    CustomerName = o.CustomerName,
                    CustomerPhone = o.CustomerPhone,
                    PackageType = o.PackageType,
                    OrderDate = o.OrderDate,
                    Status = o.Status
                }).ToListAsync();

            return Ok(orders);
        }

        // GET: /orders/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<OrderResponseDto>> GetOrder(Guid id)
        {
            var order = await _context.Orders.FindAsync(id);

            if (order == null)
            {
                return NotFound();
            }

            return new OrderResponseDto
            {
                Id = order.Id,
                CustomerName = order.CustomerName,
                CustomerPhone = order.CustomerPhone,
                PackageType = order.PackageType,
                OrderDate = order.OrderDate,
                Status = order.Status
            };
        }

        // POST: /orders
        [HttpPost]
        public async Task<ActionResult<OrderResponseDto>> CreateOrder(CreateOrderDto createOrderDto)
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerName = createOrderDto.CustomerName,
                CustomerPhone = createOrderDto.CustomerPhone,
                PackageType = createOrderDto.PackageType,
                Quantity = createOrderDto.Quantity,
                OrderDate = DateTime.UtcNow,
                Status = OrderStatus.Pending
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Order {OrderId} saved to database", order.Id);

            try
            {
                var message = new OrderCreatedMessage
                {
                    OrderId = order.Id,
                    CustomerName = order.CustomerName,
                    CustomerPhone = order.CustomerPhone,
                    PackageType = order.PackageType,
                    Quantity = order.Quantity,
                    OrderDate = order.OrderDate
                };

                _rabbitMQService.PublishOrderCreated(message);

                order.Status = OrderStatus.Processing;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Message published for order {OrderId}", order.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message for order {OrderId}", order.Id);
            }

            var response = new OrderResponseDto
            {
                Id = order.Id,
                CustomerName = order.CustomerName,
                CustomerPhone = order.CustomerPhone,
                PackageType = order.PackageType,
                OrderDate = order.OrderDate,
                Status = order.Status
            };

            return CreatedAtAction(nameof(GetOrder), new {id = order.Id }, response);
        }
    }
}