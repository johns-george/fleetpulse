using FleetPulse.Api.DTOs;
using FleetPulse.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetPulse.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDeviceService _devices;

    public DashboardController(IDeviceService devices)
    {
        _devices = devices;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary()
    {
        var summary = await _devices.GetSummaryAsync();
        return Ok(summary);
    }
}
