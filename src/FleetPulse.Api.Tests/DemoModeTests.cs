using FleetPulse.Api.Data;
using FleetPulse.Api.Entities;
using FleetPulse.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FleetPulse.Api.Tests;

public class DemoModeTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly RecordingNotifier _notifier = new();
    private readonly ManualTimeProvider _time = new(Now);

    public DemoModeTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<FleetPulseDbContext>(options => options.UseSqlite(_connection));
        services.AddSingleton<TimeProvider>(_time);
        services.AddSingleton<IDeviceNotifier>(_notifier);
        services.AddScoped<IDeviceService, DeviceService>();

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<FleetPulseDbContext>().Database.EnsureCreated();
    }

    [Fact]
    public async Task One_demo_tick_sends_a_heartbeat_for_every_device()
    {
        await SeedAsync("Low battery", 1);
        await SeedAsync("Normal", 50);

        var demo = CreateService();

        var sent = await demo.RunOnceAsync();

        Assert.Equal(2, sent);
        Assert.Equal(2, _notifier.DeviceUpdates.Count);

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetPulseDbContext>();
        var devices = await db.Devices.OrderBy(d => d.Name).ToListAsync();

        Assert.Equal(100, devices.Single(d => d.Name == "Low battery").BatteryLevel);
        Assert.Equal(49, devices.Single(d => d.Name == "Normal").BatteryLevel);
        Assert.All(devices, d => Assert.Equal(Now.UtcDateTime, d.LastSeen));
        Assert.All(devices, d => Assert.Equal(DeviceStatus.Online, d.Status));
    }

    [Fact]
    public async Task A_demo_tick_with_no_devices_sends_nothing()
    {
        var demo = CreateService();

        Assert.Equal(0, await demo.RunOnceAsync());
        Assert.Empty(_notifier.DeviceUpdates);
    }

    [Fact]
    public async Task Demo_notifier_publishes_an_acknowledgement_after_a_command()
    {
        var notifier = new DemoDeviceNotifier(
            _notifier,
            _time,
            NullLogger<DemoDeviceNotifier>.Instance,
            TimeSpan.FromMilliseconds(20));

        await notifier.SendCommandAsync(4, "lock", "Alex");

        await WaitForAsync(() => _notifier.Acknowledgements.Count == 1);

        var ack = Assert.Single(_notifier.Acknowledgements);
        Assert.Equal(4, ack.DeviceId);
        Assert.Equal("lock", ack.Command);
        Assert.Equal(Now.UtcDateTime, ack.AcknowledgedAt);
        Assert.Contains(_notifier.Commands, c => c.Command == "lock" && c.WorkerName == "Alex");
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    private DemoSimulatorService CreateService() => new(
        _provider.GetRequiredService<IServiceScopeFactory>(),
        _time,
        NullLogger<DemoSimulatorService>.Instance);

    private async Task SeedAsync(string name, int batteryLevel)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetPulseDbContext>();

        db.Devices.Add(new Device
        {
            Name = name,
            Platform = Platform.Android,
            OsVersion = "Android 14",
            BatteryLevel = batteryLevel,
            Latitude = 12.9716,
            Longitude = 77.5946,
            LastSeen = Now.UtcDateTime.AddMinutes(-5),
            Status = DeviceStatus.Offline
        });

        await db.SaveChangesAsync();
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }
    }
}
