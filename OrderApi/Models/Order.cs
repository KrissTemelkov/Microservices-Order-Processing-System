using System;
using System.ComponentModel.DataAnnotations;
using SharedModels;

namespace OrderApi.Models
{
    public class Order
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public required string CustomerName { get; set; }

        [Required]
        [StringLength(20)]
        public required string CustomerPhone { get; set; }

        [Required]
        [StringLength(50)]
        public required string PackageType { get; set; }

        public DateTime OrderDate { get; set; }

        public int Quantity { get; set; }

        public OrderStatus Status { get; set; }

        public DateTime? ProcessedDate { get; set; }
    }
}