using FleetPulse.Api.DTOs;
using FleetPulse.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FleetPulse.Api.Hubs;

/// <summary>
/// Admins join the dashboard group for broadcasts. Devices join only their own
/// device group, after proving the deviceId claim matches, and acknowledge
/// the commands they receive.
/// </summary>
[Authorize]
public class DeviceHub : Hub
{
    public const string DashboardGroup = "dashboard";

    private readonly IDeviceNotifier _notifier;
    private readonly TimeProvider _time;
    private readonly ILogger<DeviceHub> _logger;

    public DeviceHub(
        IDeviceNotifier notifier,
        TimeProvider time,
        ILogger<DeviceHub> logger)
    {
        _notifier = notifier;
        _time = time;
        _logger = logger;
    }

    public static string GroupFor(int deviceId) => $"device-{deviceId}";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole("Admin") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, DashboardGroup);
        }

        await base.OnConnectedAsync();
    }

    public async Task RegisterDevice(int deviceId)
    {
        RequireDeviceClaim(deviceId, "register");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(deviceId));
    }

    public async Task AckCommand(int deviceId, string command)
    {
        RequireDeviceClaim(deviceId, "acknowledge commands for");

        _logger.LogInformation(
            "Device {DeviceId} acknowledged command {Command}",
            deviceId,
            command);

        await _notifier.CommandAcknowledgedAsync(
            new CommandAcknowledgementDto(deviceId, command, _time.GetUtcNow().UtcDateTime));
    }

    private void RequireDeviceClaim(int deviceId, string action)
    {
        var claim = Context.User?.FindFirst("deviceId")?.Value;

        if (!int.TryParse(claim, out var tokenDeviceId) || tokenDeviceId != deviceId)
        {
            throw new HubException($"Only the enrolled device may {action} this id.");
        }
    }
}
