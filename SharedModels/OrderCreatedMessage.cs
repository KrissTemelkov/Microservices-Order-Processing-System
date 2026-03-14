using System;

namespace SharedModels
{
    public class OrderCreatedMessage
    {
        public Guid OrderId { get; set; }
        public required string CustomerName { get; set; }
        public required string CustomerPhone { get; set; }
        public required string PackageType { get; set; }
        public DateTime OrderDate { get; set; }
        public int Quantity { get; set; }
    }
}