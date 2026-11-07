using Coordina.Quality.Duplication;
using Coordina.Quality.Tests.Support;

namespace Coordina.Quality.Tests.Duplication;

public sealed class DuplicationTests
{
  private const string Root = "/repo";

  [Fact]
  public void Parse_ReadsClonesWithRepositoryRelativePaths()
  {
    var clones = DuplicationDetector.Parse(
      """
      {
        "duplicates": [
          {
            "lines": 12,
            "firstFile": { "name": "/repo/src/web/e2e/a.spec.ts", "start": 15, "end": 26 },
            "secondFile": { "name": "src/web/e2e/b.spec.ts", "start": 7, "end": 18 }
          },
          {
            "lines": 9,
            "firstFile": { "name": "/repo/src/api/core/Persistence/Migrations/Init.cs", "start": 1, "end": 9 },
            "secondFile": { "name": "/repo/src/api/core/Other.cs", "start": 1, "end": 9 }
          }
        ]
      }
      """,
      Root);

    Assert.Equal(
      [new CodeClone("src/web/e2e/a.spec.ts", 15, "src/web/e2e/b.spec.ts", 7, 12)],
      clones);
  }

  [Fact]
  public void Parse_WithoutDuplicates_ReturnsNothing()
  {
    Assert.Empty(DuplicationDetector.Parse("{}", Root));
  }

  [Fact]
  public void Violations_AreReportedOnTheCheckedSidesOnly()
  {
    CodeClone[] clones =
    [
      new("src/a.ts", 3, "src/b.ts", 8, 10),
      new("src/c.ts", 1, "src/c.ts", 40, 10)
    ];

    var onlyA = DuplicationRule.ToViolations(clones, new HashSet<string>(["src/a.ts"]));
    var everything = DuplicationRule.ToViolations(clones, new HashSet<string>(["src/a.ts", "src/b.ts", "src/c.ts"]));

    var violation = Assert.Single(onlyA);
    Assert.Equal(("src/a.ts", 3, "This block is repeated in src/b.ts."), (violation.Path, violation.Line, violation.Message));
    Assert.Equal(4, everything.Count);
    Assert.Contains(everything, item => item.Path == "src/c.ts" && item.Message == "This block is repeated elsewhere in this file.");
  }

  [Fact]
  public void FindClones_RunsJscpdOnTheRepository()
  {
    var root = Directory.CreateTempSubdirectory("quality-jscpd-").FullName;

    try
    {
      RepositoryFixture.LinkWebDependencies(root);
      var block = string.Join('\n', Enumerable.Range(0, 12).Select(index =>
        $"export const value{index} = compute(\"{index}\", {index} * 2, [{index}, {index + 1}])"));
      Write(root, "src/web/src/features/a/first.ts", block);
      Write(root, "src/web/src/features/b/second.ts", block);
      Write(root, "src/web/src/features/c/unique.ts", "export const unique = 1\n");

      var clone = Assert.Single(new DuplicationDetector(root).FindClones());

      Assert.Equal(
        ["src/web/src/features/a/first.ts", "src/web/src/features/b/second.ts"],
        new[] { clone.FirstPath, clone.SecondPath }.Order(StringComparer.Ordinal));
    }
    finally
    {
      Directory.Delete(root, recursive: true);
    }
  }

  private static void Write(string root, string relativePath, string content)
  {
    var path = Path.Combine(root, relativePath);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, content + "\n");
  }
}
