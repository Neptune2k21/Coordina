using Microsoft.AspNetCore.OpenApi;

namespace Coordina.Api.Shared.OpenApi;

public static class OpenApiServiceCollectionExtensions
{
  /// <summary>Lists the allowed codes of every <see cref="EnumCodesAttribute"/> property in the contract.</summary>
  public static IServiceCollection AddEnumCodeSchemas(this IServiceCollection services) =>
    services.ConfigureAll<OpenApiOptions>(options =>
      options.AddSchemaTransformer<EnumCodeSchemaTransformer>());
}
