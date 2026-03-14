using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ProvisioningApi.Data;
using ProvisioningApi.Models;
using SharedModels;

namespace ProvisioningApi.Services
{
    public class RabbitMQConsumer : IRabbitMQConsumer, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IChannel _channel;
        private readonly string _queueName;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RabbitMQConsumer> _logger;
        private AsyncEventingBasicConsumer _consumer = null!;
        private bool _isConsuming;

        public RabbitMQConsumer(
            IConfiguration configuration,
            IServiceScopeFactory scopeFactory,
            ILogger<RabbitMQConsumer> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;

            try
            {
                var hostName = configuration["RabbitMQ:HostName"] ?? "localhost";
                var userName = configuration["RabbitMQ:UserName"] ?? "guest";
                var password = configuration["RabbitMQ:Password"] ?? "guest";
                var exchangeName = configuration["RabbitMQ:ExchangeName"] ?? "orderExchange";
                _queueName = configuration["RabbitMQ:QueueName"] ?? "orderQueue";

                var factory = new ConnectionFactory
                {
                    HostName = hostName,
                    UserName = userName,
                    Password = password,
                    Port = 5672,
                    RequestedHeartbeat = TimeSpan.FromSeconds(60),
                };

                _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
                _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

                _channel.ExchangeDeclareAsync(
                    exchange: exchangeName,
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false).GetAwaiter().GetResult();

                _channel.QueueDeclareAsync(
                    queue: _queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null).GetAwaiter().GetResult();

                _channel.QueueBindAsync(
                    queue: _queueName,
                    exchange: exchangeName,
                    routingKey: "order.created").GetAwaiter().GetResult();

                _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false).GetAwaiter().GetResult();

                _logger.LogInformation("RabbitMQ consumer initialized for queue {QueueName}", _queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize RabbitMQ consumer");
                throw;
            }
        }

        public async Task StartConsumingAsync(CancellationToken cancellationToken)
        {
            if (_isConsuming) return;

            _consumer = new AsyncEventingBasicConsumer(_channel);
            _consumer.ReceivedAsync += async (model, ea) =>
            {
                await ProcessMessageAsync(model, ea, cancellationToken);
            };

            await _channel.BasicConsumeAsync(
                queue: _queueName,
                autoAck: false,
                consumer: _consumer);

            _isConsuming = true;
            _logger.LogInformation("Started consuming messages from queue {QueueName}", _queueName);
        }

        private async Task ProcessMessageAsync(object model, BasicDeliverEventArgs ea, CancellationToken cancellationToken)
        {
            var body = ea.Body.ToArray();
            var messageJson = Encoding.UTF8.GetString(body);
            var messageId = ea.BasicProperties?.MessageId ?? "(no message id)";

            _logger.LogInformation("Received message {MessageId}", messageId);

            try
            {
                var orderMessage = JsonSerializer.Deserialize<OrderCreatedMessage>(messageJson);
                if (orderMessage == null)
                {
                    _logger.LogWarning("Received null message, rejecting");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
                    return;
                }

                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<ProvisioningDbContext>();

                    var activation = await dbContext.ServiceActivations
                        .FirstOrDefaultAsync(a => a.OrderId == orderMessage.OrderId, cancellationToken);

                    if (activation != null)
                    {
                        if (activation.Status == "Activated")
                        {
                            _logger.LogInformation("Order {OrderId} already activated, skipping", orderMessage.OrderId);
                            await _channel.BasicAckAsync(ea.DeliveryTag, false);
                            return;
                        }

                        activation.RetryCount++;
                        _logger.LogInformation("Retrying order {OrderId}, attempt {RetryCount}", orderMessage.OrderId, activation.RetryCount);
                    }
                    else
                    {
                        activation = new ServiceActivation
                        {
                            OrderId = orderMessage.OrderId,
                            CustomerName = orderMessage.CustomerName,
                            CustomerPhone = orderMessage.CustomerPhone,
                            PackageType = orderMessage.PackageType,
                            OrderDate = orderMessage.OrderDate,
                            ReceivedDate = DateTime.UtcNow,
                            Status = "Pending",
                            RetryCount = 1
                        };
                        dbContext.ServiceActivations.Add(activation);
                    }

                    await dbContext.SaveChangesAsync(cancellationToken);

                    await SimulateServiceActivationAsync(orderMessage);

                    activation.Status = "Activated";
                    activation.ActivationDate = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation("Successfully activated service for order {OrderId}", orderMessage.OrderId);
                }

                await _channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message {MessageId}", messageId);

                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<ProvisioningDbContext>();
                    var orderMsg = JsonSerializer.Deserialize<OrderCreatedMessage>(messageJson);
                    if (orderMsg != null)
                    {
                        var activation = await dbContext.ServiceActivations
                            .FirstOrDefaultAsync(a => a.OrderId == orderMsg.OrderId, cancellationToken);

                        int retryCount = activation?.RetryCount ?? 1;
                        if (retryCount < 3)
                        {
                            _logger.LogWarning("Requeuing message {MessageId}, attempt {RetryCount}/3", messageId, retryCount);
                            await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                        }
                        else
                        {
                            _logger.LogError("Max retries reached for message {MessageId}, discarding", messageId);
                            await _channel.BasicNackAsync(ea.DeliveryTag, false, false);

                            if (activation != null)
                            {
                                activation.Status = "Failed";
                                activation.ErrorMessage = ex.Message;
                                await dbContext.SaveChangesAsync(cancellationToken);
                            }
                        }
                    }
                    else
                    {
                        // Can't deserialize – reject without requeue
                        await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
                    }
                }
            }
        }

        private Task SimulateServiceActivationAsync(OrderCreatedMessage message)
        {
            _logger.LogInformation("Activating {PackageType} for {CustomerName}", message.PackageType, message.CustomerName);
            var delay = message.PackageType switch
            {
                "Internet" => 2000,
                "Mobile" => 1500,
                "TV" => 1000,
                _ => 500
            };
            return Task.Delay(delay);
        }

        private async Task SaveFailedMessageAsync(OrderCreatedMessage message, string error)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ProvisioningDbContext>();

                var failed = await dbContext.ServiceActivations
                    .FirstOrDefaultAsync(a => a.OrderId == message.OrderId);

                if (failed != null)
                {
                    failed.Status = "Failed";
                    failed.ErrorMessage = error;
                    await dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save error status for order {OrderId}", message.OrderId);
            }
        }

        public void StopConsuming()
        {
            if (_isConsuming)
            {
                _channel?.CloseAsync().GetAwaiter().GetResult();
                _isConsuming = false;
                _logger.LogInformation("Stopped consuming messages");
            }
        }

        public void Dispose()
        {
            StopConsuming();
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}