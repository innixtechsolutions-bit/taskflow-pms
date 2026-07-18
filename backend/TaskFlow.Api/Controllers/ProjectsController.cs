using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Services;

namespace TaskFlow.Api.Controllers;

// Class-level [Authorize] sets the baseline — any authenticated user may view
// projects (FR-005) — and the create/edit/delete actions layer on a stricter
// [Authorize(Roles = ...)] of their own; ASP.NET Core ANDs attributes from both
// levels together, so those actions end up requiring both "authenticated" and
// "Manager or Admin."
[ApiController]
[Route("api/projects")]
[Authorize]
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

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProjectListItemDto>>> GetProjects([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await projectService.GetProjectsAsync(page, pageSize);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProjectDetailDto>> GetProject(int id)
    {
        try
        {
            var project = await projectService.GetProjectByIdAsync(id);
            return Ok(project);
        }
        catch (ProjectNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
    }

    [Authorize(Roles = "Manager,Admin")]
    [HttpPut("{id}")]
    public async Task<ActionResult<ProjectDetailDto>> Update(int id, ProjectRequest request)
    {
        try
        {
            var updated = await projectService.UpdateAsync(id, request);
            return Ok(updated);
        }
        catch (ProjectNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (DuplicateProjectNameException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }
    }

    [Authorize(Roles = "Manager,Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await projectService.DeleteAsync(id);
            return NoContent();
        }
        catch (ProjectNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
    }
}
