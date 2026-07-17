using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Services;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    // [ApiController] runs RegisterRequest's data annotations before this method body
    // even executes, so an invalid name/email/password already produced a 400
    // ValidationProblemDetails response by the time we get here.
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        try
        {
            var response = await authService.RegisterAsync(request);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (EmailAlreadyExistsException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }
        catch (InvalidPasswordException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        try
        {
            var response = await authService.LoginAsync(request);
            return Ok(response);
        }
        catch (InvalidCredentialsException ex)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: ex.Message);
        }
        catch (TooManyAttemptsException ex)
        {
            return Problem(statusCode: StatusCodes.Status429TooManyRequests, detail: ex.Message);
        }
    }

    // Stateless JWTs have no server-side session to end, so this is a no-op besides the
    // [Authorize] check itself — actually discarding the token is the client's job (see
    // research.md's stateless-token trade-off).
    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout() => NoContent();

    [Authorize]
    [HttpGet("me")]
    public ActionResult<MeResponse> Me()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var fullName = User.FindFirstValue(ClaimTypes.Name)!;
        var role = User.FindFirstValue(ClaimTypes.Role)!;
        return Ok(new MeResponse(id, fullName, role));
    }
}
