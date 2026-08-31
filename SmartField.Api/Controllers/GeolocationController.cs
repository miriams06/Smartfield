using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartField.Application.Geolocation;

namespace SmartField.Api.Controllers;

[ApiController]
[Route("api/geolocation")]
[Authorize]
public sealed class GeolocationController : ControllerBase
{
    private readonly IGeolocationService geolocationService;

    public GeolocationController(IGeolocationService geolocationService)
    {
        this.geolocationService = geolocationService;
    }

    [HttpPost("validate")]
    public async Task<ActionResult<GeolocationValidationDto>> Validate(
        GeolocationValidationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await geolocationService.ValidateAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        return Ok(result.Value);
    }

    private ActionResult MapFailure<T>(GeolocationResult<T> result)
        where T : class
    {
        return result.Error switch
        {
            GeolocationError.CompanyUnavailable => Forbid(),
            GeolocationError.Validation => BadRequest(new ValidationProblemDetails(
                result.ValidationErrors.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Os dados de geolocalização não são válidos."
            }),
            GeolocationError.WorkSiteNotFound => NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Local de trabalho não encontrado."
            }),
            _ => Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Não foi possível validar a geolocalização.")
        };
    }
}
