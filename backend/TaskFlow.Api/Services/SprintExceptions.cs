namespace TaskFlow.Api.Services;

public class InvalidSprintNameException() : Exception("Sprint name must be 2–50 characters.");

public class DuplicateSprintNameException() : Exception("A sprint with this name already exists in this project.");

public class InvalidSprintDateRangeException() : Exception("End date must be after the start date.");

// Feature 008 US2 — thrown both when SprintService looks up a sprint by id that
// doesn't exist, and when WorkItemService.ResolveSprintIdAsync is given a sprintId
// that doesn't belong to the item's own project (identities aren't shared across
// projects, same reasoning as Feature 006's status resolution).
public class SprintNotFoundException() : Exception("Sprint not found.");
