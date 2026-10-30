using SiliconDraft.TestSupport;

namespace SiliconDraft.Core.Tests;

/// <summary>
/// Locks the complete output of one small CP Dataset. The golden files were reviewed
/// when the CP loop landed; regenerating them is a deliberate change to the export format
/// or to the catalog, seed, or geometry used here.
/// </summary>
public sealed class GoldenExportTests
{
    [Fact]
    public void Export_MinimalCpDataset_MatchesGoldenFiles()
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var configPath = workspace.WriteConfig(GoldenConfig);
        var config = pipeline.Validate(configPath).Config!;
        var dataset = pipeline.Generate(config);
        var outputDirectory = workspace.PathFor("dataset");

        var result = pipeline.Export(dataset, outputDirectory);

        Assert.True(result.Success);
        Assert.Equal(61, dataset.DieTotals.GrossDie);
        Assert.True(dataset.Cp.PassedDie < dataset.Cp.TestedDie);
        AssertGolden("_truth/manifest.json", result.ManifestPath!);
        AssertGolden("LOT-0001_wafermap.csv", Assert.Single(result.WaferMapPaths));
        AssertGolden("LOT-0001_CP.stdf", Assert.Single(result.CpStdfPaths));
        AssertGolden("LOT-0001_FT.stdf", Assert.Single(result.FtPaths));
        AssertGolden("LOT-0001_WAT_long.txt", Assert.Single(result.WatPaths));
    }

    private static void AssertGolden(string relativePath, string actualPath)
    {
        var expectedPath = Path.Combine(AppContext.BaseDirectory, "Golden", relativePath);
        Assert.True(File.Exists(expectedPath), $"missing golden file '{expectedPath}'");

        var expected = File.ReadAllBytes(expectedPath);
        var actual = File.ReadAllBytes(actualPath);
        if (expected.AsSpan().SequenceEqual(actual))
        {
            return;
        }

        var difference = FirstDifference(expected, actual);
        Assert.Fail(
            $"'{relativePath}' differs from its golden file at byte {difference} " +
            $"(golden {expected.Length} bytes, actual {actual.Length} bytes). " +
            "Review the change and regenerate the golden file deliberately if intended.");
    }

    private static int FirstDifference(byte[] expected, byte[] actual)
    {
        var length = Math.Min(expected.Length, actual.Length);
        for (var index = 0; index < length; index++)
        {
            if (expected[index] != actual[index])
            {
                return index;
            }
        }

        return length;
    }

    private const string GoldenConfig = """
        {
          "schemaVersion": 1,
          "product": { "name": "DemoProduct" },
          "seed": 42,
          "wafer": {
            "diameterMm": 100,
            "edgeExclusionMm": 0,
            "die": { "widthMm": 10, "heightMm": 10, "scribeMm": 0 }
          },
          "lots": [ { "id": "LOT-0001", "waferCount": 1 } ],
          "output": { "directory": "out" }
        }
        """;
}
