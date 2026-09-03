using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using SmartField.Api.Authentication;
using SmartField.Api.Controllers;
using SmartField.Infrastructure.Identity;

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
        AssertHttpMethod(nameof(EmployeesController.CreateUser), typeof(HttpPostAttribute), "{id:guid}/user");
    }

    [Theory]
    [InlineData(SmartFieldRoles.Employee)]
    [InlineData(SmartFieldRoles.Manager)]
    public void CreateUser_AcceptsSupportedAssignableRoles(string role)
    {
        var errors = ValidateUserRequest("user@smartfield.local", "Password1!", role);

        Assert.DoesNotContain("Role", errors.Keys);
    }

    [Fact]
    public void CreateUser_RejectsAdminRoleThroughEmployeeFlow()
    {
        var errors = ValidateUserRequest(
            "user@smartfield.local",
            "Password1!",
            SmartFieldRoles.Admin);

        Assert.Contains("Role", errors.Keys);
    }

    private static Dictionary<string, string[]> ValidateUserRequest(
        string email,
        string password,
        string role)
    {
        var method = typeof(EmployeesController).GetMethod(
            "ValidateUserRequest",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        return Assert.IsType<Dictionary<string, string[]>>(
            method.Invoke(null, [email, password, role]));
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
