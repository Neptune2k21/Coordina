using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Coordina.Api.Shared.OpenApi;

/// <summary>Writes the codes of an <see cref="EnumCodesAttribute"/> into the schema as <c>enum</c>.</summary>
internal sealed class EnumCodeSchemaTransformer : IOpenApiSchemaTransformer
{
  public Task TransformAsync(
    OpenApiSchema schema,
    OpenApiSchemaTransformerContext context,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(schema);
    ArgumentNullException.ThrowIfNull(context);

    var attribute = context.JsonPropertyInfo?.AttributeProvider?
      .GetCustomAttributes(typeof(EnumCodesAttribute), inherit: true)
      .OfType<EnumCodesAttribute>()
      .FirstOrDefault();

    if (attribute is not null)
    {
      var values = attribute.Codes
        .Select(code => (JsonNode?)JsonValue.Create(code))
        .ToList();

      // JSON Schema checks enum after type, so a nullable code must list null too.
      if (schema.Type?.HasFlag(JsonSchemaType.Null) == true)
      {
        values.Add(null);
      }

      schema.Enum = values!;
    }

    return Task.CompletedTask;
  }
}
