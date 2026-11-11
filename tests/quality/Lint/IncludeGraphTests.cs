using Coordina.Quality.Lint;

namespace Coordina.Quality.Tests.Lint;

public sealed class IncludeGraphTests
{
  private static readonly Dictionary<string, string> Sources = new(StringComparer.Ordinal)
  {
    ["src/graph-engine/include/coordina_graph_engine.h"] = "#include <stdint.h>\n",
    ["src/graph-engine/src/core/graph_types.h"] = "#include \"coordina_graph_engine.h\"\n",
    ["src/graph-engine/src/core/graph_types.cpp"] = "#include \"core/graph_types.h\"\n",
    ["src/graph-engine/src/core/graph_snapshot.h"] = "#include \"core/graph_types.h\"\n",
    ["src/graph-engine/src/core/graph_snapshot.cpp"] = "#include \"graph_snapshot.h\"\n",
    ["src/graph-engine/src/algorithms/critical_path.h"] = "#include \"core/graph_snapshot.h\"\n",
    ["src/graph-engine/src/algorithms/critical_path.cpp"] = "  # include \"algorithms/critical_path.h\" // own header\n#include <vector>\n",
    ["src/graph-engine/src/planning/work_score.cpp"] = "#include \"../core/graph_types.h\"\n",
    ["tests/graph-engine/graph_engine_tests.cpp"] = "#include \"coordina_graph_engine.h\"\n#include \"missing.h\"\n"
  };

  private static readonly IncludeGraph Graph = IncludeGraph.Build(
    Sources.Keys,
    ["src/graph-engine/include", "src/graph-engine/src"],
    path => Sources[path]);

  [Fact]
  public void ChangedUnit_IsAnalysedAlone()
  {
    Assert.Equal(
      ["src/graph-engine/src/core/graph_snapshot.cpp"],
      Graph.UnitsCovering(["src/graph-engine/src/core/graph_snapshot.cpp"]));
  }

  [Fact]
  public void ChangedHeader_IsAnalysedThroughEveryFileThatIncludesIt()
  {
    Assert.Equal(
      ["src/graph-engine/src/algorithms/critical_path.cpp", "src/graph-engine/src/core/graph_snapshot.cpp"],
      Graph.UnitsCovering(["src/graph-engine/src/core/graph_snapshot.h"]));
  }

  [Fact]
  public void PublicHeader_ReachesEveryUnitThroughTheHeadersInBetween()
  {
    Assert.Equal(
      [
        "src/graph-engine/src/algorithms/critical_path.cpp",
        "src/graph-engine/src/core/graph_snapshot.cpp",
        "src/graph-engine/src/core/graph_types.cpp",
        "src/graph-engine/src/planning/work_score.cpp",
        "tests/graph-engine/graph_engine_tests.cpp"
      ],
      Graph.UnitsCovering(["src/graph-engine/include/coordina_graph_engine.h"]));
  }

  [Fact]
  public void FileOutsideTheEngine_SelectsNothing()
  {
    Assert.Empty(Graph.UnitsCovering(["src/api/Program.cs"]));
  }
}
