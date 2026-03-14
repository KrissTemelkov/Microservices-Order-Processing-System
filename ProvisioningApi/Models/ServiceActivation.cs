using System;
using System.ComponentModel.DataAnnotations;

namespace ProvisioningApi.Models
{
    public class ServiceActivation
    {
        [Key]
        public int Id { get; set; }

        public Guid OrderId {get; set; }

        [Required]
        [StringLength(100)]
        public required string CustomerName { get; set; }

        [Required]
        [StringLength(20)]
        public required string CustomerPhone { get; set;}

        [Required]
        [StringLength(50)]
        public required string PackageType { get; set; }

        public DateTime OrderDate { get; set; }
        public DateTime ReceivedDate { get; set; }
        public DateTime? ActivationDate { get; set; }

        [Required]
        [StringLength(20)]
        public required string Status { get; set; } = "Pending";

        public string? ErrorMessage { get; set; }
        public int RetryCount { get; set; }
    }
}