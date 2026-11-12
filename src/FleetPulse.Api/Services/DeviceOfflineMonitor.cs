namespace FleetPulse.Api.Services;

/// <summary>
/// Periodically asks the sweeper to mark silent devices Offline.
/// </summary>
public class DeviceOfflineMonitor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DeviceOfflineMonitor> _logger;

    public DeviceOfflineMonitor(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<DeviceOfflineMonitor> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var scanInterval = TimeSpan.FromSeconds(
            _configuration.GetValue("Monitoring:ScanIntervalSeconds", 5));
        var offlineAfter = TimeSpan.FromSeconds(
            _configuration.GetValue("Monitoring:OfflineThresholdSeconds", 30));

        using var timer = new PeriodicTimer(scanInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sweeper = scope.ServiceProvider.GetRequiredService<IDeviceOfflineSweeper>();
                await sweeper.SweepAsync(offlineAfter, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sweep stale devices");
            }
        }
    }
}
