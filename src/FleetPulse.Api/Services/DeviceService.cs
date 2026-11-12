using FleetPulse.Api.Data;
using FleetPulse.Api.DTOs;
using FleetPulse.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetPulse.Api.Services;

public interface IDeviceService
{
    Task<IReadOnlyList<DeviceDto>> GetDevicesAsync(string? search, string? status);
    Task<DeviceDetailDto?> GetDeviceAsync(int id);
    Task<DeviceDto?> RecordHeartbeatAsync(HeartbeatRequest request);
    Task<DeviceDto?> SetLockAsync(int id, bool locked);
    Task<DeviceDto?> RequestLocateAsync(int id);
    Task<DashboardSummaryDto> GetSummaryAsync();
}

public class DeviceService : IDeviceService
{
    public const int LowBatteryThreshold = 15;
    public static readonly TimeSpan LowBatteryAlertCooldown = TimeSpan.FromMinutes(5);

    private readonly FleetPulseDbContext _db;
    private readonly IDeviceNotifier _notifier;
    private readonly TimeProvider _time;

    public DeviceService(FleetPulseDbContext db, IDeviceNotifier notifier, TimeProvider time)
    {
        _db = db;
        _notifier = notifier;
        _time = time;
    }

    public async Task<IReadOnlyList<DeviceDto>> GetDevicesAsync(string? search, string? status)
    {
        var query = _db.Devices.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            var matchingPlatforms = Enum.GetValues<Platform>()
                .Where(p => p.ToString().ToLower().Contains(term))
                .ToList();

            query = query.Where(d =>
                d.Name.ToLower().Contains(term) ||
                d.OsVersion.ToLower().Contains(term) ||
                matchingPlatforms.Contains(d.Platform));
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<DeviceStatus>(status.Trim(), ignoreCase: true, out var parsedStatus))
        {
            query = query.Where(d => d.Status == parsedStatus);
        }

        var devices = await query.OrderBy(d => d.Name).ToListAsync();
        return devices.Select(ToDto).ToList();
    }

    public async Task<DeviceDetailDto?> GetDeviceAsync(int id)
    {
        var device = await _db.Devices.AsNoTracking()
            .Include(d => d.Alerts)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (device is null)
        {
            return null;
        }

        var alerts = device.Alerts
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .Select(ToDto)
            .ToList();

        return new DeviceDetailDto(
            device.Id,
            device.Name,
            device.Platform.ToString(),
            device.OsVersion,
            device.BatteryLevel,
            device.Latitude,
            device.Longitude,
            device.IsLocked,
            device.LastSeen,
            device.Status.ToString(),
            alerts);
    }

    public async Task<DeviceDto?> RecordHeartbeatAsync(HeartbeatRequest request)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == request.DeviceId);
        if (device is null)
        {
            return null;
        }

        var wasLowBattery = device.BatteryLevel < LowBatteryThreshold;

        device.BatteryLevel = request.BatteryLevel;
        device.Latitude = request.Latitude;
        device.Longitude = request.Longitude;
        device.LastSeen = _time.GetUtcNow().UtcDateTime;
        device.Status = DeviceStatus.Online;

        Alert? alert = null;
        if (request.BatteryLevel < LowBatteryThreshold && !wasLowBattery)
        {
            // Avoid flooding the alert feed while the battery stays low.
            alert = new Alert
            {
                DeviceId = device.Id,
                Message = $"Low battery: {device.Name} is at {request.BatteryLevel}%.",
                CreatedAt = device.LastSeen
            };
            _db.Alerts.Add(alert);
        }

        await _db.SaveChangesAsync();

        var dto = ToDto(device);
        await _notifier.DeviceUpdatedAsync(dto);

        if (alert is not null)
        {
            await _notifier.AlertCreatedAsync(ToDto(alert));
        }

        return dto;
    }

    public async Task<DeviceDto?> SetLockAsync(int id, bool locked)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == id);
        if (device is null)
        {
            return null;
        }

        device.IsLocked = locked;
        await _db.SaveChangesAsync();

        var dto = ToDto(device);
        await _notifier.DeviceUpdatedAsync(dto);
        await _notifier.SendCommandAsync(device.Id, locked ? "lock" : "unlock");

        return dto;
    }

    public async Task<DeviceDto?> RequestLocateAsync(int id)
    {
        var device = await _db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        if (device is null)
        {
            return null;
        }

        await _notifier.SendCommandAsync(device.Id, "locate");
        return ToDto(device);
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync()
    {
        var devices = await _db.Devices.AsNoTracking().ToListAsync();

        var platforms = Enum.GetValues<Platform>()
            .Select(platform => new PlatformCountDto(
                platform.ToString(),
                devices.Count(d => d.Platform == platform)))
            .ToList();

        return new DashboardSummaryDto(
            devices.Count,
            devices.Count(d => d.Status == DeviceStatus.Online),
            devices.Count(d => d.Status == DeviceStatus.Offline),
            devices.Count(d => d.BatteryLevel < LowBatteryThreshold),
            platforms);
    }

    private static DeviceDto ToDto(Device device) => new(
        device.Id,
        device.Name,
        device.Platform.ToString(),
        device.OsVersion,
        device.BatteryLevel,
        device.Latitude,
        device.Longitude,
        device.IsLocked,
        device.LastSeen,
        device.Status.ToString());

    private static AlertDto ToDto(Alert alert) =>
        new(alert.Id, alert.DeviceId, alert.Message, alert.CreatedAt);
}
