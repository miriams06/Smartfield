using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using SmartField.Application.Abstractions;
using SmartField.Application.IntegrationOutbox;
using SmartField.Domain.Entities;

namespace SmartField.Application.Employees;

public sealed class EmployeeService : IEmployeeService
{
    private const int EmployeeNumberMaxLength = 50;
    private const int NameMaxLength = 200;
    private const int EmailMaxLength = 320;
    private const int MobilePhoneMaxLength = 50;
    private const int ErpEmployeeCodeMaxLength = 100;
    private const int SearchMaxLength = 200;

    private static readonly EmailAddressAttribute EmailValidator = new();

    private readonly IEmployeeStore employeeStore;
    private readonly ICurrentCompanyProvider currentCompanyProvider;
    private readonly IIntegrationOutboxService integrationOutboxService;
    private readonly TimeProvider timeProvider;

    public EmployeeService(
        IEmployeeStore employeeStore,
        ICurrentCompanyProvider currentCompanyProvider,
        IIntegrationOutboxService integrationOutboxService,
        TimeProvider timeProvider)
    {
        this.employeeStore = employeeStore;
        this.currentCompanyProvider = currentCompanyProvider;
        this.integrationOutboxService = integrationOutboxService;
        this.timeProvider = timeProvider;
    }

    public async Task<EmployeeResult<IReadOnlyList<EmployeeDto>>> SearchAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var companyId = currentCompanyProvider.CompanyId;
        if (!companyId.HasValue)
        {
            return EmployeeResult<IReadOnlyList<EmployeeDto>>.Failure(
                EmployeeError.CompanyUnavailable);
        }

        var normalizedSearch = NormalizeOptional(search);
        if (normalizedSearch is { Length: > SearchMaxLength })
        {
            normalizedSearch = normalizedSearch[..SearchMaxLength];
        }

        var employees = await employeeStore.SearchAsync(
            companyId.Value,
            normalizedSearch,
            cancellationToken);

        return EmployeeResult<IReadOnlyList<EmployeeDto>>.Success(employees);
    }

    public async Task<EmployeeResult<EmployeeDto>> GetAsync(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var companyId = currentCompanyProvider.CompanyId;
        if (!companyId.HasValue)
        {
            return EmployeeResult<EmployeeDto>.Failure(EmployeeError.CompanyUnavailable);
        }

        var employee = await employeeStore.GetAsync(
            companyId.Value,
            employeeId,
            cancellationToken);

        return employee is null
            ? EmployeeResult<EmployeeDto>.Failure(EmployeeError.NotFound)
            : EmployeeResult<EmployeeDto>.Success(employee);
    }

    public async Task<EmployeeResult<EmployeeOptions>> GetOptionsAsync(
        Guid? employeeId,
        CancellationToken cancellationToken)
    {
        var companyId = currentCompanyProvider.CompanyId;
        if (!companyId.HasValue)
        {
            return EmployeeResult<EmployeeOptions>.Failure(EmployeeError.CompanyUnavailable);
        }

        if (employeeId.HasValue)
        {
            var employee = await employeeStore.FindEntityAsync(
                companyId.Value,
                employeeId.Value,
                cancellationToken);

            if (employee is null)
            {
                return EmployeeResult<EmployeeOptions>.Failure(EmployeeError.NotFound);
            }
        }

        var options = await employeeStore.GetOptionsAsync(
            companyId.Value,
            employeeId,
            cancellationToken);

        return EmployeeResult<EmployeeOptions>.Success(options);
    }

    public async Task<EmployeeResult<EmployeeDto>> CreateAsync(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = currentCompanyProvider.CompanyId;
        if (!companyId.HasValue)
        {
            return EmployeeResult<EmployeeDto>.Failure(EmployeeError.CompanyUnavailable);
        }

        var validation = ValidateAndNormalize(
            request.EmployeeNumber,
            request.Name,
            request.Email,
            request.MobilePhone,
            request.IsActive,
            request.DefaultWorkSiteId,
            request.UserId,
            request.ErpEmployeeCode);

        if (validation.Errors.Count > 0)
        {
            return EmployeeResult<EmployeeDto>.Invalid(validation.Errors);
        }

        if (await employeeStore.EmployeeNumberExistsAsync(
            companyId.Value,
            validation.Input.EmployeeNumber,
            null,
            cancellationToken))
        {
            return EmployeeResult<EmployeeDto>.Failure(
                EmployeeError.EmployeeNumberConflict);
        }

        var workSiteError = await ValidateWorkSiteAsync(
            companyId.Value,
            validation.Input.DefaultWorkSiteId,
            null,
            cancellationToken);

        if (workSiteError != EmployeeError.None)
        {
            return EmployeeResult<EmployeeDto>.Failure(workSiteError);
        }

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId.Value,
            EmployeeNumber = validation.Input.EmployeeNumber,
            Name = validation.Input.Name,
            Email = validation.Input.Email,
            MobilePhone = validation.Input.MobilePhone,
            IsActive = validation.Input.IsActive,
            DefaultWorkSiteId = validation.Input.DefaultWorkSiteId,
            ErpEmployeeCode = validation.Input.ErpEmployeeCode,
            CreatedAtUtc = timeProvider.GetUtcNow()
        };

        var associationStatus = await employeeStore.SetUserAssociationAsync(
            companyId.Value,
            employee.Id,
            validation.Input.UserId,
            cancellationToken);

        var associationError = MapAssociationError(associationStatus);
        if (associationError != EmployeeError.None)
        {
            return EmployeeResult<EmployeeDto>.Failure(associationError);
        }

        employeeStore.Add(employee);
        integrationOutboxService.Add(new IntegrationOutboxMessage(
            companyId.Value,
            IntegrationOutboxEventTypes.EmployeeCreated,
            nameof(Employee),
            employee.Id,
            SerializeEmployee(employee),
            employee.CreatedAtUtc));

        try
        {
            await employeeStore.SaveChangesAsync(cancellationToken);
        }
        catch (EmployeeNumberConflictException)
        {
            return EmployeeResult<EmployeeDto>.Failure(
                EmployeeError.EmployeeNumberConflict);
        }

        var createdEmployee = await employeeStore.GetAsync(
            companyId.Value,
            employee.Id,
            cancellationToken);

        return createdEmployee is null
            ? EmployeeResult<EmployeeDto>.Failure(EmployeeError.NotFound)
            : EmployeeResult<EmployeeDto>.Success(createdEmployee);
    }

    public async Task<EmployeeResult<EmployeeDto>> UpdateAsync(
        Guid employeeId,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = currentCompanyProvider.CompanyId;
        if (!companyId.HasValue)
        {
            return EmployeeResult<EmployeeDto>.Failure(EmployeeError.CompanyUnavailable);
        }

        var employee = await employeeStore.FindEntityAsync(
            companyId.Value,
            employeeId,
            cancellationToken);

        if (employee is null)
        {
            return EmployeeResult<EmployeeDto>.Failure(EmployeeError.NotFound);
        }

        var validation = ValidateAndNormalize(
            request.EmployeeNumber,
            request.Name,
            request.Email,
            request.MobilePhone,
            request.IsActive,
            request.DefaultWorkSiteId,
            request.UserId,
            request.ErpEmployeeCode);

        if (validation.Errors.Count > 0)
        {
            return EmployeeResult<EmployeeDto>.Invalid(validation.Errors);
        }

        if (await employeeStore.EmployeeNumberExistsAsync(
            companyId.Value,
            validation.Input.EmployeeNumber,
            employeeId,
            cancellationToken))
        {
            return EmployeeResult<EmployeeDto>.Failure(
                EmployeeError.EmployeeNumberConflict);
        }

        var workSiteError = await ValidateWorkSiteAsync(
            companyId.Value,
            validation.Input.DefaultWorkSiteId,
            employeeId,
            cancellationToken);

        if (workSiteError != EmployeeError.None)
        {
            return EmployeeResult<EmployeeDto>.Failure(workSiteError);
        }

        var associationStatus = await employeeStore.SetUserAssociationAsync(
            companyId.Value,
            employeeId,
            validation.Input.UserId,
            cancellationToken);

        var associationError = MapAssociationError(associationStatus);
        if (associationError != EmployeeError.None)
        {
            return EmployeeResult<EmployeeDto>.Failure(associationError);
        }

        employee.EmployeeNumber = validation.Input.EmployeeNumber;
        employee.Name = validation.Input.Name;
        employee.Email = validation.Input.Email;
        employee.MobilePhone = validation.Input.MobilePhone;
        employee.IsActive = validation.Input.IsActive;
        employee.DefaultWorkSiteId = validation.Input.DefaultWorkSiteId;
        employee.ErpEmployeeCode = validation.Input.ErpEmployeeCode;
        employee.UpdatedAtUtc = timeProvider.GetUtcNow();
        integrationOutboxService.Add(new IntegrationOutboxMessage(
            companyId.Value,
            IntegrationOutboxEventTypes.EmployeeUpdated,
            nameof(Employee),
            employee.Id,
            SerializeEmployee(employee),
            employee.UpdatedAtUtc.Value));

        try
        {
            await employeeStore.SaveChangesAsync(cancellationToken);
        }
        catch (EmployeeNumberConflictException)
        {
            return EmployeeResult<EmployeeDto>.Failure(
                EmployeeError.EmployeeNumberConflict);
        }

        var updatedEmployee = await employeeStore.GetAsync(
            companyId.Value,
            employeeId,
            cancellationToken);

        return updatedEmployee is null
            ? EmployeeResult<EmployeeDto>.Failure(EmployeeError.NotFound)
            : EmployeeResult<EmployeeDto>.Success(updatedEmployee);
    }

    private async Task<EmployeeError> ValidateWorkSiteAsync(
        Guid companyId,
        Guid? workSiteId,
        Guid? employeeId,
        CancellationToken cancellationToken)
    {
        if (!workSiteId.HasValue)
        {
            return EmployeeError.None;
        }

        return await employeeStore.WorkSiteCanBeAssignedAsync(
            companyId,
            workSiteId.Value,
            employeeId,
            cancellationToken)
            ? EmployeeError.None
            : EmployeeError.WorkSiteNotFound;
    }

    private static EmployeeError MapAssociationError(
        EmployeeUserAssociationStatus status)
    {
        return status switch
        {
            EmployeeUserAssociationStatus.Success => EmployeeError.None,
            EmployeeUserAssociationStatus.UserNotFound => EmployeeError.UserNotFound,
            EmployeeUserAssociationStatus.UserAlreadyAssigned => EmployeeError.UserAlreadyAssigned,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    private static EmployeeValidationResult ValidateAndNormalize(
        string? employeeNumber,
        string? name,
        string? email,
        string? mobilePhone,
        bool isActive,
        Guid? defaultWorkSiteId,
        Guid? userId,
        string? erpEmployeeCode)
    {
        var normalizedEmployeeNumber = employeeNumber?.Trim() ?? string.Empty;
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedEmail = NormalizeOptional(email);
        var normalizedMobilePhone = NormalizeOptional(mobilePhone);
        var normalizedErpEmployeeCode = NormalizeOptional(erpEmployeeCode);
        var errors = new Dictionary<string, string[]>();

        ValidateRequiredText(
            normalizedEmployeeNumber,
            EmployeeNumberMaxLength,
            nameof(CreateEmployeeRequest.EmployeeNumber),
            "O número de funcionário",
            errors);

        ValidateRequiredText(
            normalizedName,
            NameMaxLength,
            nameof(CreateEmployeeRequest.Name),
            "O nome",
            errors);

        ValidateOptionalText(
            normalizedEmail,
            EmailMaxLength,
            nameof(CreateEmployeeRequest.Email),
            "O email",
            errors);

        if (normalizedEmail is not null && !EmailValidator.IsValid(normalizedEmail))
        {
            errors[nameof(CreateEmployeeRequest.Email)] =
                ["O email não tem um formato válido."];
        }

        ValidateOptionalText(
            normalizedMobilePhone,
            MobilePhoneMaxLength,
            nameof(CreateEmployeeRequest.MobilePhone),
            "O telefone",
            errors);

        ValidateOptionalText(
            normalizedErpEmployeeCode,
            ErpEmployeeCodeMaxLength,
            nameof(CreateEmployeeRequest.ErpEmployeeCode),
            "O código de funcionário no ERP",
            errors);

        var input = new NormalizedEmployeeInput(
            normalizedEmployeeNumber,
            normalizedName,
            normalizedEmail,
            normalizedMobilePhone,
            isActive,
            defaultWorkSiteId,
            userId,
            normalizedErpEmployeeCode);

        return new EmployeeValidationResult(input, errors);
    }

    private static void ValidateRequiredText(
        string value,
        int maxLength,
        string propertyName,
        string displayName,
        IDictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[propertyName] = [$"{displayName} é obrigatório."];
        }
        else if (value.Length > maxLength)
        {
            errors[propertyName] =
                [$"{displayName} não pode exceder {maxLength} caracteres."];
        }
    }

    private static void ValidateOptionalText(
        string? value,
        int maxLength,
        string propertyName,
        string displayName,
        IDictionary<string, string[]> errors)
    {
        if (value is { Length: > 0 } && value.Length > maxLength)
        {
            errors[propertyName] =
                [$"{displayName} não pode exceder {maxLength} caracteres."];
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string SerializeEmployee(Employee employee)
    {
        return JsonSerializer.Serialize(new
        {
            employee.Id,
            employee.CompanyId,
            employee.EmployeeNumber,
            employee.Name,
            employee.Email,
            employee.MobilePhone,
            employee.IsActive,
            employee.DefaultWorkSiteId,
            employee.ErpEmployeeCode,
            employee.CreatedAtUtc,
            employee.UpdatedAtUtc
        });
    }

    private sealed record NormalizedEmployeeInput(
        string EmployeeNumber,
        string Name,
        string? Email,
        string? MobilePhone,
        bool IsActive,
        Guid? DefaultWorkSiteId,
        Guid? UserId,
        string? ErpEmployeeCode);

    private sealed record EmployeeValidationResult(
        NormalizedEmployeeInput Input,
        IReadOnlyDictionary<string, string[]> Errors);
}
