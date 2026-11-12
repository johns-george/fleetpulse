using FleetPulse.Api.Data;
using FleetPulse.Api.DTOs;
using FleetPulse.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetPulse.Api.Services;

public enum SessionOutcome
{
    Success,
    DeviceNotFound,
    AlreadyCheckedOut,
    NoActiveSession
}

public record SessionResult(
    SessionOutcome Outcome,
    int DeviceId,
    DeviceDto? Device = null,
    string? Message = null);

public interface IDeviceSessionService
{
    Task<SessionResult> CheckoutAsync(int deviceId, string workerName);
    Task<SessionResult> CheckinAsync(int deviceId);
    Task<SessionResult> ForceEndAsync(int deviceId, string adminUsername);

    /// <summary>Latest 20 sessions, or null when the device does not exist.</summary>
    Task<IReadOnlyList<DeviceSessionDto>?> GetSessionsAsync(int deviceId);
}

public class DeviceSessionService : IDeviceSessionService
{
    private const int MaxSessions = 20;

    private readonly FleetPulseDbContext _db;
    private readonly IDeviceNotifier _notifier;
    private readonly TimeProvider _time;

    public DeviceSessionService(
        FleetPulseDbContext db,
        IDeviceNotifier notifier,
        TimeProvider time)
    {
        _db = db;
        _notifier = notifier;
        _time = time;
    }

    public async Task<SessionResult> CheckoutAsync(int deviceId, string workerName)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId);
        if (device is null)
        {
            return new SessionResult(SessionOutcome.DeviceNotFound, deviceId);
        }

        if (await HasActiveSessionAsync(deviceId))
        {
            return new SessionResult(SessionOutcome.AlreadyCheckedOut, deviceId,
                Message: "This device is already checked out.");
        }

        var worker = workerName.Trim();

        _db.DeviceSessions.Add(new DeviceSession
        {
            DeviceId = deviceId,
            WorkerName = worker,
            StartedAt = _time.GetUtcNow().UtcDateTime
        });
        await _db.SaveChangesAsync();

        var dto = DeviceMapper.ToDto(device, worker);
        await _notifier.DeviceUpdatedAsync(dto);
        await _notifier.SendCommandAsync(deviceId, "checkout", worker);

        return new SessionResult(SessionOutcome.Success, deviceId, dto);
    }

    public async Task<SessionResult> CheckinAsync(int deviceId)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId);
        if (device is null)
        {
            return new SessionResult(SessionOutcome.DeviceNotFound, deviceId);
        }

        var session = await _db.DeviceSessions
            .FirstOrDefaultAsync(s => s.DeviceId == deviceId && s.EndedAt == null);

        if (session is null)
        {
            return new SessionResult(SessionOutcome.NoActiveSession, deviceId,
                Message: "This device has no active session.");
        }

        session.EndedAt = _time.GetUtcNow().UtcDateTime;
        session.EndedBy = "worker";
        await _db.SaveChangesAsync();

        var dto = DeviceMapper.ToDto(device);
        await _notifier.DeviceUpdatedAsync(dto);
        await _notifier.SendCommandAsync(deviceId, "checkin", session.WorkerName);

        return new SessionResult(SessionOutcome.Success, deviceId, dto);
    }

    public async Task<SessionResult> ForceEndAsync(int deviceId, string adminUsername)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId);
        if (device is null)
        {
            return new SessionResult(SessionOutcome.DeviceNotFound, deviceId);
        }

        var session = await _db.DeviceSessions
            .FirstOrDefaultAsync(s => s.DeviceId == deviceId && s.EndedAt == null);

        if (session is null)
        {
            return new SessionResult(SessionOutcome.NoActiveSession, deviceId,
                Message: "This device has no active session.");
        }

        session.EndedAt = _time.GetUtcNow().UtcDateTime;
        session.EndedBy = $"admin:{adminUsername}";
        await _db.SaveChangesAsync();

        var dto = DeviceMapper.ToDto(device);
        await _notifier.DeviceUpdatedAsync(dto);
        await _notifier.SendCommandAsync(deviceId, "end-session", session.WorkerName);

        return new SessionResult(SessionOutcome.Success, deviceId, dto);
    }

    public async Task<IReadOnlyList<DeviceSessionDto>?> GetSessionsAsync(int deviceId)
    {
        if (!await _db.Devices.AnyAsync(d => d.Id == deviceId))
        {
            return null;
        }

        var sessions = await _db.DeviceSessions.AsNoTracking()
            .Where(s => s.DeviceId == deviceId)
            .OrderByDescending(s => s.StartedAt)
            .ThenByDescending(s => s.Id)
            .Take(MaxSessions)
            .ToListAsync();

        return sessions
            .Select(s => new DeviceSessionDto(s.Id, s.DeviceId, s.WorkerName, s.StartedAt, s.EndedAt, s.EndedBy))
            .ToList();
    }

    private Task<bool> HasActiveSessionAsync(int deviceId) =>
        _db.DeviceSessions.AnyAsync(s => s.DeviceId == deviceId && s.EndedAt == null);
}
