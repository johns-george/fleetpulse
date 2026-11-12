using FleetPulse.Api.Auth;
using FleetPulse.Api.Data;
using FleetPulse.Api.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FleetPulse.Api.Services;

public interface IEnrollmentService
{
    bool IsValidKey(string? enrollmentKey);
    Task<EnrollmentDeviceDto?> GetDeviceAsync(int deviceId);
    Task<IReadOnlyList<EnrollmentDeviceDto>> GetDevicesAsync();
    EnrollmentResponse IssueToken(EnrollmentDeviceDto device);
}

public class EnrollmentService : IEnrollmentService
{
    private readonly FleetPulseDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly EnrollmentOptions _options;

    public EnrollmentService(
        FleetPulseDbContext db,
        ITokenService tokenService,
        IOptions<EnrollmentOptions> options)
    {
        _db = db;
        _tokenService = tokenService;
        _options = options.Value;
    }

    public bool IsValidKey(string? enrollmentKey)
    {
        if (string.IsNullOrWhiteSpace(_options.Key) || string.IsNullOrWhiteSpace(enrollmentKey))
        {
            return false;
        }

        return string.Equals(enrollmentKey, _options.Key, StringComparison.Ordinal);
    }

    public async Task<EnrollmentDeviceDto?> GetDeviceAsync(int deviceId)
    {
        var device = await _db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deviceId);
        return device is null ? null : ToDto(device);
    }

    public async Task<IReadOnlyList<EnrollmentDeviceDto>> GetDevicesAsync()
    {
        var devices = await _db.Devices.AsNoTracking().OrderBy(d => d.Id).ToListAsync();
        return devices.Select(ToDto).ToList();
    }

    public EnrollmentResponse IssueToken(EnrollmentDeviceDto device)
    {
        var (token, expiresAt) = _tokenService.CreateDeviceToken(device.Id);
        return new EnrollmentResponse(token, device.Id, expiresAt);
    }

    private static EnrollmentDeviceDto ToDto(Entities.Device device) => new(
        device.Id,
        device.Name,
        device.BatteryLevel,
        device.Latitude,
        device.Longitude);
}
