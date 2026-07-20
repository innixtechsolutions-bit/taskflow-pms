using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Services;

namespace TaskFlow.Api.Controllers;

// Class-level [Authorize] only — reading a project's own statuses is open to any
// authenticated user (the board, dropdowns, and filters all depend on it, FR-020),
// unlike the mutating actions (US3-US6), which each layer their own
// [Authorize(Roles = "Manager,Admin")] on top, matching ProjectsController's
// established pattern (spec.md FR-008, clarified during /speckit-analyze triage).
[ApiController]
[Route("api/projects/{projectId}/statuses")]
[Authorize]
public class ProjectStatusesController(ProjectStatusService projectStatusService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<WorkflowStatusDto>>> GetStatuses(int projectId)
    {
        try
        {
            return Ok(await projectStatusService.GetStatusesAsync(projectId));
        }
        catch (ProjectNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
    }

    [HttpPost]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<ActionResult<WorkflowStatusDto>> CreateStatus(int projectId, CreateWorkflowStatusRequest request)
    {
        try
        {
            var created = await projectStatusService.CreateAsync(projectId, request);
            return CreatedAtAction(nameof(GetStatuses), new { projectId }, created);
        }
        catch (ProjectNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (InvalidStatusNameException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
        catch (InvalidStatusCategoryException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
        catch (DuplicateStatusNameException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }
        catch (MaxStatusCountExceededException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }
    }

    // The {statusId:int} constraint (rather than a bare {statusId}) keeps this route
    // from ever matching literal-segment routes like PUT .../statuses/reorder below --
    // ASP.NET Core route precedence already favors literal segments, but the explicit
    // constraint makes the two routes unambiguous regardless.
    [HttpPut("{statusId:int}")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<ActionResult<WorkflowStatusDto>> UpdateStatus(int projectId, int statusId, UpdateWorkflowStatusRequest request)
    {
        try
        {
            return Ok(await projectStatusService.UpdateAsync(projectId, statusId, request));
        }
        catch (ProjectNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (WorkflowStatusNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (InvalidStatusNameException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
        catch (InvalidStatusColorException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
        catch (DuplicateStatusNameException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }
    }

    [HttpPut("reorder")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<ActionResult<List<WorkflowStatusDto>>> Reorder(int projectId, ReorderWorkflowStatusesRequest request)
    {
        try
        {
            return Ok(await projectStatusService.ReorderAsync(projectId, request));
        }
        catch (ProjectNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (InvalidStatusOrderException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
    }

    [HttpDelete("{statusId:int}")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> DeleteStatus(int projectId, int statusId, [FromQuery] int? destinationStatusId)
    {
        try
        {
            await projectStatusService.DeleteAsync(projectId, statusId, destinationStatusId);
            return NoContent();
        }
        catch (ProjectNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (WorkflowStatusNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (LastStatusInCategoryException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
        catch (InvalidDestinationStatusException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
        catch (DestinationStatusRequiredException ex)
        {
            // The client needs the current item count to render "Move N items..."
            // (contracts/workflow-api.md) -- Problem() alone can't carry it, so the
            // ProblemDetails.Extensions bag is populated directly.
            var problemDetails = new ProblemDetails { Status = StatusCodes.Status400BadRequest, Detail = ex.Message };
            problemDetails.Extensions["itemCount"] = ex.ItemCount;
            return new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status400BadRequest };
        }
    }
}
