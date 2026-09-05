using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using ThreatIntelligence.API.Controllers;
using ThreatIntelligence.Application.DTOs;

namespace ThreatIntelligence.Application.Tests.Controllers;

public class ControllerResponseTypingTests
{
    [Fact]
    public void AllActionsReturnTypedActionResult()
    {
        var untyped = GetActions()
            .Where(a => !IsTypedActionResult(a.ReturnType))
            .Select(a => $"{a.DeclaringType!.Name}.{a.Name} returns {a.ReturnType.Name}")
            .ToList();

        Assert.Empty(untyped);
    }

    [Fact]
    public void AllActionsDeclareProblemDetailsErrorResponses()
    {
        var missing = GetActions()
            .Where(a => !HasProblemDetailsErrorResponses(a))
            .Select(a => $"{a.DeclaringType!.Name}.{a.Name}")
            .ToList();

        Assert.Empty(missing);
    }

    private static IEnumerable<MethodInfo> GetActions()
    {
        return typeof(ThreatFoxController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());
    }

    private static bool IsTypedActionResult(Type returnType)
    {
        var unwrapped = UnwrapTask(returnType);
        return unwrapped.IsGenericType && unwrapped.GetGenericTypeDefinition() == typeof(ActionResult<>);
    }

    private static Type UnwrapTask(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>)
            ? type.GetGenericArguments()[0]
            : type;
    }

    private static bool HasProblemDetailsErrorResponses(MethodInfo action)
    {
        var errorStatuses = new[]
        {
            StatusCodes.Status401Unauthorized,
            StatusCodes.Status502BadGateway,
            StatusCodes.Status422UnprocessableEntity,
            StatusCodes.Status500InternalServerError
        };

        var declared = action.GetCustomAttributes<ProducesResponseTypeAttribute>()
            .Where(p => p.Type == typeof(ProblemDetails))
            .Select(p => p.StatusCode)
            .ToHashSet();

        return errorStatuses.All(declared.Contains);
    }
}