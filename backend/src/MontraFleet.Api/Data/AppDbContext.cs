using Microsoft.EntityFrameworkCore;
using MontraFleet.Api.Models;

namespace MontraFleet.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Vehicle>(entity => {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Vin).IsUnique();
            entity.Property(x => x.Vin).HasMaxLength(50).IsRequired();
            entity.Property(x => x.RegistrationNumber).HasMaxLength(30);
        });
    }
}
