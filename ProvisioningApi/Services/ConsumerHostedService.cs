namespace ProvisioningApi.Services
{ 
    public class ConsumerHostedService : BackgroundService
    {
        private readonly IRabbitMQConsumer _consumer;
        private readonly ILogger<ConsumerHostedService> _logger;

        public ConsumerHostedService(IRabbitMQConsumer consumer, ILogger<ConsumerHostedService> logger)
        {
            _consumer = consumer;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Consumer hosted service starting");
            try
            {
                await _consumer.StartConsumingAsync(stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Consumer hosted service stopping due to cancellation");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in consumer hosted service");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Consumer hosted service stopping");
            _consumer.StopConsuming();
            await base.StopAsync(cancellationToken);
        } 
    }
}