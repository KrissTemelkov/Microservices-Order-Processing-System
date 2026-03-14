using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using SharedModels;

namespace OrderApi.Services
{
    public class RabbitMQService : IRabbitMQService, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IChannel _channel;
        private readonly string _exchangeName;
        private readonly ILogger<RabbitMQService> _logger;

        public RabbitMQService(IConfiguration configuration, ILogger<RabbitMQService> logger)
        {
            _logger = logger;

            try
            {
                var hostName = configuration["RabbitMQ:HostName"] ?? "localhost";
                var userName = configuration["RabbitMQ:UserName"] ?? "guest";
                var password = configuration["RabbitMQ:Password"] ?? "guest";
                _exchangeName = configuration["RabbitMQ:ExchangeName"] ?? "orderExchange";

                var factory = new ConnectionFactory
                {
                    HostName = hostName,
                    UserName = userName,
                    Password = password,
                    Port = 5672,
                    RequestedHeartbeat = TimeSpan.FromSeconds(60)
                };
                _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
                _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

                _channel.ExchangeDeclareAsync(
                    exchange: _exchangeName,
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false).GetAwaiter().GetResult();
                
                _logger.LogInformation("RabbitMQ connection established");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Faild to connect to RabbitMQ");
                throw; // Rethrow – the app won't start if RabbitMQ is unavailable
            }
        }

        public void PublishOrderCreated(OrderCreatedMessage message)
        {
            try
            {
                var json = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(json);

                var properties = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    MessageId = message.OrderId.ToString(),
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                };

                _channel.BasicPublishAsync(
                    exchange: _exchangeName,
                    routingKey: "order.created",
                    mandatory: false,
                    basicProperties: properties,
                    body: body).GetAwaiter().GetResult(); 

                _logger.LogInformation("Published order {OrderId} to RabbitMQ", message.OrderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish order {OrderId}", message.OrderId);
                throw;
            }
        }
        
        public void Dispose()
        {
            _channel?.CloseAsync().GetAwaiter().GetResult();
            _connection?.CloseAsync().GetAwaiter().GetResult();
            _logger.LogInformation("RabbitMQ connection closed");
        }
    }
}