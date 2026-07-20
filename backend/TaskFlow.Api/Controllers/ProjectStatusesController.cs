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
}
