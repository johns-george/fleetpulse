namespace FleetPulse.Api.Entities;

/// <summary>
/// One worker's use of a shared device. EndedAt stays null while the session is active.
/// </summary>
public class DeviceSession
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public string WorkerName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    /// <summary>"worker" for a normal check-in, "admin:&lt;username&gt;" when forced.</summary>
    public string? EndedBy { get; set; }

    public Device? Device { get; set; }
}
