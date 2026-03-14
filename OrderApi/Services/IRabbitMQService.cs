using SharedModels;

namespace OrderApi.Services
{
    public interface IRabbitMQService
    {
        void PublishOrderCreated(OrderCreatedMessage message);
    }
}