using FleetPulse.Api.DTOs;
using FleetPulse.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetPulse.Api.Controllers;

/// <summary>
/// Device enrollment. Both actions are protected by the shared enrollment key
/// rather than a user token, because a device has no token before enrolling.
/// </summary>
[ApiController]
[Route("api/enrollment")]
[AllowAnonymous]
public class EnrollmentController : ControllerBase
{
    public const string KeyHeader = "X-Enrollment-Key";

    private readonly IEnrollmentService _enrollment;

    public EnrollmentController(IEnrollmentService enrollment)
    {
        _enrollment = enrollment;
    }

    [HttpPost]
    public async Task<ActionResult<EnrollmentResponse>> Enroll([FromBody] EnrollmentRequest request)
    {
        if (!_enrollment.IsValidKey(request.EnrollmentKey))
        {
            return Unauthorized(new { message = "Invalid enrollment key." });
        }

        var device = await _enrollment.GetDeviceAsync(request.DeviceId);
        if (device is null)
        {
            return NotFound(new { message = $"Device {request.DeviceId} was not found." });
        }

        return Ok(_enrollment.IssueToken(device));
    }

    /// <summary>Device inventory for the simulator, keyed by header instead of a JWT.</summary>
    [HttpGet("devices")]
    public async Task<ActionResult<IReadOnlyList<EnrollmentDeviceDto>>> GetDevices(
        [FromHeader(Name = KeyHeader)] string? enrollmentKey)
    {
        if (!_enrollment.IsValidKey(enrollmentKey))
        {
            return Unauthorized(new { message = "Invalid enrollment key." });
        }

        return Ok(await _enrollment.GetDevicesAsync());
    }
}
