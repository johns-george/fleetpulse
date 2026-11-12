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
    string Status,
    string? CurrentWorker = null);

public record AlertDto(int Id, int DeviceId, string Message, DateTime CreatedAt);

public record PagedResultDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

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
    IReadOnlyList<AlertDto> Alerts,
    string? CurrentWorker = null);

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

public record CommandAcknowledgementDto(int DeviceId, string Command, DateTime AcknowledgedAt);

public record DeviceSessionDto(
    int Id,
    int DeviceId,
    string WorkerName,
    DateTime StartedAt,
    DateTime? EndedAt,
    string? EndedBy);

public class CheckoutRequest
{
    [Required]
    [StringLength(100)]
    public string WorkerName { get; set; } = string.Empty;
}
