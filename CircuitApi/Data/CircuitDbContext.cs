using CircuitApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CircuitApi.Data
{
    // Connects circuit data to the database.
    public class CircuitDbContext : DbContext
    {
        public CircuitDbContext(
            DbContextOptions<CircuitDbContext> options) : base(options)
        {
        }

        public DbSet<Circuit> Circuits { get; set; } = null!; // Database table.

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Circuit>(entity =>
            {
                entity.HasKey(circuit => circuit.CircuitId);
                entity.Property(circuit => circuit.Name).IsRequired().HasMaxLength(100);
                entity.Property(circuit => circuit.JsonData).IsRequired();
                entity.Property(circuit => circuit.CreatedAt).IsRequired();
                entity.Property(circuit => circuit.UpdatedAt).IsRequired();
            });
        }
    }
}