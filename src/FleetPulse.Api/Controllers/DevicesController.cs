using FleetPulse.Api.DTOs;
using FleetPulse.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetPulse.Api.Controllers;

[ApiController]
[Route("api/devices")]
[Authorize(Roles = "Admin")]
public class DevicesController : ControllerBase
{
    private readonly IDeviceService _devices;
    private readonly IDeviceSessionService _sessions;

    public DevicesController(IDeviceService devices, IDeviceSessionService sessions)
    {
        _devices = devices;
        _sessions = sessions;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDto<DeviceDto>>> GetDevices(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var devices = await _devices.GetDevicesAsync(search, status, page, pageSize);
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

    [HttpPost("{id:int}/checkout")]
    public async Task<ActionResult<DeviceDto>> Checkout(int id, [FromBody] CheckoutRequest request)
    {
        return ToActionResult(await _sessions.CheckoutAsync(id, request.WorkerName));
    }

    [HttpPost("{id:int}/checkin")]
    public async Task<ActionResult<DeviceDto>> Checkin(int id)
    {
        return ToActionResult(await _sessions.CheckinAsync(id));
    }

    [HttpPost("{id:int}/force-end-session")]
    public async Task<ActionResult<DeviceDto>> ForceEndSession(int id)
    {
        var admin = User.Identity?.Name ?? "unknown";
        return ToActionResult(await _sessions.ForceEndAsync(id, admin));
    }

    [HttpGet("{id:int}/sessions")]
    public async Task<ActionResult<IReadOnlyList<DeviceSessionDto>>> GetSessions(int id)
    {
        var sessions = await _sessions.GetSessionsAsync(id);
        return sessions is null
            ? NotFound(new { message = $"Device {id} was not found." })
            : Ok(sessions);
    }

    private ActionResult<DeviceDto> ToActionResult(SessionResult result) => result.Outcome switch
    {
        SessionOutcome.Success => Ok(result.Device),
        SessionOutcome.DeviceNotFound => NotFound(new { message = $"Device {result.DeviceId} was not found." }),
        _ => Conflict(new { message = result.Message })
    };
}
