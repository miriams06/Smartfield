using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using SmartField.Api.Authentication;
using SmartField.Api.Controllers;

namespace SmartField.Api.Tests;

public class ProjectsControllerTests
{
    [Fact]
    public void Controller_RequiresBackofficePolicy()
    {
        var authorize = typeof(ProjectsController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(SmartFieldPolicies.Backoffice, authorize.Policy);
    }

    [Fact]
    public void Controller_ExposesPlannerRoutes()
    {
        var controllerRoute = typeof(ProjectsController)
            .GetCustomAttribute<RouteAttribute>();

        Assert.Equal("api/projects", controllerRoute?.Template);
        AssertHttpMethod(nameof(ProjectsController.Search), typeof(HttpGetAttribute), null);
        AssertHttpMethod(nameof(ProjectsController.GetById), typeof(HttpGetAttribute), "{id:guid}");
        AssertHttpMethod(nameof(ProjectsController.Create), typeof(HttpPostAttribute), null);
        AssertHttpMethod(nameof(ProjectsController.Update), typeof(HttpPutAttribute), "{id:guid}");
    }

    private static void AssertHttpMethod(
        string methodName,
        Type attributeType,
        string? expectedTemplate)
    {
        var method = typeof(ProjectsController).GetMethod(methodName);
        var attribute = method?.GetCustomAttributes(attributeType, inherit: false)
            .OfType<HttpMethodAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(expectedTemplate, attribute.Template);
    }
}
