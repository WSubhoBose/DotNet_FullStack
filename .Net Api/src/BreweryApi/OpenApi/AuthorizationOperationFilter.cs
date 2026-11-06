using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BreweryApi.OpenApi;

/// <summary>Adds the Auth header requirement only to protected operations.</summary>
public sealed class AuthorizationOperationFilter : IOperationFilter
{
    /// <summary>Applies authentication metadata from the endpoint to its OpenAPI operation.</summary>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any() ||
            !metadata.OfType<IAuthorizeData>().Any())
        {
            return;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Auth", context.Document)] = []
        });
    }
}
