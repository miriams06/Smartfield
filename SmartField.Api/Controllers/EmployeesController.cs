using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartField.Api.Authentication;
using SmartField.Application.Employees;

namespace SmartField.Api.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize(Policy = SmartFieldPolicies.Backoffice)]
public sealed class EmployeesController : ControllerBase
{
    private readonly IEmployeeService employeeService;

    public EmployeesController(IEmployeeService employeeService)
    {
        this.employeeService = employeeService;
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

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Value!.Id },
            result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EmployeeDto>> Update(
        Guid id,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await employeeService.UpdateAsync(
            id,
            request,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        return Ok(result.Value);
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
