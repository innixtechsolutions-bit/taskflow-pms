using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Services;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Authorize]
public class WorkItemsController(WorkItemService workItemService) : ControllerBase
{
    [HttpPost("api/projects/{projectId}/work-items")]
    public async Task<ActionResult<WorkItemDto>> Create(int projectId, WorkItemRequest request)
    {
        var callerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            var created = await workItemService.CreateAsync(callerId, projectId, request);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (ProjectNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (InvalidWorkItemTypeException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
        catch (InvalidWorkItemPriorityException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
        catch (InvalidWorkItemStatusException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
        catch (AssigneeNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
    }

    [HttpGet("api/projects/{projectId}/work-items")]
    public async Task<ActionResult<PagedResult<WorkItemDto>>> GetWorkItems(int projectId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var result = await workItemService.GetWorkItemsAsync(projectId, page, pageSize);
            return Ok(result);
        }
        catch (ProjectNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
    }

    [HttpGet("api/work-items/{id}")]
    public async Task<ActionResult<WorkItemDto>> Get(int id)
    {
        try
        {
            return Ok(await workItemService.GetByIdAsync(id));
        }
        catch (WorkItemNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
    }

    [HttpPut("api/work-items/{id}")]
    public async Task<ActionResult<WorkItemDto>> Update(int id, WorkItemRequest request)
    {
        var callerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var callerRole = User.FindFirstValue(ClaimTypes.Role)!;

        try
        {
            var updated = await workItemService.UpdateAsync(callerId, callerRole, id, request);
            return Ok(updated);
        }
        catch (WorkItemNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (NotAuthorizedToEditWorkItemException ex)
        {
            return Problem(statusCode: StatusCodes.Status403Forbidden, detail: ex.Message);
        }
        catch (InvalidWorkItemTypeException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
        catch (InvalidWorkItemPriorityException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
        catch (InvalidWorkItemStatusException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
        catch (AssigneeNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
    }

    [HttpDelete("api/work-items/{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var callerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var callerRole = User.FindFirstValue(ClaimTypes.Role)!;

        try
        {
            await workItemService.DeleteAsync(callerId, callerRole, id);
            return NoContent();
        }
        catch (WorkItemNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
        }
        catch (NotAuthorizedToDeleteWorkItemException ex)
        {
            return Problem(statusCode: StatusCodes.Status403Forbidden, detail: ex.Message);
        }
    }
}
