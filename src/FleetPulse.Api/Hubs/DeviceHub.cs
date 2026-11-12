using Microsoft.AspNetCore.SignalR;

namespace FleetPulse.Api.Hubs;

/// <summary>
/// Dashboard clients receive broadcasts. Simulated devices register themselves
/// and then receive lock/locate commands targeted at their device id.
/// </summary>
public class DeviceHub : Hub
{
    public static string GroupFor(int deviceId) => $"device-{deviceId}";

    public Task RegisterDevice(int deviceId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(deviceId));

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "dashboard");
        await base.OnConnectedAsync();
    }
}
