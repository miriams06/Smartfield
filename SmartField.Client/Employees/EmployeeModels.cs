using System.ComponentModel.DataAnnotations;
using System.Net;

namespace SmartField.Client.Employees;

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
    string Password,
    string? Role);

public sealed class EmployeeEditorModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "O número de funcionário é obrigatório.")]
    [StringLength(50, ErrorMessage = "O número não pode exceder 50 caracteres.")]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "O nome é obrigatório.")]
    [StringLength(200, ErrorMessage = "O nome não pode exceder 200 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "O email não tem um formato válido.")]
    [StringLength(320, ErrorMessage = "O email não pode exceder 320 caracteres.")]
    public string? Email { get; set; }

    [StringLength(50, ErrorMessage = "O telefone não pode exceder 50 caracteres.")]
    public string? MobilePhone { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? DefaultWorkSiteId { get; set; }

    public Guid? UserId { get; set; }

    [StringLength(100, ErrorMessage = "O código ERP não pode exceder 100 caracteres.")]
    public string? ErpEmployeeCode { get; set; }

    public string? UserEmail { get; set; }

    public void Load(EmployeeDto employee)
    {
        Id = employee.Id;
        EmployeeNumber = employee.EmployeeNumber;
        Name = employee.Name;
        Email = employee.Email;
        MobilePhone = employee.MobilePhone;
        IsActive = employee.IsActive;
        DefaultWorkSiteId = employee.DefaultWorkSiteId;
        UserId = employee.UserId;
        UserEmail = employee.UserEmail;
        ErpEmployeeCode = employee.ErpEmployeeCode;
    }

    public CreateEmployeeRequest ToCreateRequest()
    {
        return new CreateEmployeeRequest(
            EmployeeNumber,
            Name,
            Email,
            MobilePhone,
            IsActive,
            DefaultWorkSiteId,
            UserId,
            ErpEmployeeCode);
    }

    public UpdateEmployeeRequest ToUpdateRequest()
    {
        return new UpdateEmployeeRequest(
            EmployeeNumber,
            Name,
            Email,
            MobilePhone,
            IsActive,
            DefaultWorkSiteId,
            UserId,
            ErpEmployeeCode);
    }
}

public sealed class EmployeeUserEditorModel
{
    [Required(ErrorMessage = "O email de login é obrigatório.")]
    [EmailAddress(ErrorMessage = "O email de login não tem um formato válido.")]
    [StringLength(320, ErrorMessage = "O email de login não pode exceder 320 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "A password é obrigatória.")]
    [MinLength(6, ErrorMessage = "A password deve ter pelo menos 6 caracteres.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "O perfil é obrigatório.")]
    public string Role { get; set; } = "Employee";

    public CreateEmployeeUserRequest ToRequest()
    {
        return new CreateEmployeeUserRequest(
            Email,
            Password,
            Role);
    }
}

public sealed record ApiProblemDetails(
    string? Title,
    string? Detail,
    Dictionary<string, string[]>? Errors);

public sealed class EmployeeApiException : Exception
{
    public EmployeeApiException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
