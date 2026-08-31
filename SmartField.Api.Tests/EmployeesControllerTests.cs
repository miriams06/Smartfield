using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using SmartField.Api.Authentication;
using SmartField.Api.Controllers;

namespace SmartField.Api.Tests;

public class EmployeesControllerTests
{
    [Fact]
    public void Controller_RequiresBackofficePolicy()
    {
        var authorize = typeof(EmployeesController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(SmartFieldPolicies.Backoffice, authorize.Policy);
    }

    [Fact]
    public void Controller_ExposesPlannerRoutes()
    {
        var controllerRoute = typeof(EmployeesController)
            .GetCustomAttribute<RouteAttribute>();

        Assert.Equal("api/employees", controllerRoute?.Template);
        AssertHttpMethod(nameof(EmployeesController.Search), typeof(HttpGetAttribute), null);
        AssertHttpMethod(nameof(EmployeesController.GetById), typeof(HttpGetAttribute), "{id:guid}");
        AssertHttpMethod(nameof(EmployeesController.Create), typeof(HttpPostAttribute), null);
        AssertHttpMethod(nameof(EmployeesController.Update), typeof(HttpPutAttribute), "{id:guid}");
    }

    private static void AssertHttpMethod(
        string methodName,
        Type attributeType,
        string? expectedTemplate)
    {
        var method = typeof(EmployeesController).GetMethod(methodName);
        var attribute = method?.GetCustomAttributes(attributeType, inherit: false)
            .OfType<HttpMethodAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(expectedTemplate, attribute.Template);
    }
}
