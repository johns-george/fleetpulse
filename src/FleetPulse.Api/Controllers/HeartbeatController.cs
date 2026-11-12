using FleetPulse.Api.DTOs;
using FleetPulse.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetPulse.Api.Controllers;

[ApiController]
[Route("api/devices")]
public class HeartbeatController : ControllerBase
{
    private readonly IDeviceService _devices;

    public HeartbeatController(IDeviceService devices)
    {
        _devices = devices;
    }

    /// <summary>Called by enrolled devices. The token's deviceId must match the body.</summary>
    [HttpPost("heartbeat")]
    [Authorize(Roles = "Device")]
    public async Task<ActionResult<DeviceDto>> Heartbeat([FromBody] HeartbeatRequest request)
    {
        var claim = User.FindFirst("deviceId")?.Value;
        if (!int.TryParse(claim, out var tokenDeviceId) || tokenDeviceId != request.DeviceId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "The device token does not match the heartbeat deviceId."
            });
        }

        var device = await _devices.RecordHeartbeatAsync(request);
        return device is null
            ? NotFound(new { message = $"Device {request.DeviceId} was not found." })
            : Ok(device);
    }
}
