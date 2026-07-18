using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Services;

namespace TaskFlow.Api.Controllers;

// [Authorize] at the class level sets the baseline — any authenticated user — since
// GetLookup (Feature 002) is intentionally open to every role. GetUsers/ChangeRole each
// layer their own [Authorize(Roles = "Admin")] on top (FR-017 from Feature 001):
// enforced server-side regardless of how the request is made, since client-side route
// guarding is a UX nicety only (FR-021). A non-Admin caller with a valid token still
// gets 403, not 401, from those two: they're authenticated, just not authorized.
[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(UserService userService) : ControllerBase
{
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserListItemDto>>> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await userService.GetUsersAsync(page, pageSize);
        return Ok(result);
    }

    [Authorize(Roles = "Admin")]
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

    // Deliberately open to any authenticated role, unlike GetUsers/ChangeRole above —
    // backs the work-item assignee picker (Feature 002, research.md §9), which any
    // signed-in user needs regardless of their own role.
    [HttpGet("lookup")]
    public async Task<ActionResult<List<UserLookupItemDto>>> GetLookup()
    {
        var result = await userService.GetAssignableUsersAsync();
        return Ok(result);
    }
}
