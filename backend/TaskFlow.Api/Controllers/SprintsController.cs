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

    [HttpPut("{sprintId:int}/start")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<ActionResult<SprintDto>> StartSprint(int projectId, int sprintId)
    {
        try
        {
            return Ok(await sprintService.StartAsync(projectId, sprintId));
        }
        catch (ProjectNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (SprintNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (SprintNotPlannedException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
        catch (EmptySprintException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
        catch (AnotherSprintActiveException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }
    }

    [HttpPut("{sprintId:int}/complete")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<ActionResult<SprintDto>> CompleteSprint(int projectId, int sprintId, CompleteSprintRequest request)
    {
        try
        {
            return Ok(await sprintService.CompleteAsync(projectId, sprintId, request));
        }
        catch (ProjectNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (SprintNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (SprintNotActiveException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
        catch (InvalidDestinationSprintException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
        catch (DestinationRequiredException ex)
        {
            // The client needs the not-Done item count to render "Move N items..."
            // (contracts/sprints-api.md) -- Problem() alone can't carry it, same
            // ProblemDetails.Extensions pattern as Feature 006's
            // DestinationStatusRequiredException.
            var problemDetails = new ProblemDetails { Status = StatusCodes.Status400BadRequest, Detail = ex.Message };
            problemDetails.Extensions["itemCount"] = ex.ItemCount;
            return new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status400BadRequest };
        }
    }

    [HttpDelete("{sprintId:int}")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> DeleteSprint(int projectId, int sprintId)
    {
        try
        {
            await sprintService.DeleteAsync(projectId, sprintId);
            return NoContent();
        }
        catch (ProjectNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (SprintNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (SprintNotDeletableException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
    }
}
