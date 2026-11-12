using FleetPulse.Api.DTOs;
using FleetPulse.Api.Entities;

namespace FleetPulse.Api.Services;

/// <summary>
/// Single place to map entities to API DTOs so services and background work stay consistent.
/// </summary>
public static class DeviceMapper
{
    public static DeviceDto ToDto(Device device, string? currentWorker = null) => new(
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
        currentWorker);

    public static AlertDto ToDto(Alert alert) =>
        new(alert.Id, alert.DeviceId, alert.Message, alert.CreatedAt);
}
