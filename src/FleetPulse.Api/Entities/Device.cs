namespace FleetPulse.Api.Entities;

public enum Platform
{
    Android,
    iOS,
    Windows
}

public enum DeviceStatus
{
    Online,
    Offline
}

public class Device
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Platform Platform { get; set; }
    public string OsVersion { get; set; } = string.Empty;
    public int BatteryLevel { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsLocked { get; set; }
    public DateTime LastSeen { get; set; }
    public DeviceStatus Status { get; set; } = DeviceStatus.Offline;

    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
}
