using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Services;

namespace TaskFlow.Api.Controllers;

// Every action here requires the Admin role (FR-017) — enforced server-side regardless
// of how the request is made, since client-side route guarding is a UX nicety only
// (FR-021). A non-Admin caller with a valid token still gets 403, not 401: they're
// authenticated, just not authorized for this resource.
[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController(UserService userService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserListItemDto>>> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await userService.GetUsersAsync(page, pageSize);
        return Ok(result);
    }

    [HttpPut("{id}/role")]
    public async Task<ActionResult<UserListItemDto>> ChangeRole(int id, ChangeRoleRequest request)
    {
        var callerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            var updated = await userService.ChangeRoleAsync(callerId, id, request.Role);
            return Ok(updated);
        }
        catch (UserNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (LastAdminException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
        catch (InvalidRoleException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
    }
}
