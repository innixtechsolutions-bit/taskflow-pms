namespace TaskFlow.Api.Dtos;

// The Kanban board's response shape (Feature 005). Columns come from the calling
// project's own ordered WorkflowStatus list (Feature 006) -- the frontend renders
// column headers purely from this array and never derives one itself.
public record WorkItemBoardDto(
    List<BoardColumnDto> Columns,
    List<WorkItemBoardCardDto> Items);

public record BoardColumnDto(int StatusId, string Name, string Category, string ColorKey);
