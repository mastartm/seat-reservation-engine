using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SeatReservation.Api.Infrastructure;
using SeatReservation.Api.Contracts;
using SeatReservation.Application.Auth;

namespace SeatReservation.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    /// <summary>Yeni kullanıcı (rol: User) oluşturur ve doğrudan oturum açtırır.</summary>
    [HttpPost("register")]
    [ProducesResponseType<AuthResult>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(request.Email, request.Password, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await auth.LoginAsync(request.Email, request.Password, ct));

    /// <summary>Demo modunda (Demo__Enabled=true) tek kullanımlık misafir hesabı açar ve giriş yaptırır; kapalıysa 404.</summary>
    [HttpPost("demo")]
    [EnableRateLimiting(Program.DemoRateLimitPolicy)]
    [ProducesResponseType<AuthResult>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Demo(CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await auth.CreateDemoSessionAsync(ct));

    /// <summary>Token'daki kimliği döner (Swagger'da "Authorize" sonrası denemek için).</summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() => Ok(new
    {
        userId = User.GetUserId(),
        email = User.FindFirst("email")?.Value,
        role = User.FindFirst("role")?.Value,
    });
}
