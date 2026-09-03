using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using SmartField.Api.Authentication;
using SmartField.Api.Controllers;

namespace SmartField.Api.Tests;

public class GeofenceSettingsControllerTests
{
    [Fact]
    public void Controller_RequiresBackofficePolicy()
    {
        var authorize = typeof(GeofenceSettingsController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(SmartFieldPolicies.Backoffice, authorize.Policy);
    }

    [Fact]
    public void Controller_ExposesSettingsRoutes()
    {
        var controllerRoute = typeof(GeofenceSettingsController)
            .GetCustomAttribute<RouteAttribute>();

        Assert.Equal("api/geofence-settings", controllerRoute?.Template);
        AssertHttpMethod(nameof(GeofenceSettingsController.Get), typeof(HttpGetAttribute), null);
        AssertHttpMethod(nameof(GeofenceSettingsController.Update), typeof(HttpPutAttribute), null);
    }

    private static void AssertHttpMethod(
        string methodName,
        Type attributeType,
        string? expectedTemplate)
    {
        var method = typeof(GeofenceSettingsController).GetMethod(methodName);
        var attribute = method?.GetCustomAttributes(attributeType, inherit: false)
            .OfType<HttpMethodAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(expectedTemplate, attribute.Template);
    }
}
