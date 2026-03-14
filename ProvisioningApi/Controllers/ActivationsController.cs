using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProvisioningApi.Data;
using ProvisioningApi.Models;

namespace ProvisioningApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ActivationsController : ControllerBase
    {
        private readonly ProvisioningDbContext _context;

        public ActivationsController(ProvisioningDbContext context)
        {
            _context = context;
        }

        // GET: api/activations
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceActivation>>> GetActivations()
        {
            return await _context.ServiceActivations.ToListAsync();
        }

         // GET: api/activations/order/{orderId}
         [HttpGet("order/{oredrId}")]
         public async Task<ActionResult<ServiceActivation>> GetByOrderId(Guid orderId)
        {
            var activation = await _context.ServiceActivations
                .FirstOrDefaultAsync(a => a.OrderId == orderId);

            if (activation == null)
            {
                return NotFound();
            }

            return activation;
        }

        // GET: api/activations/pending
        [HttpGet("pending")]
        public async Task<ActionResult<IEnumerable<ServiceActivation>>> GetPending()
        {
            var pending = await _context.ServiceActivations
                .Where(a => a.Status == "Pending")
                .ToListAsync();
            return Ok(pending);
        }
    }
}