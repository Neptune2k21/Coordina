using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Coordina.Api.Modules.Boards.Application.Graph;
using Coordina.Api.Tests.Support;

namespace Coordina.Api.Tests.Unit.Boards;

/// <summary>
/// The API marshals the graph engine's C structs by hand, so the two sides
/// must agree on every field. <c>contracts/graph-engine-abi.json</c> records
/// the layout as the C++ compiler sees it (<c>make graph-engine-abi</c>), and
/// these tests compare the C# side with it. A field added, removed, reordered
/// or resized on one side only fails here instead of corrupting memory.
/// </summary>
public sealed partial class GraphEngineAbiTests
{
  private const string ContractPath = "contracts/graph-engine-abi.json";
  private const string NativePrefix = "coordina_graph_";

  private static readonly Lazy<JsonDocument> Contract = new(() =>
    JsonDocument.Parse(File.ReadAllText(Path.Combine(SourceTree.RepositoryRoot, ContractPath))));

  // The C# enums that mirror the C ones.
  private static readonly Dictionary<string, Type> Enums = new(StringComparer.Ordinal)
  {
    ["coordina_graph_assignment"] = typeof(GraphAssignment),
    ["coordina_graph_size"] = typeof(GraphSize),
    ["coordina_graph_edge_status"] = typeof(GraphEdgeStatus),
    ["coordina_graph_status"] = NestedType("NativeGraphStatus")
  };

  public static TheoryData<string> Structs => new(Entries("structs").Select(Name));

  public static TheoryData<string> EnumNames => new(Entries("enums").Select(Name));

  [Theory]
  [MemberData(nameof(Structs))]
  public void Struct_MatchesTheNativeLayout(string name)
  {
    var native = Entries("structs").Single(entry => Name(entry) == name);
    var type = NestedType(ManagedStructName(name));

    var expected = native.GetProperty("fields").EnumerateArray()
      .Select(field => (
        Name: PascalCase(field.GetProperty("name").GetString()!),
        Offset: field.GetProperty("offset").GetInt32(),
        Size: field.GetProperty("size").GetInt32()))
      .ToArray();
    var actual = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
      .Select(field => (
        Name: FieldName(field),
        Offset: (int)Marshal.OffsetOf(type, field.Name),
        Size: Marshal.SizeOf(field.FieldType)))
      .OrderBy(field => field.Offset)
      .ToArray();

    Assert.Equal(expected, actual);
    Assert.Equal(native.GetProperty("size").GetInt32(), Marshal.SizeOf(type));
  }

  [Fact]
  public void EveryMarshalledStruct_IsInTheContract()
  {
    var marshalled = typeof(NativeGuidGraphEngine)
      .GetNestedTypes(BindingFlags.NonPublic)
      .Where(type => type is { IsValueType: true, IsEnum: false }
        && type.Name.StartsWith("NativeGraph", StringComparison.Ordinal))
      .Select(type => type.Name)
      .Order(StringComparer.Ordinal);

    Assert.Equal(
      Entries("structs").Select(entry => ManagedStructName(Name(entry))).Order(StringComparer.Ordinal),
      marshalled);
  }

  [Theory]
  [MemberData(nameof(EnumNames))]
  public void Enum_HasTheNativeValues(string name)
  {
    Assert.True(Enums.TryGetValue(name, out var type), $"Map {name} to its C# enum in {nameof(Enums)}.");
    // C enums are int-sized.
    Assert.Equal(typeof(int), Enum.GetUnderlyingType(type));

    var native = Entries("enums").Single(entry => Name(entry) == name)
      .GetProperty("values").EnumerateArray()
      .Select(value => (Name: value.GetProperty("name").GetString()!, Value: value.GetProperty("value").GetInt32()))
      .ToArray();
    var prefix = CommonPrefix(native.Select(value => value.Name));

    // A C# name may drop a qualifier the C name needs, like
    // COORDINA_GRAPH_ASSIGNED_TO_REQUESTER and GraphAssignment.Requester.
    var expected = native
      .Select(value => (
        Name: Enum.GetNames(type).Single(managed => PascalCase(value.Name[prefix.Length..])
          .EndsWith(managed, StringComparison.Ordinal)),
        value.Value))
      .ToArray();
    var actual = Enum.GetNames(type)
      .Select(managed => (Name: managed, Value: (int)Enum.Parse(type, managed)))
      .ToArray();

    Assert.Equal(expected.OrderBy(value => value.Value), actual.OrderBy(value => value.Value));
  }

  private static JsonElement[] Entries(string kind) =>
    [.. Contract.Value.RootElement.GetProperty(kind).EnumerateArray()];

  private static string Name(JsonElement entry) => entry.GetProperty("name").GetString()!;

  private static Type NestedType(string name) =>
    typeof(NativeGuidGraphEngine).GetNestedType(name, BindingFlags.NonPublic)
      ?? throw new InvalidOperationException($"NativeGuidGraphEngine has no nested type {name}.");

  // coordina_graph_work_item is marshalled as NativeGraphWorkItem.
  private static string ManagedStructName(string nativeName) =>
    "NativeGraph" + PascalCase(nativeName[NativePrefix.Length..]);

  private static string PascalCase(string snakeCase) =>
    string.Concat(snakeCase.Split('_').Select(word =>
      char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));

  // A positional record struct stores High in <High>k__BackingField.
  private static string FieldName(FieldInfo field) =>
    BackingField().Match(field.Name) is { Success: true } match ? match.Groups[1].Value : field.Name;

  // The longest prefix shared by every name, up to and including an underscore.
  private static string CommonPrefix(IEnumerable<string> names)
  {
    var prefix = names.Aggregate((left, right) =>
      left[..left.Zip(right).TakeWhile(pair => pair.First == pair.Second).Count()]);

    return prefix[..(prefix.LastIndexOf('_') + 1)];
  }

  [GeneratedRegex("^<(.+)>k__BackingField$")]
  private static partial Regex BackingField();
}
