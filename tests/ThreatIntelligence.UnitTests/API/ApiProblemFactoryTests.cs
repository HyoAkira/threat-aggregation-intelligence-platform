using Microsoft.AspNetCore.Http;
using ThreatIntelligence.API.Errors;

namespace ThreatIntelligence.UnitTests.API;

public sealed class ApiProblemFactoryTests
{
    [Fact]
    public void Creates_an_rfc_9457_problem_with_a_stable_urn_type()
    {
        var context = new DefaultHttpContext();
        context.Items["CorrelationId"] = "corr-123";

        var problem = ApiProblemFactory.Create(
            context,
            StatusCodes.Status400BadRequest,
            "invalid_input",
            "Invalid request",
            "The query is invalid.");

        Assert.Equal("urn:threat-intelligence:problem:invalid_input", problem.Type);
        Assert.Equal("invalid_input", problem.Code);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("corr-123", problem.CorrelationId);
    }
}
