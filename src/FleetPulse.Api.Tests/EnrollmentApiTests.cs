using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FleetPulse.Api.Tests;

public sealed class FleetPulseApiFactory : WebApplicationFactory<Program>
{
    public const string TestEnrollmentKey = "test-enrollment-key";

    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"fleetpulse-api-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
        builder.UseSetting("Enrollment:Key", TestEnrollmentKey);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        foreach (var path in new[] { _dbPath, $"{_dbPath}-shm", $"{_dbPath}-wal" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}

public class EnrollmentApiTests : IClassFixture<FleetPulseApiFactory>
{
    private const string ValidKey = FleetPulseApiFactory.TestEnrollmentKey;

    private readonly HttpClient _client;

    public EnrollmentApiTests(FleetPulseApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Enrollment_with_a_wrong_key_is_rejected()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/enrollment",
            new { deviceId = 1, enrollmentKey = "not-the-key" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Enrollment_for_an_unknown_device_returns_not_found()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/enrollment",
            new { deviceId = 99999, enrollmentKey = ValidKey });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrollment_returns_a_device_token_with_role_and_device_claim()
    {
        var token = await EnrollAsync(1);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Device");
        Assert.Contains(jwt.Claims, c => c.Type == "deviceId" && c.Value == "1");
    }

    [Fact]
    public async Task Heartbeat_without_a_token_is_unauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/devices/heartbeat", new
        {
            deviceId = 1,
            batteryLevel = 50,
            latitude = 12.97,
            longitude = 77.59
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Heartbeat_with_a_mismatched_device_id_is_forbidden()
    {
        var token = await EnrollAsync(1);

        var response = await SendHeartbeatAsync(token, deviceId: 2);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Heartbeat_with_the_matching_device_token_is_accepted()
    {
        var token = await EnrollAsync(1);

        var response = await SendHeartbeatAsync(token, deviceId: 1);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_device_token_cannot_read_the_admin_device_list()
    {
        var token = await EnrollAsync(1);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/devices");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<string> EnrollAsync(int deviceId)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/enrollment",
            new { deviceId, enrollmentKey = ValidKey });

        response.EnsureSuccessStatusCode();
        var enrollment = await response.Content.ReadFromJsonAsync<EnrollmentResponse>();
        Assert.NotNull(enrollment);

        return enrollment!.Token;
    }

    private Task<HttpResponseMessage> SendHeartbeatAsync(string token, int deviceId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/devices/heartbeat")
        {
            Content = JsonContent.Create(new
            {
                deviceId,
                batteryLevel = 50,
                latitude = 12.97,
                longitude = 77.59
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return _client.SendAsync(request);
    }

    private sealed record EnrollmentResponse(string Token);
}
