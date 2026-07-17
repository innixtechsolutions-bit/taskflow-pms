using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Services;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController(ProjectService projectService) : ControllerBase
{
    [Authorize(Roles = "Manager,Admin")]
    [HttpPost]
    public async Task<ActionResult<ProjectDetailDto>> Create(ProjectRequest request)
    {
        var callerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            var created = await projectService.CreateAsync(callerId, request);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (DuplicateProjectNameException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }
    }
}
