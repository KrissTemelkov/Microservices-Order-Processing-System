namespace ProvisioningApi.Services
{
    public interface IRabbitMQConsumer
    {
        Task StartConsumingAsync(CancellationToken cancellationToken);
        void StopConsuming();
    }
}