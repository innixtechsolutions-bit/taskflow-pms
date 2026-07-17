namespace TaskFlow.Api.Services;

public class DuplicateProjectNameException() : Exception("A project with this name already exists.");

public class ProjectNotFoundException() : Exception("Project not found.");
