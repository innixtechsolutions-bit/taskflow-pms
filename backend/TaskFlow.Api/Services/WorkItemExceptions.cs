namespace TaskFlow.Api.Services;

public class InvalidWorkItemTypeException() : Exception("Type must be one of Epic, Story, Task, or SubTask.");

public class InvalidWorkItemPriorityException() : Exception("Priority must be one of Low, Medium, High, or Critical.");

public class InvalidWorkItemStatusException() : Exception("Status must be one of ToDo, InProgress, or Done.");

public class AssigneeNotFoundException() : Exception("Assignee must be an existing user.");

public class WorkItemNotFoundException() : Exception("Work item not found.");

// Broader than delete (see NotAuthorizedToDeleteWorkItemException) — edit also allows
// the current assignee, not just the creator or a Manager/Admin (FR-016).
public class NotAuthorizedToEditWorkItemException() : Exception(
    "You do not have permission to edit this work item.");

// Narrower than edit: the current assignee alone cannot delete, only the creator or a
// Manager/Admin (FR-017/FR-018).
public class NotAuthorizedToDeleteWorkItemException() : Exception(
    "You do not have permission to delete this work item.");

// Hierarchy rule violations (data-model.md's Hierarchy rules table). Each names the
// specific rule broken, per FR-009's "error response that names the specific rule
// violated" requirement.
public class EpicCannotHaveParentException() : Exception("An Epic cannot have a parent.");

public class ParentRequiredException(Data.Entities.WorkItemType type) : Exception($"{type} requires a parent.");

public class InvalidParentTypeException(Data.Entities.WorkItemType childType, Data.Entities.WorkItemType requiredParentType)
    : Exception($"{childType}'s parent must be a {requiredParentType}.");

public class ParentWorkItemNotFoundException() : Exception("Parent work item not found.");

public class ParentMustBeSameProjectException() : Exception("Parent must belong to the same project.");
