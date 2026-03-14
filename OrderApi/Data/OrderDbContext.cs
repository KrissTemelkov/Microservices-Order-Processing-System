using Microsoft.EntityFrameworkCore;
using OrderApi.Models;

namespace OrderApi.Data
{
    public class OrderDbContext : DbContext
    {
        public OrderDbContext(DbContextOptions<OrderDbContext> options)
        : base(options) {}

        public DbSet<Order> Orders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CustomerName)
                    .IsRequired()
                    .HasMaxLength(100);
                entity.Property(e => e.CustomerPhone)
                    .IsRequired()
                    .HasMaxLength(20);
                entity.Property(e => e.PackageType)
                    .IsRequired()
                    .HasMaxLength(50);
                entity.Property(e => e.OrderDate)
                    .HasDefaultValueSql("GETDATE()");
            });
        }
    }
}