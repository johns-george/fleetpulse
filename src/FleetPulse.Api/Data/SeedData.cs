using FleetPulse.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetPulse.Api.Data;

public static class SeedData
{
    public static async Task InitializeAsync(FleetPulseDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Devices.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;

        var devices = new List<Device>
        {
            new() { Name = "Pixel 7 - Field Ops", Platform = Platform.Android, OsVersion = "Android 14", BatteryLevel = 82, Latitude = 12.9716, Longitude = 77.5946, LastSeen = now, Status = DeviceStatus.Online },
            new() { Name = "Galaxy Tab - Warehouse", Platform = Platform.Android, OsVersion = "Android 13", BatteryLevel = 54, Latitude = 12.9352, Longitude = 77.6245, LastSeen = now, Status = DeviceStatus.Online },
            new() { Name = "iPhone 13 - Sales", Platform = Platform.iOS, OsVersion = "iOS 17.2", BatteryLevel = 96, Latitude = 12.9784, Longitude = 77.6408, LastSeen = now, Status = DeviceStatus.Online },
            new() { Name = "iPhone SE - Delivery", Platform = Platform.iOS, OsVersion = "iOS 16.6", BatteryLevel = 12, Latitude = 12.9539, Longitude = 77.5806, LastSeen = now, Status = DeviceStatus.Online },
            new() { Name = "Surface Go - Reception", Platform = Platform.Windows, OsVersion = "Windows 11", BatteryLevel = 67, Latitude = 12.9698, Longitude = 77.6003, LastSeen = now, Status = DeviceStatus.Online },
            new() { Name = "ThinkPad - Finance", Platform = Platform.Windows, OsVersion = "Windows 10", BatteryLevel = 41, Latitude = 12.9850, Longitude = 77.5690, LastSeen = now, Status = DeviceStatus.Online },
            new() { Name = "Pixel 6a - Support", Platform = Platform.Android, OsVersion = "Android 14", BatteryLevel = 8, Latitude = 12.9279, Longitude = 77.6271, LastSeen = now.AddSeconds(-12), Status = DeviceStatus.Online, IsLocked = true },
            new() { Name = "iPad Mini - Audit", Platform = Platform.iOS, OsVersion = "iPadOS 17.2", BatteryLevel = 73, Latitude = 12.9611, Longitude = 77.6387, LastSeen = now, Status = DeviceStatus.Online },
            new() { Name = "Dell Latitude - HR", Platform = Platform.Windows, OsVersion = "Windows 11", BatteryLevel = 23, Latitude = 13.0034, Longitude = 77.5709, LastSeen = now.AddMinutes(-4), Status = DeviceStatus.Offline },
            new() { Name = "Pixel 4a - Security", Platform = Platform.Android, OsVersion = "Android 12", BatteryLevel = 35, Latitude = 12.9165, Longitude = 77.6101, LastSeen = now.AddMinutes(-11), Status = DeviceStatus.Offline }
        };

        db.Devices.AddRange(devices);
        await db.SaveChangesAsync(cancellationToken);

        db.Alerts.AddRange(
            new Alert
            {
                DeviceId = devices[3].Id,
                Message = $"Low battery: {devices[3].Name} is at {devices[3].BatteryLevel}%.",
                CreatedAt = now.AddMinutes(-6)
            },
            new Alert
            {
                DeviceId = devices[6].Id,
                Message = $"Low battery: {devices[6].Name} is at {devices[6].BatteryLevel}%.",
                CreatedAt = now.AddMinutes(-2)
            });

        await db.SaveChangesAsync(cancellationToken);
    }
}
