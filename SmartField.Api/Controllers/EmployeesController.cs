using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartField.Api.Authentication;
using SmartField.Application.Audit;
using SmartField.Application.Employees;
using SmartField.Infrastructure.Identity;
using SmartField.Infrastructure.Persistence;

namespace SmartField.Api.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize(Policy = SmartFieldPolicies.Backoffice)]
public sealed class EmployeesController : ControllerBase
{
    private readonly IEmployeeService employeeService;
    private readonly IAuditService auditService;
    private readonly UserManager<ApplicationUser> userManager;
    private readonly SmartFieldDbContext dbContext;
    private readonly TimeProvider timeProvider;

    public EmployeesController(
        IEmployeeService employeeService,
        IAuditService auditService,
        UserManager<ApplicationUser> userManager,
        SmartFieldDbContext dbContext,
        TimeProvider timeProvider)
    {
        this.employeeService = employeeService;
        this.auditService = auditService;
        this.userManager = userManager;
        this.dbContext = dbContext;
        this.timeProvider = timeProvider;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> Search(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await employeeService.SearchAsync(search, cancellationToken);
        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/user")]
    public async Task<ActionResult<EmployeeDto>> CreateUser(
        Guid id,
        CreateEmployeeUserRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = User.GetRequiredCompanyId();
        var employee = await dbContext.Employees.SingleOrDefaultAsync(
            item => item.CompanyId == companyId && item.Id == id,
            cancellationToken);

        if (employee is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Funcionário não encontrado."
            });
        }

        var employeeAlreadyHasUser = await dbContext.Users.AnyAsync(
            user => user.CompanyId == companyId && user.EmployeeId == id,
            cancellationToken);

        if (employeeAlreadyHasUser)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Funcionário já tem utilizador.",
                Detail = "Este funcionário já tem uma conta de login associada."
            });
        }

        var email = request.Email?.Trim();
        var validationErrors = ValidateUserRequest(email, request.Password);
        if (validationErrors.Count > 0)
        {
            return BadRequest(new ValidationProblemDetails(validationErrors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Os dados da conta de login não são válidos."
            });
        }

        var existingUser = await userManager.FindByEmailAsync(email!);
        if (existingUser is not null)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Email de login já utilizado.",
                Detail = "Já existe uma conta de login com este email."
            });
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            CompanyId = companyId,
            EmployeeId = id,
            IsActive = employee.IsActive
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return BadRequest(new ValidationProblemDetails(
                MapIdentityErrors(createResult.Errors))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Não foi possível criar a conta de login."
            });
        }

        var roleResult = await userManager.AddToRoleAsync(user, SmartFieldRoles.Employee);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return BadRequest(new ValidationProblemDetails(
                MapIdentityErrors(roleResult.Errors))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Não foi possível atribuir a role de funcionário."
            });
        }

        await AddAuditAsync(
            id,
            "UserCreated",
            null,
            JsonSerializer.Serialize(new
            {
                UserId = user.Id,
                user.Email,
                Role = SmartFieldRoles.Employee
            }),
            cancellationToken);

        var result = await employeeService.GetAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpGet("options")]
    public async Task<ActionResult<EmployeeOptions>> GetOptions(
        [FromQuery] Guid? employeeId,
        CancellationToken cancellationToken)
    {
        var result = await employeeService.GetOptionsAsync(
            employeeId,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await employeeService.GetAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeDto>> Create(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await employeeService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        var created = result.Value!;
        await AddAuditAsync(
            created.Id,
            "Created",
            null,
            JsonSerializer.Serialize(created),
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = created.Id },
            created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EmployeeDto>> Update(
        Guid id,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var beforeResult = await employeeService.GetAsync(id, cancellationToken);
        var result = await employeeService.UpdateAsync(
            id,
            request,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        await AddAuditAsync(
            id,
            "Updated",
            beforeResult.IsSuccess ? JsonSerializer.Serialize(beforeResult.Value) : null,
            JsonSerializer.Serialize(result.Value),
            cancellationToken);

        return Ok(result.Value);
    }

    private async Task AddAuditAsync(
        Guid entityId,
        string action,
        string? oldValues,
        string? newValues,
        CancellationToken cancellationToken)
    {
        auditService.Add(
            User.GetRequiredCompanyId(),
            User.GetRequiredUserId(),
            nameof(EmployeeDto).Replace("Dto", string.Empty, StringComparison.Ordinal),
            entityId,
            action,
            oldValues,
            newValues,
            timeProvider.GetUtcNow());
        await auditService.SaveChangesAsync(cancellationToken);
    }

    private static Dictionary<string, string[]> ValidateUserRequest(
        string? email,
        string? password)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(email))
        {
            errors[nameof(CreateEmployeeUserRequest.Email)] =
                ["O email de login é obrigatório."];
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            errors[nameof(CreateEmployeeUserRequest.Password)] =
                ["A password é obrigatória."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> MapIdentityErrors(
        IEnumerable<IdentityError> errors)
    {
        var mappedErrors = errors
            .Select(error => MapIdentityError(error.Code, error.Description))
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Message).ToArray());

        return mappedErrors.Count == 0
            ? new Dictionary<string, string[]>
            {
                [nameof(CreateEmployeeUserRequest.Password)] =
                    ["A password não cumpre os requisitos de segurança."]
            }
            : mappedErrors;
    }

    private static (string PropertyName, string Message) MapIdentityError(
        string code,
        string description)
    {
        return code switch
        {
            "DuplicateUserName" or "DuplicateEmail" =>
                (nameof(CreateEmployeeUserRequest.Email),
                    "Já existe uma conta de login com este email."),
            "InvalidEmail" =>
                (nameof(CreateEmployeeUserRequest.Email),
                    "O email de login não tem um formato válido."),
            "PasswordTooShort" =>
                (nameof(CreateEmployeeUserRequest.Password),
                    "A password deve ter pelo menos 6 caracteres."),
            "PasswordRequiresNonAlphanumeric" =>
                (nameof(CreateEmployeeUserRequest.Password),
                    "A password deve incluir pelo menos um símbolo, como !, ? ou #."),
            "PasswordRequiresDigit" =>
                (nameof(CreateEmployeeUserRequest.Password),
                    "A password deve incluir pelo menos um número."),
            "PasswordRequiresLower" =>
                (nameof(CreateEmployeeUserRequest.Password),
                    "A password deve incluir pelo menos uma letra minúscula."),
            "PasswordRequiresUpper" =>
                (nameof(CreateEmployeeUserRequest.Password),
                    "A password deve incluir pelo menos uma letra maiúscula."),
            _ => (nameof(CreateEmployeeUserRequest.Password), description)
        };
    }

    private ActionResult MapFailure<T>(EmployeeResult<T> result)
        where T : class
    {
        return result.Error switch
        {
            EmployeeError.CompanyUnavailable => Forbid(),
            EmployeeError.Validation => BadRequest(new ValidationProblemDetails(
                result.ValidationErrors.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Os dados do funcionário não são válidos."
            }),
            EmployeeError.NotFound => NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Funcionário não encontrado."
            }),
            EmployeeError.EmployeeNumberConflict => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Número de funcionário já utilizado.",
                Detail = "Já existe um funcionário com este número na empresa atual."
            }),
            EmployeeError.WorkSiteNotFound => BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Local habitual inválido.",
                Detail = "O local indicado não existe, está inativo ou não pertence à empresa atual."
            }),
            EmployeeError.UserNotFound => BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Utilizador inválido.",
                Detail = "O utilizador indicado não existe, está inativo ou não pertence à empresa atual."
            }),
            EmployeeError.UserAlreadyAssigned => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Utilizador já associado.",
                Detail = "O utilizador indicado já está associado a outro funcionário."
            }),
            _ => Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Não foi possível processar o funcionário.")
        };
    }
}
