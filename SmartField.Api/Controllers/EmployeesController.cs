using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartField.Api.Authentication;
using SmartField.Application.Audit;
using SmartField.Application.Employees;

namespace SmartField.Api.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize(Policy = SmartFieldPolicies.Backoffice)]
public sealed class EmployeesController : ControllerBase
{
    private readonly IEmployeeService employeeService;
    private readonly IAuditService auditService;
    private readonly TimeProvider timeProvider;

    public EmployeesController(
        IEmployeeService employeeService,
        IAuditService auditService,
        TimeProvider timeProvider)
    {
        this.employeeService = employeeService;
        this.auditService = auditService;
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
