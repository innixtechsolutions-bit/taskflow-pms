namespace TaskFlow.Api.Dtos;

// Recursive — Children nests further WorkItemTreeNodeDto instances. Returned whole,
// unpaginated, by GET .../work-items/tree: a tree's shape doesn't compose with
// pagination, and at this feature's internal-tool scale returning the whole project
// in one response is simpler than inventing tree-aware paging (research.md §4).
public record WorkItemTreeNodeDto(
    int Id,
    string Type,
    string Title,
    string Status,
    string Priority,
    string? AssigneeName,
    int DirectChildrenCount,
    int DirectChildrenDoneCount,
    List<WorkItemTreeNodeDto> Children);
