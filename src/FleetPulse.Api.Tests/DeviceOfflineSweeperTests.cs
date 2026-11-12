using FleetPulse.Api.Data;
using FleetPulse.Api.Entities;
using FleetPulse.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FleetPulse.Api.Tests;

public class DeviceOfflineSweeperTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly FleetPulseDbContext _db;
    private readonly RecordingNotifier _notifier = new();
    private readonly ManualTimeProvider _time = new(Now);

    public DeviceOfflineSweeperTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<FleetPulseDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new FleetPulseDbContext(options);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Devices_past_the_threshold_become_offline_and_fire_a_notification()
    {
        var stale = await SeedAsync("Stale device", Now.AddSeconds(-31), DeviceStatus.Online);
        var recent = await SeedAsync("Recent device", Now.AddSeconds(-5), DeviceStatus.Online);
        var alreadyOffline = await SeedAsync("Old offline device", Now.AddMinutes(-5), DeviceStatus.Offline);

        var sweeper = CreateSweeper();
        var changed = await sweeper.SweepAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(1, changed);
        Assert.Equal(DeviceStatus.Offline, (await _db.Devices.SingleAsync(d => d.Id == stale.Id)).Status);
        Assert.Equal(DeviceStatus.Online, (await _db.Devices.SingleAsync(d => d.Id == recent.Id)).Status);
        Assert.Equal(DeviceStatus.Offline, (await _db.Devices.SingleAsync(d => d.Id == alreadyOffline.Id)).Status);

        var update = Assert.Single(_notifier.DeviceUpdates);
        Assert.Equal(stale.Id, update.Id);
        Assert.Equal("Offline", update.Status);
    }

    [Fact]
    public async Task Recent_devices_are_untouched()
    {
        await SeedAsync("Recent device", Now.AddSeconds(-10), DeviceStatus.Online);

        var sweeper = CreateSweeper();
        var changed = await sweeper.SweepAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(0, changed);
        Assert.Empty(_notifier.DeviceUpdates);
        Assert.Equal(DeviceStatus.Online, (await _db.Devices.SingleAsync()).Status);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private DeviceOfflineSweeper CreateSweeper() =>
        new(_db, _notifier, _time, NullLogger<DeviceOfflineSweeper>.Instance);

    private async Task<Device> SeedAsync(string name, DateTimeOffset lastSeen, DeviceStatus status)
    {
        var device = new Device
        {
            Name = name,
            Platform = Platform.Android,
            OsVersion = "Test OS",
            BatteryLevel = 60,
            Latitude = 12.9716,
            Longitude = 77.5946,
            LastSeen = lastSeen.UtcDateTime,
            Status = status
        };

        _db.Devices.Add(device);
        await _db.SaveChangesAsync();
        return device;
    }
}
