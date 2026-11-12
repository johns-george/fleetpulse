using FleetPulse.Api.Data;
using FleetPulse.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetPulse.Api.Services;

/// <summary>
/// Marks devices Offline when no heartbeat has arrived within the configured window.
/// </summary>
public class DeviceOfflineMonitor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DeviceOfflineMonitor> _logger;

    public DeviceOfflineMonitor(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<DeviceOfflineMonitor> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var scanInterval = TimeSpan.FromSeconds(
            _configuration.GetValue("Monitoring:ScanIntervalSeconds", 5));
        var offlineAfter = TimeSpan.FromSeconds(
            _configuration.GetValue("Monitoring:OfflineThresholdSeconds", 30));

        using var timer = new PeriodicTimer(scanInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(offlineAfter, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sweep stale devices");
            }
        }
    }

    private async Task SweepAsync(TimeSpan offlineAfter, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetPulseDbContext>();
        var notifier = scope.ServiceProvider.GetRequiredService<IDeviceNotifier>();

        var cutoff = DateTime.UtcNow - offlineAfter;
        var staleDevices = await db.Devices
            .Where(d => d.Status == DeviceStatus.Online && d.LastSeen < cutoff)
            .ToListAsync(cancellationToken);

        if (staleDevices.Count == 0)
        {
            return;
        }

        foreach (var device in staleDevices)
        {
            device.Status = DeviceStatus.Offline;
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var device in staleDevices)
        {
            _logger.LogInformation("Device {DeviceName} marked offline", device.Name);
            await notifier.DeviceUpdatedAsync(new DTOs.DeviceDto(
                device.Id,
                device.Name,
                device.Platform.ToString(),
                device.OsVersion,
                device.BatteryLevel,
                device.Latitude,
                device.Longitude,
                device.IsLocked,
                device.LastSeen,
                device.Status.ToString()));
        }
    }
}
