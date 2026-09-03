using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartField.Api.Authentication;
using SmartField.Application.Geolocation;

namespace SmartField.Api.Controllers;

[ApiController]
[Route("api/geofence-settings")]
[Authorize(Policy = SmartFieldPolicies.Backoffice)]
public sealed class GeofenceSettingsController : ControllerBase
{
    private readonly IGeofenceSettingsService geofenceSettingsService;

    public GeofenceSettingsController(
        IGeofenceSettingsService geofenceSettingsService)
    {
        this.geofenceSettingsService = geofenceSettingsService;
    }

    [HttpGet]
    public async Task<ActionResult<GeofenceSettingsDto>> Get(
        CancellationToken cancellationToken)
    {
        var result = await geofenceSettingsService.GetAsync(cancellationToken);
        if (!result.IsSuccess)
        {
            return MapFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpPut]
    public async Task<ActionResult<GeofenceSettingsDto>> Update(
        UpdateGeofenceSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await geofenceSettingsService.UpdateAsync(
            request,
            cancellationToken);

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
                Title = "A configuração de geofence não é válida."
            }),
            _ => Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Não foi possível processar a configuração de geofence.")
        };
    }
}
