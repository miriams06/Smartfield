using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartField.Application.Abstractions;
using SmartField.Application.Employees;
using SmartField.Application.WorkSites;

namespace SmartField.Api.Controllers;

[ApiController]
[Route("api/attendance/worksites")]
[Authorize]
public sealed class AttendanceWorkSitesController : ControllerBase
{
    private readonly IWorkSiteService workSiteService;
    private readonly IEmployeeService employeeService;
    private readonly ICurrentUserProvider currentUserProvider;

    public AttendanceWorkSitesController(
        IWorkSiteService workSiteService,
        IEmployeeService employeeService,
        ICurrentUserProvider currentUserProvider)
    {
        this.workSiteService = workSiteService;
        this.employeeService = employeeService;
        this.currentUserProvider = currentUserProvider;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AttendanceWorkSiteOptionDto>>> GetActive(
        CancellationToken cancellationToken)
    {
        var employeeId = currentUserProvider.EmployeeId;
        if (!employeeId.HasValue)
        {
            return Forbid();
        }

        var employeeResult = await employeeService.GetAsync(
            employeeId.Value,
            cancellationToken);
        if (!employeeResult.IsSuccess || employeeResult.Value is null)
        {
            return Forbid();
        }

        var workSitesResult = await workSiteService.SearchAsync(null, cancellationToken);
        if (!workSitesResult.IsSuccess)
        {
            return workSitesResult.Error == WorkSiteError.CompanyUnavailable
                ? Forbid()
                : Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Não foi possível carregar os locais de trabalho disponíveis.");
        }

        var defaultWorkSiteId = employeeResult.Value.DefaultWorkSiteId;
        var workSites = workSitesResult.Value!
            .Where(workSite => workSite.IsActive)
            .OrderByDescending(workSite => workSite.Id == defaultWorkSiteId)
            .ThenBy(workSite => workSite.Name)
            .ThenBy(workSite => workSite.Code)
            .Select(workSite => new AttendanceWorkSiteOptionDto(
                workSite.Id,
                workSite.Code,
                workSite.Name,
                workSite.Address,
                workSite.Id == defaultWorkSiteId))
            .ToArray();

        return Ok(workSites);
    }
}

public sealed record AttendanceWorkSiteOptionDto(
    Guid Id,
    string Code,
    string Name,
    string? Address,
    bool IsDefault);
