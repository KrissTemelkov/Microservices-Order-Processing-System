using System.ComponentModel.DataAnnotations;

namespace OrderApi.DTOs
{
    public class CreateOrderDto
    {
        [Required]
        [StringLength(100)]
        public required string CustomerName { get; set; }

        [Required]
        [StringLength(20)]
        [Phone]
        public required string CustomerPhone { get; set; }

        [Required]
        [StringLength(50)]
        public required string PackageType { get; set; }

        [Required]
        [Range(1, 100)]
        public int Quantity { get; set; }
    }
}