using FleetPulse.Api.DTOs;
using FleetPulse.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetPulse.Api.Controllers;

[ApiController]
[Route("api/devices")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly IDeviceService _devices;

    public DevicesController(IDeviceService devices)
    {
        _devices = devices;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeviceDto>>> GetDevices(
        [FromQuery] string? search,
        [FromQuery] string? status)
    {
        var devices = await _devices.GetDevicesAsync(search, status);
        return Ok(devices);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DeviceDetailDto>> GetDevice(int id)
    {
        var device = await _devices.GetDeviceAsync(id);
        return device is null
            ? NotFound(new { message = $"Device {id} was not found." })
            : Ok(device);
    }

    /// <summary>Called by the simulator; intentionally anonymous.</summary>
    [HttpPost("heartbeat")]
    [AllowAnonymous]
    public async Task<ActionResult<DeviceDto>> Heartbeat([FromBody] HeartbeatRequest request)
    {
        var device = await _devices.RecordHeartbeatAsync(request);
        return device is null
            ? NotFound(new { message = $"Device {request.DeviceId} was not found." })
            : Ok(device);
    }

    [HttpPost("{id:int}/lock")]
    public async Task<ActionResult<DeviceDto>> Lock(int id)
    {
        var device = await _devices.SetLockAsync(id, locked: true);
        return device is null
            ? NotFound(new { message = $"Device {id} was not found." })
            : Ok(device);
    }

    [HttpPost("{id:int}/locate")]
    public async Task<ActionResult<LocateResponse>> Locate(int id)
    {
        var device = await _devices.RequestLocateAsync(id);
        return device is null
            ? NotFound(new { message = $"Device {id} was not found." })
            : Ok(new LocateResponse($"Locate command sent to {device.Name}."));
    }
}
