using Microsoft.EntityFrameworkCore;
using ProvisioningApi.Models;

namespace ProvisioningApi.Data
{
    public class ProvisioningDbContext : DbContext
    {
        public ProvisioningDbContext(DbContextOptions<ProvisioningDbContext> options)
            : base(options){}

        public DbSet<ServiceActivation> ServiceActivations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ServiceActivation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.OrderId).IsUnique();
                entity.Property(e => e.CustomerName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CustomerPhone).IsRequired().HasMaxLength(20);
                entity.Property(e => e.PackageType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
                entity.Property(e => e.ReceivedDate).HasDefaultValueSql("datetime('now')");
            });
        }
    }
}