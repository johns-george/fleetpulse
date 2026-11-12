namespace FleetPulse.Simulator;

/// <summary>
/// In-memory state for one simulated device: battery drains slowly and the
/// reported coordinates drift a little on every heartbeat.
/// </summary>
public sealed class SimulatedDevice
{
    private readonly Random _random = new();
    private double _battery;
    private double _latitude;
    private double _longitude;

    public SimulatedDevice(int id, string name, int batteryLevel, double latitude, double longitude)
    {
        Id = id;
        Name = name;
        _battery = batteryLevel;
        _latitude = latitude;
        _longitude = longitude;
    }

    public int Id { get; }
    public string Name { get; }
    public double Battery => _battery;

    public HeartbeatPayload NextHeartbeat()
    {
        _battery -= 0.2 + _random.NextDouble() * 0.6;

        if (_battery <= 1)
        {
            // Simulated recharge so a long-running demo keeps producing heartbeats.
            _battery = 100;
        }

        _latitude += (_random.NextDouble() - 0.5) * 0.0008;
        _longitude += (_random.NextDouble() - 0.5) * 0.0008;

        return new HeartbeatPayload(
            Id,
            (int)Math.Round(_battery),
            Math.Round(_latitude, 6),
            Math.Round(_longitude, 6));
    }
}

public record HeartbeatPayload(int DeviceId, int BatteryLevel, double Latitude, double Longitude);
