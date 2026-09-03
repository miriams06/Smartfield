namespace SmartField.Application.Employees;

public sealed record EmployeeDto(
    Guid Id,
    string EmployeeNumber,
    string Name,
    string? Email,
    string? MobilePhone,
    bool IsActive,
    Guid? DefaultWorkSiteId,
    string? DefaultWorkSiteName,
    Guid? UserId,
    string? UserEmail,
    string? ErpEmployeeCode,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record EmployeeWorkSiteOption(
    Guid Id,
    string Code,
    string Name,
    bool IsActive);

public sealed record EmployeeUserOption(
    Guid Id,
    string Email,
    bool IsActive);

public sealed record EmployeeOptions(
    IReadOnlyList<EmployeeWorkSiteOption> WorkSites,
    IReadOnlyList<EmployeeUserOption> Users);

public sealed record CreateEmployeeRequest(
    string EmployeeNumber,
    string Name,
    string? Email,
    string? MobilePhone,
    bool IsActive,
    Guid? DefaultWorkSiteId,
    Guid? UserId,
    string? ErpEmployeeCode);

public sealed record UpdateEmployeeRequest(
    string EmployeeNumber,
    string Name,
    string? Email,
    string? MobilePhone,
    bool IsActive,
    Guid? DefaultWorkSiteId,
    Guid? UserId,
    string? ErpEmployeeCode);

public sealed record CreateEmployeeUserRequest(
    string Email,
    string Password);

public enum EmployeeError
{
    None = 0,
    CompanyUnavailable = 1,
    Validation = 2,
    NotFound = 3,
    EmployeeNumberConflict = 4,
    WorkSiteNotFound = 5,
    UserNotFound = 6,
    UserAlreadyAssigned = 7
}

public enum EmployeeUserAssociationStatus
{
    Success = 0,
    UserNotFound = 1,
    UserAlreadyAssigned = 2
}

public sealed record EmployeeResult<T>(
    T? Value,
    EmployeeError Error,
    IReadOnlyDictionary<string, string[]> ValidationErrors)
    where T : class
{
    public bool IsSuccess => Error == EmployeeError.None;

    public static EmployeeResult<T> Success(T value)
    {
        return new EmployeeResult<T>(
            value,
            EmployeeError.None,
            new Dictionary<string, string[]>());
    }

    public static EmployeeResult<T> Failure(EmployeeError error)
    {
        return new EmployeeResult<T>(
            null,
            error,
            new Dictionary<string, string[]>());
    }

    public static EmployeeResult<T> Invalid(
        IReadOnlyDictionary<string, string[]> validationErrors)
    {
        return new EmployeeResult<T>(
            null,
            EmployeeError.Validation,
            validationErrors);
    }
}

public sealed class EmployeeNumberConflictException : Exception
{
    public EmployeeNumberConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
