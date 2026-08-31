namespace SmartField.Application.WorkSites;

public sealed record WorkSiteDto(
    Guid Id,
    string Code,
    string Name,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    int? GeofenceRadiusMeters,
    bool IsActive,
    string? ErpCostCenterCode,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record CreateWorkSiteRequest(
    string Code,
    string Name,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    int? GeofenceRadiusMeters,
    bool IsActive,
    string? ErpCostCenterCode);

public sealed record UpdateWorkSiteRequest(
    string Code,
    string Name,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    int? GeofenceRadiusMeters,
    bool IsActive,
    string? ErpCostCenterCode);

public enum WorkSiteError
{
    None = 0,
    CompanyUnavailable = 1,
    Validation = 2,
    NotFound = 3,
    CodeConflict = 4
}

public sealed record WorkSiteResult<T>(
    T? Value,
    WorkSiteError Error,
    IReadOnlyDictionary<string, string[]> ValidationErrors)
    where T : class
{
    public bool IsSuccess => Error == WorkSiteError.None;

    public static WorkSiteResult<T> Success(T value)
    {
        return new WorkSiteResult<T>(
            value,
            WorkSiteError.None,
            new Dictionary<string, string[]>());
    }

    public static WorkSiteResult<T> Failure(WorkSiteError error)
    {
        return new WorkSiteResult<T>(
            null,
            error,
            new Dictionary<string, string[]>());
    }

    public static WorkSiteResult<T> Invalid(
        IReadOnlyDictionary<string, string[]> validationErrors)
    {
        return new WorkSiteResult<T>(
            null,
            WorkSiteError.Validation,
            validationErrors);
    }
}

public sealed class WorkSiteCodeConflictException : Exception
{
    public WorkSiteCodeConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
