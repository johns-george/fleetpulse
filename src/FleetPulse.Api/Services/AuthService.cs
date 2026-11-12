using FleetPulse.Api.Auth;
using FleetPulse.Api.DTOs;
using Microsoft.Extensions.Options;

namespace FleetPulse.Api.Services;

public interface IAuthService
{
    LoginResponse? Login(LoginRequest request);
}

public class AuthService : IAuthService
{
    private readonly AdminOptions _admin;
    private readonly ITokenService _tokenService;

    public AuthService(IOptions<AdminOptions> admin, ITokenService tokenService)
    {
        _admin = admin.Value;
        _tokenService = tokenService;
    }

    public LoginResponse? Login(LoginRequest request)
    {
        var usernameMatches = string.Equals(request.Username?.Trim(), _admin.Username, StringComparison.OrdinalIgnoreCase);
        var passwordMatches = request.Password == _admin.Password;

        if (!usernameMatches || !passwordMatches)
        {
            return null;
        }

        var (token, expiresAt) = _tokenService.CreateToken(_admin.Username);
        return new LoginResponse(token, _admin.Username, expiresAt);
    }
}
