using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartField.Application.Attendance;

namespace SmartField.Api.Controllers;

[ApiController]
[Route("api/attendance")]
[Authorize]
public sealed class AttendanceController : ControllerBase
{
    private readonly IAttendanceService attendanceService;

    public AttendanceController(IAttendanceService attendanceService)
    {
        this.attendanceService = attendanceService;
    }

    [HttpGet("state")]
    public async Task<ActionResult<AttendanceStateDto>> GetState(
        CancellationToken cancellationToken)
    {
        var result = await attendanceService.GetStateAsync(cancellationToken);
        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpGet("today")]
    public async Task<ActionResult<AttendanceTodayDto>> GetToday(
        CancellationToken cancellationToken)
    {
        var result = await attendanceService.GetTodayAsync(cancellationToken);
        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpPost("punch")]
    public async Task<ActionResult<AttendancePunchDto>> Punch(
        AttendancePunchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await attendanceService.PunchAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        return Ok(result.Value);
    }

    private ActionResult MapFailure<T>(AttendanceResult<T> result)
        where T : class
    {
        return result.Error switch
        {
            AttendanceError.CompanyUnavailable
                or AttendanceError.UserUnavailable
                or AttendanceError.EmployeeUnavailable => Forbid(),
            AttendanceError.Validation => BadRequest(new ValidationProblemDetails(
                result.ValidationErrors.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Os dados da picagem não são válidos."
            }),
            AttendanceError.WorkSiteNotFound => NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Local de trabalho não encontrado."
            }),
            AttendanceError.ProjectNotFound => NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Projeto não encontrado."
            }),
            AttendanceError.InvalidSequence => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Sequência de picagens inválida.",
                Detail = result.Detail
            }),
            AttendanceError.GeofenceRejected => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Picagem bloqueada pela geofence.",
                Detail = result.Detail
            }),
            AttendanceError.ClientEventConflict => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Não foi possível garantir a idempotência da picagem."
            }),
            _ => Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Não foi possível registar a picagem.")
        };
    }
}
