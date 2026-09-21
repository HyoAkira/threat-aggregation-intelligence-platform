using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ThreatIntelligence.API.Errors;

public sealed class ApiProblemOpenApiTransformer : IOpenApiOperationTransformer
{
    private static readonly (string StatusCode, string Description)[] ProblemResponses =
    [
        ("400", "Bad request"),
        ("401", "Unauthorized"),
        ("403", "Forbidden"),
        ("404", "Not found"),
        ("409", "Conflict"),
        ("500", "Unexpected error"),
        ("503", "Service unavailable")
    ];

    public async Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var schema = await context.GetOrCreateSchemaAsync(
            typeof(ApiProblemDetails),
            parameterDescription: null,
            cancellationToken);

        if (context.Document is not null)
        {
            context.Document.AddComponent("ApiProblemDetails", schema);
        }

        operation.Responses ??= new OpenApiResponses();
        foreach (var (statusCode, description) in ProblemResponses)
        {
            if (operation.Responses.ContainsKey(statusCode))
            {
                continue;
            }

            operation.Responses[statusCode] = new OpenApiResponse
            {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [ApiProblemFactory.ProblemMediaType] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchemaReference("ApiProblemDetails", context.Document)
                    }
                }
            };
        }
    }
}
