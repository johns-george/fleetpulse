using FleetPulse.Api.Data;
using FleetPulse.Api.DTOs;
using FleetPulse.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetPulse.Api.Services;

public interface IDeviceService
{
    Task<PagedResultDto<DeviceDto>> GetDevicesAsync(string? search, string? status, int page = 1, int pageSize = 20);
    Task<DeviceDetailDto?> GetDeviceAsync(int id);
    Task<DeviceDto?> RecordHeartbeatAsync(HeartbeatRequest request);
    Task<DeviceDto?> SetLockAsync(int id, bool locked);
    Task<DeviceDto?> RequestLocateAsync(int id);
    Task<DashboardSummaryDto> GetSummaryAsync();
}

public class DeviceService : IDeviceService
{
    public const int LowBatteryThreshold = 15;

    private readonly FleetPulseDbContext _db;
    private readonly IDeviceNotifier _notifier;
    private readonly TimeProvider _time;

    public DeviceService(FleetPulseDbContext db, IDeviceNotifier notifier, TimeProvider time)
    {
        _db = db;
        _notifier = notifier;
        _time = time;
    }

    public async Task<PagedResultDto<DeviceDto>> GetDevicesAsync(
        string? search,
        string? status,
        int page = 1,
        int pageSize = 20)
    {
        const int maxPageSize = 100;
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, maxPageSize);

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

        var totalCount = await query.CountAsync();

        var devices = await query
            .OrderBy(d => d.Name)
            .ThenBy(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var deviceIds = devices.Select(d => d.Id).ToList();
        var activeWorkers = await _db.DeviceSessions.AsNoTracking()
            .Where(s => s.EndedAt == null && deviceIds.Contains(s.DeviceId))
            .ToDictionaryAsync(s => s.DeviceId, s => s.WorkerName);

        return new PagedResultDto<DeviceDto>(
            devices.Select(d => DeviceMapper.ToDto(d, activeWorkers.GetValueOrDefault(d.Id))).ToList(),
            page,
            pageSize,
            totalCount);
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
            .Select(DeviceMapper.ToDto)
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
            alerts,
            await GetActiveWorkerAsync(device.Id));
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

        var dto = DeviceMapper.ToDto(device, await GetActiveWorkerAsync(device.Id));
        await _notifier.DeviceUpdatedAsync(dto);

        if (alert is not null)
        {
            await _notifier.AlertCreatedAsync(DeviceMapper.ToDto(alert));
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

        var dto = DeviceMapper.ToDto(device, await GetActiveWorkerAsync(device.Id));
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
        return DeviceMapper.ToDto(device, await GetActiveWorkerAsync(device.Id));
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync()
    {
        var total = await _db.Devices.CountAsync();
        var online = await _db.Devices.CountAsync(d => d.Status == DeviceStatus.Online);
        var lowBattery = await _db.Devices.CountAsync(d => d.BatteryLevel < LowBatteryThreshold);

        var platformCounts = await _db.Devices
            .GroupBy(d => d.Platform)
            .Select(group => new { Platform = group.Key, Count = group.Count() })
            .ToListAsync();

        // Keep the chart stable by returning every known platform, including empty ones.
        var platforms = Enum.GetValues<Platform>()
            .Select(platform => new PlatformCountDto(
                platform.ToString(),
                platformCounts.FirstOrDefault(item => item.Platform == platform)?.Count ?? 0))
            .ToList();

        return new DashboardSummaryDto(total, online, total - online, lowBattery, platforms);
    }

    private Task<string?> GetActiveWorkerAsync(int deviceId) =>
        _db.DeviceSessions.AsNoTracking()
            .Where(s => s.DeviceId == deviceId && s.EndedAt == null)
            .Select(s => s.WorkerName)
            .FirstOrDefaultAsync();
}
