using FleetPulse.Api.DTOs;
using FleetPulse.Api.Services;

namespace FleetPulse.Api.Tests;

internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public ManualTimeProvider(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}

internal sealed class RecordingNotifier : IDeviceNotifier
{
    public List<DeviceDto> DeviceUpdates { get; } = new();
    public List<AlertDto> Alerts { get; } = new();
    public List<DeviceCommand> Commands { get; } = new();
    public List<CommandAcknowledgementDto> Acknowledgements { get; } = new();

    public Task DeviceUpdatedAsync(DeviceDto device)
    {
        DeviceUpdates.Add(device);
        return Task.CompletedTask;
    }

    public Task AlertCreatedAsync(AlertDto alert)
    {
        Alerts.Add(alert);
        return Task.CompletedTask;
    }

    public Task SendCommandAsync(int deviceId, string command, string? workerName = null)
    {
        Commands.Add(new DeviceCommand(deviceId, command, workerName));
        return Task.CompletedTask;
    }

    public Task CommandAcknowledgedAsync(CommandAcknowledgementDto acknowledgement)
    {
        Acknowledgements.Add(acknowledgement);
        return Task.CompletedTask;
    }
}
