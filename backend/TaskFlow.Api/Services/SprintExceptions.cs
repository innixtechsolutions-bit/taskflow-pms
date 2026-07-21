namespace TaskFlow.Api.Services;

public class InvalidSprintNameException() : Exception("Sprint name must be 2–50 characters.");

public class DuplicateSprintNameException() : Exception("A sprint with this name already exists in this project.");

public class InvalidSprintDateRangeException() : Exception("End date must be after the start date.");

// Feature 008 US2 — thrown both when SprintService looks up a sprint by id that
// doesn't exist, and when WorkItemService.ResolveSprintIdAsync is given a sprintId
// that doesn't belong to the item's own project (identities aren't shared across
// projects, same reasoning as Feature 006's status resolution).
public class SprintNotFoundException() : Exception("Sprint not found.");

// Feature 008 US4 -- Start.
public class SprintNotPlannedException() : Exception("Only a Planned sprint can be started.");

public class EmptySprintException() : Exception("A sprint needs at least one item before it can be started.");

// Names the sprint that is already Active (FR-006) -- both the ordinary
// check-then-act path and the DB-constraint race-recovery path in
// SprintService.StartAsync throw this same type (research.md's C2 fix).
public class AnotherSprintActiveException(string activeSprintName) : Exception(
    $"\"{activeSprintName}\" is already active in this project.");

// Feature 008 US4 -- Complete.
public class SprintNotActiveException() : Exception("Only an Active sprint can be completed.");

// Carries the not-Done item count so the client can render "Move N items..."
// before the caller commits to a resolution -- Problem() alone can't carry it,
// so it travels via ProblemDetails.Extensions["itemCount"], the same pattern
// Feature 006's DestinationStatusRequiredException already established.
public class DestinationRequiredException(int itemCount) : Exception(
    "This sprint has not-Done items -- choose where they should go before completing it.")
{
    public int ItemCount { get; } = itemCount;
}

public class InvalidDestinationSprintException() : Exception(
    "Resolution must be \"Backlog\", or \"Sprint\" with a valid destinationSprintId (a different Planned or Active sprint in the same project).");

// Feature 008 US4 -- Delete.
public class SprintNotDeletableException() : Exception(
    "Only an empty, never-started Planned sprint can be deleted.");
