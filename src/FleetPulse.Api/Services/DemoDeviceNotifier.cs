using FleetPulse.Api.DTOs;

namespace FleetPulse.Api.Services;

/// <summary>
/// Demo-only decorator: after a command is sent, publish an acknowledgement about
/// a second later so the dashboard shows "confirmed by device" without a real device.
/// </summary>
public sealed class DemoDeviceNotifier : IDeviceNotifier
{
    private static readonly TimeSpan DefaultAckDelay = TimeSpan.FromSeconds(1);

    private readonly IDeviceNotifier _inner;
    private readonly TimeProvider _time;
    private readonly ILogger<DemoDeviceNotifier> _logger;
    private readonly TimeSpan _ackDelay;

    public DemoDeviceNotifier(
        IDeviceNotifier inner,
        TimeProvider time,
        ILogger<DemoDeviceNotifier> logger,
        TimeSpan? ackDelay = null)
    {
        _inner = inner;
        _time = time;
        _logger = logger;
        _ackDelay = ackDelay ?? DefaultAckDelay;
    }

    public Task DeviceUpdatedAsync(DeviceDto device) => _inner.DeviceUpdatedAsync(device);

    public Task AlertCreatedAsync(AlertDto alert) => _inner.AlertCreatedAsync(alert);

    public Task CommandAcknowledgedAsync(CommandAcknowledgementDto acknowledgement) =>
        _inner.CommandAcknowledgedAsync(acknowledgement);

    public Task SendCommandAsync(int deviceId, string command, string? workerName = null)
    {
        var sending = _inner.SendCommandAsync(deviceId, command, workerName);

        // Fire-and-forget on purpose: the HTTP response should not wait for the ack.
        _ = AcknowledgeLaterAsync(deviceId, command);

        return sending;
    }

    private async Task AcknowledgeLaterAsync(int deviceId, string command)
    {
        try
        {
            await Task.Delay(_ackDelay);

            await _inner.CommandAcknowledgedAsync(new CommandAcknowledgementDto(
                deviceId,
                command,
                _time.GetUtcNow().UtcDateTime));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Demo acknowledgement for command {Command} failed", command);
        }
    }
}
