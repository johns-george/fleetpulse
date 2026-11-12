using FleetPulse.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetPulse.Api.Data;

public class FleetPulseDbContext : DbContext
{
    public FleetPulseDbContext(DbContextOptions<FleetPulseDbContext> options) : base(options)
    {
    }

    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<DeviceSession> DeviceSessions => Set<DeviceSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>(entity =>
        {
            entity.Property(d => d.Name).IsRequired().HasMaxLength(100);
            entity.Property(d => d.OsVersion).HasMaxLength(50);
            entity.Property(d => d.Platform).HasConversion<string>().HasMaxLength(20);
            entity.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasMany(d => d.Alerts)
                .WithOne(a => a.Device)
                .HasForeignKey(a => a.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Alert>(entity =>
        {
            entity.Property(a => a.Message).IsRequired().HasMaxLength(250);
        });

        modelBuilder.Entity<DeviceSession>(entity =>
        {
            entity.Property(s => s.WorkerName).IsRequired().HasMaxLength(100);
            entity.Property(s => s.EndedBy).HasMaxLength(100);
            entity.HasIndex(s => s.DeviceId);
            entity.HasOne(s => s.Device)
                .WithMany()
                .HasForeignKey(s => s.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
