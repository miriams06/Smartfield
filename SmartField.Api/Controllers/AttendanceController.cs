using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartField.Api.Authentication;
using SmartField.Application.Attendance;

namespace SmartField.Api.Controllers;

[ApiController]
[Route("api/attendance")]
[Authorize]
public sealed class AttendanceController : ControllerBase
{
    private readonly IAttendanceService attendanceService;
    private readonly ILogger<AttendanceController> logger;

    public AttendanceController(
        IAttendanceService attendanceService,
        ILogger<AttendanceController> logger)
    {
        this.attendanceService = attendanceService;
        this.logger = logger;
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

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<AttendanceHistoryDayDto>>> GetHistory(
        CancellationToken cancellationToken)
    {
        var result = await attendanceService.GetHistoryAsync(cancellationToken);
        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpGet("day/{date}")]
    public async Task<ActionResult<AttendanceDayDetailDto>> GetDay(
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var result = await attendanceService.GetDayAsync(date, cancellationToken);
        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpGet("admin/day")]
    [Authorize(Policy = SmartFieldPolicies.Backoffice)]
    public async Task<ActionResult<AttendanceBackofficeDayDto>> GetBackofficeDay(
        [FromQuery] DateOnly date,
        [FromQuery] Guid? employeeId,
        [FromQuery] Guid? workSiteId,
        CancellationToken cancellationToken)
    {
        var result = await attendanceService.GetBackofficeDayAsync(
            new AttendanceBackofficeDayFilter(date, employeeId, workSiteId),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpGet("admin/export.csv")]
    [Authorize(Policy = SmartFieldPolicies.Backoffice)]
    public async Task<IActionResult> ExportBackofficeCsv(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] Guid? employeeId,
        [FromQuery] Guid? workSiteId,
        CancellationToken cancellationToken)
    {
        var result = await attendanceService.ExportBackofficeCsvAsync(
            new AttendanceBackofficeExportFilter(
                fromDate,
                toDate,
                employeeId,
                workSiteId),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes(result.Value!.Content);
        var bytes = new byte[preamble.Length + content.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(content, 0, bytes, preamble.Length, content.Length);

        return File(bytes, result.Value.ContentType, result.Value.FileName);
    }

    [HttpGet("admin/day/{date}/employees/{employeeId}")]
    [Authorize(Policy = SmartFieldPolicies.Backoffice)]
    public async Task<ActionResult<AttendanceBackofficeDayDetailDto>> GetBackofficeDayDetail(
        DateOnly date,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var result = await attendanceService.GetBackofficeDayDetailAsync(
            employeeId,
            date,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpPost("admin/events/{attendanceEventId}/corrections")]
    [Authorize(Policy = SmartFieldPolicies.Backoffice)]
    public async Task<ActionResult<AttendanceCorrectionDto>> CorrectBackofficeEvent(
        Guid attendanceEventId,
        AttendanceCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await attendanceService.CorrectBackofficeEventAsync(
            attendanceEventId,
            request,
            cancellationToken);
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
        logger.LogWarning(
            "Attendance request failed with {AttendanceError}. Detail: {AttendanceFailureDetail}",
            result.Error,
            result.Detail);

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
            AttendanceError.EmployeeNotFound => NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Funcionário não encontrado."
            }),
            AttendanceError.AttendanceEventNotFound => NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Picagem não encontrada."
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
                title: "Não foi possível processar o pedido de assiduidade.")
        };
    }
}
