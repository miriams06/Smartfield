using SmartField.Domain.Enums;

namespace SmartField.Application.Projects;

public sealed record ProjectDto(
    Guid Id,
    string Code,
    string Name,
    string ProjectType,
    string Status,
    string? CustomerName,
    Guid? WorkSiteId,
    string? WorkSiteName,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? ErpProjectCode,
    string? ErpCostCenterCode,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record CreateProjectRequest(
    string? Code,
    string? Name,
    string? ProjectType,
    string? Status,
    string? CustomerName,
    Guid? WorkSiteId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? ErpProjectCode,
    string? ErpCostCenterCode);

public sealed record UpdateProjectRequest(
    string? Code,
    string? Name,
    string? ProjectType,
    string? Status,
    string? CustomerName,
    Guid? WorkSiteId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? ErpProjectCode,
    string? ErpCostCenterCode);

public enum ProjectError
{
    None = 0,
    CompanyUnavailable = 1,
    Validation = 2,
    NotFound = 3,
    CodeConflict = 4,
    WorkSiteNotFound = 5
}

public sealed record ProjectResult<T>(
    T? Value,
    ProjectError Error,
    IReadOnlyDictionary<string, string[]> ValidationErrors)
    where T : class
{
    public bool IsSuccess => Error == ProjectError.None;

    public static ProjectResult<T> Success(T value)
    {
        return new ProjectResult<T>(
            value,
            ProjectError.None,
            new Dictionary<string, string[]>());
    }

    public static ProjectResult<T> Failure(ProjectError error)
    {
        return new ProjectResult<T>(
            null,
            error,
            new Dictionary<string, string[]>());
    }

    public static ProjectResult<T> Invalid(
        IReadOnlyDictionary<string, string[]> validationErrors)
    {
        return new ProjectResult<T>(
            null,
            ProjectError.Validation,
            validationErrors);
    }
}

public sealed class ProjectCodeConflictException : Exception
{
    public ProjectCodeConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

internal sealed record NormalizedProjectInput(
    string Code,
    string Name,
    ProjectType ProjectType,
    ProjectStatus Status,
    string? CustomerName,
    Guid? WorkSiteId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? ErpProjectCode,
    string? ErpCostCenterCode);
