namespace TaskFlow.Api.Dtos;

// The Kanban board's response shape (Feature 005). Columns are {status, label}
// pairs computed server-side, not bare status strings -- the frontend renders
// column headers purely from this array and never derives a label itself, so
// a future per-project column list (custom-columns feature) is a pure backend
// change, never a board-component one (research.md #2, revised during triage).
public record WorkItemBoardDto(
    List<BoardColumnDto> Columns,
    List<WorkItemBoardCardDto> Items);

public record BoardColumnDto(string Status, string Label);
