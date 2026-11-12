using FleetPulse.Api.Data;
using FleetPulse.Api.DTOs;
using FleetPulse.Api.Entities;
using FleetPulse.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FleetPulse.Api.Tests;

public class DeviceServiceTests : IDisposable
{
    private static readonly DateTimeOffset StartTime = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly FleetPulseDbContext _db;
    private readonly RecordingNotifier _notifier = new();
    private readonly ManualTimeProvider _time = new(StartTime);

    public DeviceServiceTests()
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
    public async Task Heartbeat_updates_battery_location_and_marks_device_online()
    {
        var device = await SeedDeviceAsync(batteryLevel: 80, DeviceStatus.Offline);
        var service = CreateService();

        var result = await service.RecordHeartbeatAsync(new HeartbeatRequest
        {
            DeviceId = device.Id,
            BatteryLevel = 55,
            Latitude = 12.9352,
            Longitude = 77.6245
        });

        Assert.NotNull(result);
        Assert.Equal(55, result!.BatteryLevel);
        Assert.Equal("Online", result.Status);
        Assert.Equal(12.9352, result.Latitude);
        Assert.Equal(77.6245, result.Longitude);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, result.LastSeen);
        Assert.Single(_notifier.DeviceUpdates);
    }

    [Fact]
    public async Task Heartbeat_below_threshold_creates_a_low_battery_alert()
    {
        var device = await SeedDeviceAsync(batteryLevel: 80);
        var service = CreateService();

        await service.RecordHeartbeatAsync(new HeartbeatRequest
        {
            DeviceId = device.Id,
            BatteryLevel = 9,
            Latitude = 12.97,
            Longitude = 77.59
        });

        var alert = await _db.Alerts.SingleAsync();
        Assert.Equal(device.Id, alert.DeviceId);
        Assert.Contains("9%", alert.Message);
        Assert.Equal(StartTime.UtcDateTime, alert.CreatedAt);
        Assert.Single(_notifier.Alerts);
    }

    [Fact]
    public async Task Heartbeat_above_threshold_does_not_create_an_alert()
    {
        var device = await SeedDeviceAsync(batteryLevel: 80);
        var service = CreateService();

        await service.RecordHeartbeatAsync(new HeartbeatRequest
        {
            DeviceId = device.Id,
            BatteryLevel = 42,
            Latitude = 12.97,
            Longitude = 77.59
        });

        Assert.Empty(_db.Alerts);
        Assert.Empty(_notifier.Alerts);
    }

    [Fact]
    public async Task Repeated_low_battery_heartbeats_do_not_duplicate_alerts()
    {
        var device = await SeedDeviceAsync(batteryLevel: 80);
        var service = CreateService();

        await RecordBatteryAsync(service, device.Id, 10);
        await RecordBatteryAsync(service, device.Id, 8);
        await RecordBatteryAsync(service, device.Id, 6);

        Assert.Single(_db.Alerts);
        Assert.Single(_notifier.Alerts);
    }

    [Fact]
    public async Task Low_battery_alerts_fire_again_after_the_device_recovers()
    {
        var device = await SeedDeviceAsync(batteryLevel: 80);
        var service = CreateService();

        await RecordBatteryAsync(service, device.Id, 10);
        await RecordBatteryAsync(service, device.Id, 90);
        await RecordBatteryAsync(service, device.Id, 12);

        Assert.Equal(2, await _db.Alerts.CountAsync());
        Assert.Equal(2, _notifier.Alerts.Count);
    }

    [Fact]
    public async Task Heartbeat_for_an_unknown_device_returns_null()
    {
        var service = CreateService();

        var result = await service.RecordHeartbeatAsync(new HeartbeatRequest
        {
            DeviceId = 999,
            BatteryLevel = 50,
            Latitude = 12.97,
            Longitude = 77.59
        });

        Assert.Null(result);
        Assert.Empty(_notifier.DeviceUpdates);
    }

    [Fact]
    public async Task Device_list_is_paginated_and_stably_ordered()
    {
        for (var i = 1; i <= 5; i++)
        {
            await SeedDeviceAsync(80, name: $"Device {i}");
        }

        var service = CreateService();

        var firstPage = await service.GetDevicesAsync(null, null, page: 1, pageSize: 2);
        Assert.Equal(5, firstPage.TotalCount);
        Assert.Equal(2, firstPage.PageSize);
        Assert.Equal(new[] { "Device 1", "Device 2" }, firstPage.Items.Select(d => d.Name));

        var lastPage = await service.GetDevicesAsync(null, null, page: 3, pageSize: 2);
        Assert.Single(lastPage.Items);
        Assert.Equal("Device 5", lastPage.Items[0].Name);
    }

    [Fact]
    public async Task Page_number_and_size_are_clamped()
    {
        await SeedDeviceAsync(80);
        var service = CreateService();

        var result = await service.GetDevicesAsync(null, null, page: -4, pageSize: 500);

        Assert.Equal(1, result.Page);
        Assert.Equal(100, result.PageSize);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task Lock_sets_the_flag_and_sends_a_command_to_the_device()
    {
        var device = await SeedDeviceAsync(batteryLevel: 80);
        var service = CreateService();

        var result = await service.SetLockAsync(device.Id, locked: true);

        Assert.NotNull(result);
        Assert.True(result!.IsLocked);
        Assert.True((await _db.Devices.SingleAsync()).IsLocked);
        Assert.Contains(_notifier.Commands, c => c.DeviceId == device.Id && c.Command == "lock");
    }

    [Fact]
    public async Task Locate_sends_a_command_to_the_device()
    {
        var device = await SeedDeviceAsync(batteryLevel: 80);
        var service = CreateService();

        var result = await service.RequestLocateAsync(device.Id);

        Assert.NotNull(result);
        Assert.Contains(_notifier.Commands, c => c.DeviceId == device.Id && c.Command == "locate");
    }

    [Fact]
    public async Task Summary_counts_online_offline_low_battery_and_platforms()
    {
        await SeedDeviceAsync(90, DeviceStatus.Online, Platform.Android);
        await SeedDeviceAsync(10, DeviceStatus.Online, Platform.iOS);
        await SeedDeviceAsync(50, DeviceStatus.Offline, Platform.Windows);
        var service = CreateService();

        var summary = await service.GetSummaryAsync();

        Assert.Equal(3, summary.Total);
        Assert.Equal(2, summary.Online);
        Assert.Equal(1, summary.Offline);
        Assert.Equal(1, summary.LowBattery);
        Assert.Equal(1, summary.Platforms.Single(p => p.Platform == "Android").Count);
        Assert.Equal(1, summary.Platforms.Single(p => p.Platform == "iOS").Count);
        Assert.Equal(1, summary.Platforms.Single(p => p.Platform == "Windows").Count);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private DeviceService CreateService() => new(_db, _notifier, _time);

    private Task RecordBatteryAsync(DeviceService service, int deviceId, int batteryLevel) =>
        service.RecordHeartbeatAsync(new HeartbeatRequest
        {
            DeviceId = deviceId,
            BatteryLevel = batteryLevel,
            Latitude = 12.97,
            Longitude = 77.59
        });

    private async Task<Device> SeedDeviceAsync(
        int batteryLevel,
        DeviceStatus status = DeviceStatus.Online,
        Platform platform = Platform.Android,
        string name = "Test device")
    {
        var device = new Device
        {
            Name = name,
            Platform = platform,
            OsVersion = "Test OS",
            BatteryLevel = batteryLevel,
            Latitude = 12.9716,
            Longitude = 77.5946,
            LastSeen = _time.GetUtcNow().UtcDateTime,
            Status = status
        };

        _db.Devices.Add(device);
        await _db.SaveChangesAsync();
        return device;
    }

}
