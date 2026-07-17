namespace TaskFlow.Api.Services;

public class InvalidWorkItemTypeException() : Exception("Type must be one of Epic, Story, Task, or SubTask.");

public class InvalidWorkItemPriorityException() : Exception("Priority must be one of Low, Medium, High, or Critical.");

public class InvalidWorkItemStatusException() : Exception("Status must be one of ToDo, InProgress, or Done.");

public class AssigneeNotFoundException() : Exception("Assignee must be an existing user.");
