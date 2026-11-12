using System.Text.Json.Serialization;
using FleetPulse.Simulator;
using Microsoft.AspNetCore.SignalR.Client;

var apiUrl = (args.Length > 0
        ? args[0]
        : Environment.GetEnvironmentVariable("FLEETPULSE_API") ?? "http://localhost:5080")
    .TrimEnd('/');

const string DemoUsername = "admin";
const string DemoPassword = "Admin@123";

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

Log.Info($"FleetPulse simulator starting against {apiUrl}");

using var api = new ApiClient(apiUrl);

IReadOnlyList<SimulatedDevice> devices;
try
{
    var token = await api.LoginAsync(DemoUsername, DemoPassword, cts.Token);
    if (token is null)
    {
        Log.Error("Login failed. Check the demo credentials in FleetPulse.Api/appsettings.json.");
        return 1;
    }

    devices = await api.GetDevicesAsync(cts.Token);
}
catch (Exception ex)
{
    Log.Error($"Could not reach the API at {apiUrl}. Is it running? ({ex.Message})");
    return 1;
}

if (devices.Count == 0)
{
    Log.Error("The API returned no devices. Start the API once so it seeds the database.");
    return 1;
}

Log.Info($"Simulating {devices.Count} devices every 5 seconds. Press Ctrl+C to stop.");

await using var hub = new HubConnectionBuilder()
    .WithUrl($"{apiUrl}/hubs/devices", options =>
        options.AccessTokenProvider = () => Task.FromResult(api.Token))
    .WithAutomaticReconnect()
    .Build();

hub.On<DeviceCommand>("ReceiveCommand", command =>
{
    var message = command.Command.ToLowerInvariant() switch
    {
        "lock" => "lock command received - device screen locked",
        "unlock" => "unlock command received - device unlocked",
        "locate" => "locate command received - reporting current position",
        _ => $"unknown command '{command.Command}' received"
    };

    Log.Command($"Device {command.DeviceId}: {message}");
});

hub.Reconnected += async _ =>
{
    Log.Info("Reconnected to the hub, re-registering devices.");
    await RegisterDevicesAsync(hub, devices, cts.Token);
};

try
{
    await hub.StartAsync(cts.Token);
    await RegisterDevicesAsync(hub, devices, cts.Token);
}
catch (Exception ex)
{
    Log.Warn($"SignalR connection failed ({ex.Message}). Heartbeats will continue without command handling.");
}

try
{
    while (!cts.IsCancellationRequested)
    {
        var sent = 0;
        foreach (var device in devices)
        {
            var heartbeat = device.NextHeartbeat();
            try
            {
                await api.SendHeartbeatAsync(heartbeat, cts.Token);
                sent++;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Warn($"{device.Name}: heartbeat failed ({ex.Message})");
            }
        }

        Log.Heartbeat($"{sent} heartbeats sent. {devices[0].Name} battery now {devices[0].Battery:F0}%.");

        await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
    }
}
catch (OperationCanceledException)
{
    // Normal shutdown via Ctrl+C.
}
finally
{
    if (hub.State == HubConnectionState.Connected)
    {
        await hub.StopAsync();
    }
}

Log.Info("Simulator stopped.");
return 0;

static async Task RegisterDevicesAsync(HubConnection connection, IEnumerable<SimulatedDevice> devices, CancellationToken cancellationToken)
{
    foreach (var device in devices)
    {
        await connection.InvokeAsync("RegisterDevice", device.Id, cancellationToken);
    }

    Log.Info("Registered with the hub for lock/locate commands.");
}

public record DeviceCommand(
    [property: JsonPropertyName("deviceId")] int DeviceId,
    [property: JsonPropertyName("command")] string Command);
