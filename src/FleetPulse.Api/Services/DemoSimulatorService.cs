using FleetPulse.Api.DTOs;

namespace FleetPulse.Api.Services;

public class DemoOptions
{
    public const string SectionName = "Demo";

    /// <summary>When true, the API simulates device traffic without the console simulator.</summary>
    public bool Enabled { get; set; }
}

/// <summary>
/// Hosted demo traffic: heartbeats every device directly through the service layer,
/// so a deployed instance has live data without running FleetPulse.Simulator.
/// </summary>
public class DemoSimulatorService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _time;
    private readonly ILogger<DemoSimulatorService> _logger;

    public DemoSimulatorService(
        IServiceScopeFactory scopeFactory,
        TimeProvider time,
        ILogger<DemoSimulatorService> logger)
    {
        _scopeFactory = scopeFactory;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Demo heartbeat tick failed");
            }
        }
    }

    /// <summary>One heartbeat round for every device. Public so tests can drive it.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var devices = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        var page = await devices.GetDevicesAsync(search: null, status: null, page: 1, pageSize: 100);

        var sent = 0;
        foreach (var device in page.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var heartbeat = new HeartbeatRequest
            {
                DeviceId = device.Id,
                // Drain one point per tick; recharge once it bottoms out at 1%.
                BatteryLevel = device.BatteryLevel <= 1 ? 100 : device.BatteryLevel - 1,
                Latitude = Drift(device.Latitude, 90),
                Longitude = Drift(device.Longitude, 180)
            };

            if (await devices.RecordHeartbeatAsync(heartbeat) is not null)
            {
                sent++;
            }
        }

        if (sent > 0)
        {
            _logger.LogDebug("Demo mode sent {Count} heartbeats at {Time}", sent, _time.GetUtcNow());
        }

        return sent;
    }

    private static double Drift(double value, double limit) =>
        Math.Round(Math.Clamp(value + (Random.Shared.NextDouble() - 0.5) * 0.002, -limit, limit), 6);
}
