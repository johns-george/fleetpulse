using FleetPulse.Api.DTOs;
using FleetPulse.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace FleetPulse.Api.Services;

/// <summary>
/// Thin abstraction over the SignalR hub so device services stay testable.
/// </summary>
public interface IDeviceNotifier
{
    Task DeviceUpdatedAsync(DeviceDto device);
    Task AlertCreatedAsync(AlertDto alert);
    Task SendCommandAsync(int deviceId, string command);
}

public class DeviceNotifier : IDeviceNotifier
{
    private readonly IHubContext<DeviceHub> _hub;

    public DeviceNotifier(IHubContext<DeviceHub> hub)
    {
        _hub = hub;
    }

    public Task DeviceUpdatedAsync(DeviceDto device) =>
        _hub.Clients.All.SendAsync("DeviceUpdated", device);

    public Task AlertCreatedAsync(AlertDto alert) =>
        _hub.Clients.All.SendAsync("AlertCreated", alert);

    public Task SendCommandAsync(int deviceId, string command) =>
        _hub.Clients.Group(DeviceHub.GroupFor(deviceId))
            .SendAsync("ReceiveCommand", new DeviceCommand(deviceId, command));
}

public record DeviceCommand(int DeviceId, string Command);
