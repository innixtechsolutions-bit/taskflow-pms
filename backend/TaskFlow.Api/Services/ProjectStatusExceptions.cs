namespace TaskFlow.Api.Services;

// Length re-checked in the service itself (not just via the request DTO's
// [StringLength] annotation), so CreateAsync/UpdateAsync behave identically
// whether called through HTTP model binding or directly (analyze-triage U1).
public class InvalidStatusNameException() : Exception("Name must be between 2 and 30 characters.");

public class InvalidStatusCategoryException() : Exception("Category must be one of Open or Done.");

public class InvalidStatusColorException() : Exception("Color must be one of the fixed ChipColor set.");

public class DuplicateStatusNameException() : Exception(
    "A status with this name already exists in this project.");

public class MaxStatusCountExceededException() : Exception(
    "A project cannot have more than 10 statuses.");

public class WorkflowStatusNotFoundException() : Exception(
    "Status not found, or it does not belong to this project.");

public class InvalidStatusOrderException() : Exception(
    "The provided order must contain exactly this project's current statuses, with no missing, unknown, or duplicate ids.");
