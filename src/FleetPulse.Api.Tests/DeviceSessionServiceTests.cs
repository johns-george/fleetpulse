using FleetPulse.Api.Data;
using FleetPulse.Api.Entities;
using FleetPulse.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FleetPulse.Api.Tests;

public class DeviceSessionServiceTests : IDisposable
{
    private static readonly DateTimeOffset StartTime = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly FleetPulseDbContext _db;
    private readonly RecordingNotifier _notifier = new();
    private readonly ManualTimeProvider _time = new(StartTime);

    public DeviceSessionServiceTests()
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
    public async Task Checkout_starts_a_session_and_exposes_the_current_worker()
    {
        var device = await SeedDeviceAsync();
        var service = CreateService();

        var result = await service.CheckoutAsync(device.Id, "  Alice  ");

        Assert.Equal(SessionOutcome.Success, result.Outcome);
        Assert.Equal("Alice", result.Device!.CurrentWorker);

        var session = await _db.DeviceSessions.SingleAsync();
        Assert.Equal(device.Id, session.DeviceId);
        Assert.Equal("Alice", session.WorkerName);
        Assert.Equal(StartTime.UtcDateTime, session.StartedAt);
        Assert.Null(session.EndedAt);
        Assert.Null(session.EndedBy);

        Assert.Contains(_notifier.Commands,
            c => c.DeviceId == device.Id && c.Command == "checkout" && c.WorkerName == "Alice");
    }

    [Fact]
    public async Task A_second_checkout_for_the_same_device_conflicts()
    {
        var device = await SeedDeviceAsync();
        var service = CreateService();

        await service.CheckoutAsync(device.Id, "Alice");
        var result = await service.CheckoutAsync(device.Id, "Bob");

        Assert.Equal(SessionOutcome.AlreadyCheckedOut, result.Outcome);
        Assert.Single(await _db.DeviceSessions.ToListAsync());
    }

    [Fact]
    public async Task Checkin_ends_the_session_as_the_worker()
    {
        var device = await SeedDeviceAsync();
        var service = CreateService();

        await service.CheckoutAsync(device.Id, "Alice");
        _time.Advance(TimeSpan.FromMinutes(5));

        var result = await service.CheckinAsync(device.Id);

        Assert.Equal(SessionOutcome.Success, result.Outcome);
        Assert.Null(result.Device!.CurrentWorker);

        var session = await _db.DeviceSessions.SingleAsync();
        Assert.Equal(StartTime.AddMinutes(5).UtcDateTime, session.EndedAt);
        Assert.Equal("worker", session.EndedBy);
        Assert.Contains(_notifier.Commands, c => c.Command == "checkin" && c.WorkerName == "Alice");
    }

    [Fact]
    public async Task Force_end_records_the_admin_and_sends_end_session_to_the_device()
    {
        var device = await SeedDeviceAsync();
        var service = CreateService();

        await service.CheckoutAsync(device.Id, "Alice");
        var result = await service.ForceEndAsync(device.Id, "admin");

        Assert.Equal(SessionOutcome.Success, result.Outcome);
        Assert.Null(result.Device!.CurrentWorker);

        var session = await _db.DeviceSessions.SingleAsync();
        Assert.NotNull(session.EndedAt);
        Assert.Equal("admin:admin", session.EndedBy);
        Assert.Contains(_notifier.Commands,
            c => c.Command == "end-session" && c.WorkerName == "Alice");
    }

    [Fact]
    public async Task Checkin_without_an_active_session_conflicts()
    {
        var device = await SeedDeviceAsync();
        var service = CreateService();

        var result = await service.CheckinAsync(device.Id);

        Assert.Equal(SessionOutcome.NoActiveSession, result.Outcome);
        Assert.Empty(_notifier.Commands);
    }

    [Fact]
    public async Task Sessions_for_an_unknown_device_return_null()
    {
        var service = CreateService();

        Assert.Null(await service.GetSessionsAsync(999));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private DeviceSessionService CreateService() => new(_db, _notifier, _time);

    private async Task<Device> SeedDeviceAsync()
    {
        var device = new Device
        {
            Name = "Shared tablet",
            Platform = Platform.Android,
            OsVersion = "Android 14",
            BatteryLevel = 75,
            Latitude = 12.9716,
            Longitude = 77.5946,
            LastSeen = StartTime.UtcDateTime,
            Status = DeviceStatus.Online
        };

        _db.Devices.Add(device);
        await _db.SaveChangesAsync();
        return device;
    }
}
