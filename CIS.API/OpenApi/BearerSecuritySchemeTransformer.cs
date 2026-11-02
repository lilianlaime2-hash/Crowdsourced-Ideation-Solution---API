using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CIS_WebAPI.OpenApi
{
    public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
    {
        public Task TransformAsync(
            OpenApiDocument document,
            OpenApiDocumentTransformerContext context,
            CancellationToken cancellationToken)
        {
            document.Components ??= new OpenApiComponents();

            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Enter your JWT token from Users API."
            };

            foreach (var path in document.Paths.Values)
            {
                if (path.Operations is null)
                {
                    continue;
                }

                foreach (var operation in path.Operations.Values)
                {
                    operation.Security ??= [];

                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                    });
                }
            }

            return Task.CompletedTask;
        }
    }
}
