using System.ComponentModel.DataAnnotations;

namespace FleetPulse.Api.DTOs;

public record DeviceDto(
    int Id,
    string Name,
    string Platform,
    string OsVersion,
    int BatteryLevel,
    double Latitude,
    double Longitude,
    bool IsLocked,
    DateTime LastSeen,
    string Status);

public record AlertDto(int Id, int DeviceId, string Message, DateTime CreatedAt);

public record DeviceDetailDto(
    int Id,
    string Name,
    string Platform,
    string OsVersion,
    int BatteryLevel,
    double Latitude,
    double Longitude,
    bool IsLocked,
    DateTime LastSeen,
    string Status,
    IReadOnlyList<AlertDto> Alerts);

public class HeartbeatRequest
{
    [Required]
    public int DeviceId { get; set; }

    [Range(0, 100)]
    public int BatteryLevel { get; set; }

    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }
}

public record PlatformCountDto(string Platform, int Count);

public record DashboardSummaryDto(
    int Total,
    int Online,
    int Offline,
    int LowBattery,
    IReadOnlyList<PlatformCountDto> Platforms);

public record LocateResponse(string Message);
