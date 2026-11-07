using Coordina.Api.Shared.Enums;

namespace Coordina.Api.Shared.OpenApi;

/// <summary>
/// Marks a string property that carries an enum code, so the OpenAPI contract
/// lists the allowed values and the web app gets a union type instead of
/// <c>string</c>. Use the generic form: <c>[property: EnumCodes&lt;BoardTaskStatus&gt;]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public abstract class EnumCodesAttribute : Attribute
{
  public abstract IReadOnlyList<string> Codes { get; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class EnumCodesAttribute<TEnum> : EnumCodesAttribute
  where TEnum : struct, Enum
{
  public override IReadOnlyList<string> Codes { get; } =
    Enum.GetValues<TEnum>().Select(EnumCode.Format).ToArray();
}
