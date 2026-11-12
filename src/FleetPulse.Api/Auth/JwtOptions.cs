namespace FleetPulse.Api.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "FleetPulse.Api";
    public string Audience { get; set; } = "FleetPulse.Web";
    public int ExpiryMinutes { get; set; } = 480;
}

public class AdminOptions
{
    public const string SectionName = "Admin";

    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "Admin@123";
}
