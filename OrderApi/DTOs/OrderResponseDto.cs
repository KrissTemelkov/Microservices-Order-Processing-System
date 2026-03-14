using SharedModels;

namespace OrderApi.DTOs
{
    public class OrderResponseDto
    {
        public Guid Id { get; set; }
        public required string CustomerName{ get; set; }
        public required string CustomerPhone{ get; set; }
        public required string PackageType{ get; set; }
        public DateTime OrderDate{ get; set; }
        public OrderStatus Status{ get; set; }
    }
}