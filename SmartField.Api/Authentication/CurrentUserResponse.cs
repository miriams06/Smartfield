namespace SmartField.Api.Authentication;

public sealed record CurrentUserResponse(
    Guid UserId,
    string Email,
    Guid CompanyId,
    Guid? EmployeeId,
    IReadOnlyCollection<string> Roles);
