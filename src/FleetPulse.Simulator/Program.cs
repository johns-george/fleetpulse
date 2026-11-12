using System.Text.Json.Serialization;
using FleetPulse.Simulator;
using Microsoft.AspNetCore.SignalR.Client;

var apiUrl = (args.Length > 0
        ? args[0]
        : Environment.GetEnvironmentVariable("FLEETPULSE_API") ?? "http://localhost:5080")
    .TrimEnd('/');

var enrollmentKey = Environment.GetEnvironmentVariable("FLEETPULSE_ENROLLMENT_KEY")
                    ?? "fleetpulse-demo-enrollment-key";

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

Log.Info($"FleetPulse simulator starting against {apiUrl}");

using var api = new ApiClient(apiUrl);

IReadOnlyList<SimulatedDevice> devices;
var deviceTokens = new Dictionary<int, string>();

try
{
    devices = await api.GetDevicesForEnrollmentAsync(enrollmentKey, cts.Token);

    foreach (var device in devices)
    {
        var token = await api.EnrollAsync(device.Id, enrollmentKey, cts.Token);
        if (token is null)
        {
            Log.Error($"Device {device.Id} ({device.Name}) could not be enrolled.");
            return 1;
        }

        deviceTokens[device.Id] = token;
    }
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

Log.Info($"Enrolled {devices.Count} devices. Sending heartbeats every 5 seconds. Press Ctrl+C to stop.");

// Each device has its own token, so each needs its own hub connection.
var connections = new List<DeviceConnection>();
foreach (var device in devices)
{
    var connection = new HubConnectionBuilder()
        .WithUrl($"{apiUrl}/hubs/devices", options =>
            options.AccessTokenProvider = () => Task.FromResult<string?>(deviceTokens[device.Id]))
        .WithAutomaticReconnect()
        .Build();

    connection.On<DeviceCommand>("ReceiveCommand", async command =>
    {
        LogCommand(device.Name, command);

        try
        {
            await connection.InvokeAsync("AckCommand", command.DeviceId, command.Command);
            Log.Command($"{device.Name}: acknowledged '{command.Command}' to the server.");
        }
        catch (Exception ex)
        {
            Log.Warn($"{device.Name}: could not acknowledge '{command.Command}' ({ex.Message}).");
        }
    });

    connection.Reconnected += async _ =>
    {
        try
        {
            await connection.InvokeAsync("RegisterDevice", device.Id);
            Log.Info($"{device.Name} reconnected to the hub.");
        }
        catch (Exception ex)
        {
            Log.Warn($"{device.Name}: hub re-registration failed ({ex.Message}).");
        }
    };

    connections.Add(new DeviceConnection(device, connection));
}

foreach (var entry in connections)
{
    try
    {
        await entry.Connection.StartAsync(cts.Token);
        await entry.Connection.InvokeAsync("RegisterDevice", entry.Device.Id, cts.Token);
    }
    catch (Exception ex)
    {
        Log.Warn($"{entry.Device.Name}: hub registration failed ({ex.Message}). Heartbeats will continue.");
    }
}

Log.Info("Devices registered for lock/locate commands.");

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
                await api.SendHeartbeatAsync(heartbeat, deviceTokens[device.Id], cts.Token);
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
    foreach (var entry in connections)
    {
        if (entry.Connection.State == HubConnectionState.Connected)
        {
            await entry.Connection.StopAsync();
        }
    }
}

Log.Info("Simulator stopped.");
return 0;

static void LogCommand(string deviceName, DeviceCommand command)
{
    var message = command.Command.ToLowerInvariant() switch
    {
        "lock" => "lock command received - device screen locked",
        "unlock" => "unlock command received - device unlocked",
        "locate" => "locate command received - reporting current position",
        "checkout" => $"checkout command received - assigned to {command.WorkerName ?? "a worker"}",
        "checkin" => $"checkin command received - session for {command.WorkerName ?? "worker"} closed",
        "end-session" => $"end-session command received - session for {command.WorkerName ?? "worker"} ended by an administrator",
        _ => $"unknown command '{command.Command}' received"
    };

    Log.Command($"{deviceName} ({command.DeviceId}): {message}");
}

public record DeviceConnection(SimulatedDevice Device, HubConnection Connection);

public record DeviceCommand(
    [property: JsonPropertyName("deviceId")] int DeviceId,
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("workerName")] string? WorkerName = null);
