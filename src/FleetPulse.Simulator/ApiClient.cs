using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FleetPulse.Simulator;

/// <summary>
/// Minimal typed HTTP client for the FleetPulse API. Devices enroll with the
/// shared enrollment key and then authenticate with their own device token.
/// </summary>
public sealed class ApiClient : IDisposable
{
    public const string EnrollmentKeyHeader = "X-Enrollment-Key";

    private readonly HttpClient _http;

    public ApiClient(string baseUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
    }

    public async Task<IReadOnlyList<SimulatedDevice>> GetDevicesForEnrollmentAsync(
        string enrollmentKey,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/enrollment/devices");
        request.Headers.Add(EnrollmentKeyHeader, enrollmentKey);

        using var response = await _http.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException(
                "The API rejected the enrollment key. Check FLEETPULSE_ENROLLMENT_KEY and Enrollment:Key.");
        }

        response.EnsureSuccessStatusCode();

        var devices = await response.Content.ReadFromJsonAsync<List<EnrollmentDevice>>(
                          cancellationToken: cancellationToken)
                      ?? new List<EnrollmentDevice>();

        return devices
            .Select(d => new SimulatedDevice(d.Id, d.Name, d.BatteryLevel, d.Latitude, d.Longitude))
            .ToList();
    }

    public async Task<string?> EnrollAsync(int deviceId, string enrollmentKey, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(
            "api/enrollment",
            new { deviceId, enrollmentKey },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var enrollment = await response.Content.ReadFromJsonAsync<EnrollmentResponse>(
            cancellationToken: cancellationToken);

        return enrollment?.Token;
    }

    public async Task SendHeartbeatAsync(
        HeartbeatPayload payload,
        string deviceToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/devices/heartbeat")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deviceToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() => _http.Dispose();

    private sealed record EnrollmentResponse(string Token);

    private sealed record EnrollmentDevice(
        int Id,
        string Name,
        int BatteryLevel,
        double Latitude,
        double Longitude);
}
