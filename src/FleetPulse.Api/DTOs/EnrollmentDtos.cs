using System.ComponentModel.DataAnnotations;

namespace FleetPulse.Api.DTOs;

public class EnrollmentRequest
{
    [Required]
    public int DeviceId { get; set; }

    [Required]
    public string EnrollmentKey { get; set; } = string.Empty;
}

public record EnrollmentResponse(string Token, int DeviceId, DateTime ExpiresAtUtc);

/// <summary>Minimal device information the simulator needs before it has a token.</summary>
public record EnrollmentDeviceDto(
    int Id,
    string Name,
    int BatteryLevel,
    double Latitude,
    double Longitude);
