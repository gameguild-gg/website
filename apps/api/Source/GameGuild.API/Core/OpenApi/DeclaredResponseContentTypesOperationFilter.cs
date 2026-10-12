using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace GameGuild.API.Core.OpenApi;

/// <summary>Preserves explicit response media types over inherited controller defaults.</summary>
public sealed class DeclaredResponseContentTypesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        foreach (var group in context.MethodInfo.GetCustomAttributes<ProducesResponseTypeAttribute>(inherit: true)
                     .GroupBy(attribute => attribute.StatusCode))
        {
            var declared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var metadata in group)
            {
                var contentTypes = new MediaTypeCollection();
                ((IApiResponseMetadataProvider)metadata).SetContentTypes(contentTypes);
                declared.UnionWith(contentTypes);
            }

            if (declared.Count == 0 ||
                !operation.Responses.TryGetValue(group.Key.ToString(CultureInfo.InvariantCulture), out var response))
            {
                continue;
            }

            foreach (var mediaType in response.Content.Keys.Where(mediaType => !declared.Contains(mediaType)).ToArray())
            {
                response.Content.Remove(mediaType);
            }
        }
    }
}
