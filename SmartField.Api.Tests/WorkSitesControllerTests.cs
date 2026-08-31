using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using SmartField.Api.Authentication;
using SmartField.Api.Controllers;

namespace SmartField.Api.Tests;

public class WorkSitesControllerTests
{
    [Fact]
    public void Controller_RequiresBackofficePolicy()
    {
        var authorize = typeof(WorkSitesController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(SmartFieldPolicies.Backoffice, authorize.Policy);
    }

    [Fact]
    public void Controller_ExposesPlannerRoutes()
    {
        var controllerRoute = typeof(WorkSitesController)
            .GetCustomAttribute<RouteAttribute>();

        Assert.Equal("api/worksites", controllerRoute?.Template);
        AssertHttpMethod(nameof(WorkSitesController.Search), typeof(HttpGetAttribute), null);
        AssertHttpMethod(nameof(WorkSitesController.GetById), typeof(HttpGetAttribute), "{id:guid}");
        AssertHttpMethod(nameof(WorkSitesController.Create), typeof(HttpPostAttribute), null);
        AssertHttpMethod(nameof(WorkSitesController.Update), typeof(HttpPutAttribute), "{id:guid}");
    }

    private static void AssertHttpMethod(
        string methodName,
        Type attributeType,
        string? expectedTemplate)
    {
        var method = typeof(WorkSitesController).GetMethod(methodName);
        var attribute = method?.GetCustomAttributes(attributeType, inherit: false)
            .OfType<HttpMethodAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(expectedTemplate, attribute.Template);
    }
}
