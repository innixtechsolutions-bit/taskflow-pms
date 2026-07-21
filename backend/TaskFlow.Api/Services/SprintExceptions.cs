namespace TaskFlow.Api.Services;

public class InvalidSprintNameException() : Exception("Sprint name must be 2–50 characters.");

public class DuplicateSprintNameException() : Exception("A sprint with this name already exists in this project.");

public class InvalidSprintDateRangeException() : Exception("End date must be after the start date.");
