using CircuitApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CircuitApi.Data
{

    public class CircuitDbContext : DbContext
    {
        public CircuitDbContext(
            DbContextOptions<CircuitDbContext> options) : base(options)
        {
        }

        public DbSet<Circuit> Circuits { get; set; } = null!;
        public DbSet<SharedCircuit> SharedCircuits { get; set; } = null!;

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
                entity.Property(circuit => circuit.OwnerId).HasMaxLength(32);
                entity.HasIndex(circuit => new { circuit.OwnerId, circuit.UpdatedAt });
            });
            modelBuilder.Entity<SharedCircuit>(entity =>
            {
                entity.HasKey(circuit => circuit.SharedCircuitId);
                entity.Property(circuit => circuit.OwnerId).IsRequired().HasMaxLength(32);
                entity.Property(circuit => circuit.Name).IsRequired().HasMaxLength(100);
                entity.Property(circuit => circuit.AuthorName).IsRequired().HasMaxLength(60);
                entity.Property(circuit => circuit.Description).IsRequired().HasMaxLength(500);
                entity.Property(circuit => circuit.JsonData).IsRequired();
                entity.Property(circuit => circuit.PublishedAt).IsRequired();
                entity.HasIndex(circuit => circuit.PublishedAt);
            });
        }
    }
}
