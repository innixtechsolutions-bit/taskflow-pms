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
}
