using FleetPulse.Api.Data;
using FleetPulse.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetPulse.Api.Services;

/// <summary>
/// Marks devices Offline when their last heartbeat is older than the threshold.
/// Extracted from the hosted service so the sweep can be tested directly.
/// </summary>
public interface IDeviceOfflineSweeper
{
    /// <summary>Returns the number of devices that were moved to Offline.</summary>
    Task<int> SweepAsync(TimeSpan offlineAfter, CancellationToken cancellationToken = default);
}

public class DeviceOfflineSweeper : IDeviceOfflineSweeper
{
    private readonly FleetPulseDbContext _db;
    private readonly IDeviceNotifier _notifier;
    private readonly TimeProvider _time;
    private readonly ILogger<DeviceOfflineSweeper> _logger;

    public DeviceOfflineSweeper(
        FleetPulseDbContext db,
        IDeviceNotifier notifier,
        TimeProvider time,
        ILogger<DeviceOfflineSweeper> logger)
    {
        _db = db;
        _notifier = notifier;
        _time = time;
        _logger = logger;
    }

    public async Task<int> SweepAsync(TimeSpan offlineAfter, CancellationToken cancellationToken = default)
    {
        var cutoff = _time.GetUtcNow().UtcDateTime - offlineAfter;

        var staleDevices = await _db.Devices
            .Where(d => d.Status == DeviceStatus.Online && d.LastSeen < cutoff)
            .ToListAsync(cancellationToken);

        if (staleDevices.Count == 0)
        {
            return 0;
        }

        foreach (var device in staleDevices)
        {
            device.Status = DeviceStatus.Offline;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var deviceIds = staleDevices.Select(d => d.Id).ToList();
        var activeWorkers = await _db.DeviceSessions.AsNoTracking()
            .Where(s => s.EndedAt == null && deviceIds.Contains(s.DeviceId))
            .ToDictionaryAsync(s => s.DeviceId, s => s.WorkerName, cancellationToken);

        foreach (var device in staleDevices)
        {
            _logger.LogInformation("Device {DeviceName} marked offline", device.Name);
            await _notifier.DeviceUpdatedAsync(
                DeviceMapper.ToDto(device, activeWorkers.GetValueOrDefault(device.Id)));
        }

        return staleDevices.Count;
    }
}
