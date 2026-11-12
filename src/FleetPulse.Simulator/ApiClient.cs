using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FleetPulse.Simulator;

/// <summary>
/// Minimal typed HTTP client for the FleetPulse API.
/// </summary>
public sealed class ApiClient : IDisposable
{
    private readonly HttpClient _http;

    public ApiClient(string baseUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
    }

    public string? Token { get; private set; }

    public async Task<string?> LoginAsync(string username, string password, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(
            "api/auth/login",
            new { username, password },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellationToken);
        Token = login?.Token;

        if (Token is not null)
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        }

        return Token;
    }

    public async Task<IReadOnlyList<SimulatedDevice>> GetDevicesAsync(CancellationToken cancellationToken)
    {
        var devices = await _http.GetFromJsonAsync<List<DeviceDto>>("api/devices", cancellationToken)
                      ?? new List<DeviceDto>();

        return devices
            .Select(d => new SimulatedDevice(d.Id, d.Name, d.BatteryLevel, d.Latitude, d.Longitude))
            .ToList();
    }

    public async Task SendHeartbeatAsync(HeartbeatPayload payload, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync("api/devices/heartbeat", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() => _http.Dispose();

    private sealed record LoginResponse(string Token);

    private sealed record DeviceDto(int Id, string Name, int BatteryLevel, double Latitude, double Longitude);
}
