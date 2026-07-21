using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Services;

namespace TaskFlow.Api.Controllers;

// Class-level [Authorize] only -- reading a project's own sprints is open to any
// authenticated user (the Backlog view and the Board's sprint-scoped mode both
// depend on it), matching ProjectStatusesController's established read/write split
// (Feature 006). Mutating actions each layer their own
// [Authorize(Roles = "Manager,Admin")] on top.
[ApiController]
[Route("api/projects/{projectId}/sprints")]
[Authorize]
public class SprintsController(SprintService sprintService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SprintDto>>> GetSprints(int projectId)
    {
        try
        {
            return Ok(await sprintService.GetSprintsAsync(projectId));
        }
        catch (ProjectNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
    }

    [HttpPost]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<ActionResult<SprintDto>> CreateSprint(int projectId, CreateSprintRequest request)
    {
        try
        {
            var created = await sprintService.CreateAsync(projectId, request);
            return CreatedAtAction(nameof(GetSprints), new { projectId }, created);
        }
        catch (ProjectNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (InvalidSprintNameException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
        catch (InvalidSprintDateRangeException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
        catch (DuplicateSprintNameException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }
    }
}
