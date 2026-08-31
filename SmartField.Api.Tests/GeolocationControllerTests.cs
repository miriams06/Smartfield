using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using SmartField.Api.Controllers;

namespace SmartField.Api.Tests;

public class GeolocationControllerTests
{
    [Fact]
    public void Controller_RequiresAuthenticatedUser()
    {
        var authorize = typeof(GeolocationController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Null(authorize.Policy);
    }

    [Fact]
    public void Controller_ExposesPlannerValidationRoute()
    {
        var controllerRoute = typeof(GeolocationController)
            .GetCustomAttribute<RouteAttribute>();

        Assert.Equal("api/geolocation", controllerRoute?.Template);
        AssertHttpMethod(
            nameof(GeolocationController.Validate),
            typeof(HttpPostAttribute),
            "validate");
    }

    private static void AssertHttpMethod(
        string methodName,
        Type attributeType,
        string? expectedTemplate)
    {
        var method = typeof(GeolocationController).GetMethod(methodName);
        var attribute = method?.GetCustomAttributes(attributeType, inherit: false)
            .OfType<HttpMethodAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(expectedTemplate, attribute.Template);
    }
}
