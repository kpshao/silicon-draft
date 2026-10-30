using System.Globalization;
using System.Text.Json;
using SiliconDraft.TestSupport;

namespace SiliconDraft.Core.Tests;

public sealed class WaferMapExportTests
{
    [Fact]
    public void Export_WritesWaferMapCsvThatAgreesWithManifestCounts()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(SmallGeometry("retain"));
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);

        Assert.True(result.Success);
        var waferMapPath = Assert.Single(result.WaferMapPaths);
        Assert.Equal(Path.Combine(outputDirectory, "LOT-0001_wafermap.csv"), waferMapPath);
        Assert.True(File.Exists(waferMapPath));

        var rows = ReadWaferMap(waferMapPath);
        Assert.Equal(101, rows.Count);
        Assert.Equal(61, rows.Count(row => row.Status == "valid"));
        Assert.Equal(40, rows.Count(row => row.Status == "partial"));
        Assert.Equal(61, rows.Count(row => row.Tested));
        Assert.All(rows, row => Assert.Equal(row.Status == "valid", row.Tested));

        using var manifest = JsonDocument.Parse(File.ReadAllText(result.ManifestPath!));
        var root = manifest.RootElement;
        Assert.Equal(4, root.GetProperty("manifestSchemaVersion").GetInt32());
        var datasetMetadata = root.GetProperty("dataset");
        Assert.Equal(61, datasetMetadata.GetProperty("grossDie").GetInt32());
        Assert.Equal(40, datasetMetadata.GetProperty("partialDie").GetInt32());
        Assert.Equal(40, datasetMetadata.GetProperty("partialDieRetained").GetInt32());
        Assert.Equal(
            rows.Count(row => row.Status == "valid"),
            datasetMetadata.GetProperty("grossDie").GetInt32());
        Assert.Equal(
            rows.Count(row => row.Tested),
            datasetMetadata.GetProperty("cp").GetProperty("testedDie").GetInt32());
        Assert.Equal(
            datasetMetadata.GetProperty("cp").GetProperty("passedDie").GetInt32(),
            datasetMetadata.GetProperty("netDie").GetInt32());
        Assert.Equal(
            rows.Count(row => row.Status == "partial"),
            datasetMetadata.GetProperty("partialDieRetained").GetInt32());

        var lot = Assert.Single(datasetMetadata.GetProperty("lots").EnumerateArray());
        Assert.Equal("LOT-0001", lot.GetProperty("lotId").GetString());
        Assert.Equal(61, lot.GetProperty("grossDie").GetInt32());
        Assert.Equal(
            datasetMetadata.GetProperty("netDie").GetInt32(),
            lot.GetProperty("netDie").GetInt32());
        var wafer = Assert.Single(lot.GetProperty("wafers").EnumerateArray());
        Assert.Equal("LOT-0001-W01", wafer.GetProperty("waferId").GetString());
        Assert.Equal(61, wafer.GetProperty("grossDie").GetInt32());
        Assert.Equal(
            lot.GetProperty("netDie").GetInt32(),
            wafer.GetProperty("netDie").GetInt32());
        Assert.Equal(40, wafer.GetProperty("partialDie").GetInt32());
        Assert.Equal(40, wafer.GetProperty("partialDieRetained").GetInt32());

        var resolvedWafer = root.GetProperty("resolvedConfig").GetProperty("wafer");
        Assert.Equal(100, resolvedWafer.GetProperty("diameterMm").GetDouble());
        Assert.Equal(0, resolvedWafer.GetProperty("edgeExclusionMm").GetDouble());
        Assert.Equal(10, resolvedWafer.GetProperty("pitchWidthMm").GetDouble());
        Assert.Equal("retain", resolvedWafer.GetProperty("partialDiePolicy").GetString());
        Assert.Equal("center", resolvedWafer.GetProperty("indexOrigin").GetString());
    }

    [Fact]
    public void Export_WithExcludePolicy_WritesOnlyValidDie()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(SmallGeometry("exclude"));
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);

        Assert.True(result.Success);
        var rows = ReadWaferMap(Assert.Single(result.WaferMapPaths));
        Assert.Equal(61, rows.Count);
        Assert.All(rows, row => Assert.Equal("valid", row.Status));

        using var manifest = JsonDocument.Parse(File.ReadAllText(result.ManifestPath!));
        var datasetMetadata = manifest.RootElement.GetProperty("dataset");
        Assert.Equal(61, datasetMetadata.GetProperty("grossDie").GetInt32());
        Assert.Equal(40, datasetMetadata.GetProperty("partialDie").GetInt32());
        Assert.Equal(0, datasetMetadata.GetProperty("partialDieRetained").GetInt32());
    }

    [Fact]
    public void Export_WithSeveralWafers_RepeatsTheMapPerWafer()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(
            SmallGeometry("exclude"),
            lotsJson: """[ { "id": "LOT-0001", "waferCount": 2 } ]""");
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);

        var rows = ReadWaferMap(Assert.Single(result.WaferMapPaths));
        Assert.Equal(122, rows.Count);
        Assert.Equal(61, rows.Count(row => row.WaferId == "LOT-0001-W01"));
        Assert.Equal(61, rows.Count(row => row.WaferId == "LOT-0001-W02"));

        using var manifest = JsonDocument.Parse(File.ReadAllText(result.ManifestPath!));
        var datasetMetadata = manifest.RootElement.GetProperty("dataset");
        Assert.Equal(2, datasetMetadata.GetProperty("waferCount").GetInt32());
        Assert.Equal(122, datasetMetadata.GetProperty("grossDie").GetInt32());
        Assert.Equal(
            datasetMetadata.GetProperty("cp").GetProperty("passedDie").GetInt32(),
            datasetMetadata.GetProperty("netDie").GetInt32());
        var wafers = datasetMetadata.GetProperty("lots")[0].GetProperty("wafers").EnumerateArray().ToArray();
        Assert.Equal(
            datasetMetadata.GetProperty("netDie").GetInt32(),
            wafers.Sum(wafer => wafer.GetProperty("netDie").GetInt32()));
    }

    [Fact]
    public void Export_WithTwoLots_WritesOneWaferMapPerLot()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(
            SmallGeometry("exclude"),
            lotsJson: """
                [ { "id": "LOT-0001", "waferCount": 1 }, { "id": "LOT-0002", "waferCount": 1 } ]
                """);
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);

        Assert.True(result.Success);
        Assert.Equal(2, result.WaferMapPaths.Count);
        Assert.EndsWith("LOT-0001_wafermap.csv", result.WaferMapPaths[0], StringComparison.Ordinal);
        Assert.EndsWith("LOT-0002_wafermap.csv", result.WaferMapPaths[1], StringComparison.Ordinal);
        Assert.Equal(61, ReadWaferMap(result.WaferMapPaths[0]).Count);
        Assert.Equal(61, ReadWaferMap(result.WaferMapPaths[1]).Count);

        using var manifest = JsonDocument.Parse(File.ReadAllText(result.ManifestPath!));
        Assert.Equal(122, manifest.RootElement.GetProperty("dataset").GetProperty("grossDie").GetInt32());
    }

    [Fact]
    public void Export_SameConfigAndSeed_ProducesIdenticalFiles()
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var dataset = Generate(SmallGeometry("retain"));

        var first = pipeline.Export(dataset, workspace.PathFor("run-a"));
        var second = pipeline.Export(dataset, workspace.PathFor("run-b"));

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(
            File.ReadAllBytes(first.ManifestPath!),
            File.ReadAllBytes(second.ManifestPath!));
        Assert.Equal(
            File.ReadAllBytes(Assert.Single(first.WaferMapPaths)),
            File.ReadAllBytes(Assert.Single(second.WaferMapPaths)));
    }

    [Fact]
    public void Export_WhenWaferMapAlreadyExists_RequiresOverwriteConfirmation()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(SmallGeometry("exclude"));
        var outputDirectory = workspace.PathFor("dataset");
        Directory.CreateDirectory(outputDirectory);
        var waferMapPath = Path.Combine(outputDirectory, "LOT-0001_wafermap.csv");
        File.WriteAllText(waferMapPath, "stale\n");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);

        Assert.False(result.Success);
        Assert.Contains("already exists", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LOT-0001_wafermap.csv", result.Error, StringComparison.OrdinalIgnoreCase);

        var overwritten = new GenerationPipeline().Export(dataset, outputDirectory, overwrite: true);
        Assert.True(overwritten.Success);
        Assert.DoesNotContain("stale", File.ReadAllText(waferMapPath));
    }

    private static string SmallGeometry(string partialDiePolicy) => $$"""
        {
          "diameterMm": 100,
          "edgeExclusionMm": 0,
          "die": { "widthMm": 10, "heightMm": 10, "scribeMm": 0 },
          "partialDiePolicy": "{{partialDiePolicy}}"
        }
        """;

    private static Dataset Generate(string waferJson, string? lotsJson = null)
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var json = lotsJson is null
            ? TestConfigs.WithWafer(waferJson)
            : TestConfigs.WithWafer(waferJson, lotsJson);
        return pipeline.Generate(pipeline.Validate(workspace.WriteConfig(json)).Config!);
    }

    private static List<WaferMapRow> ReadWaferMap(string path)
    {
        var lines = File.ReadAllLines(path);
        Assert.Equal(
            "lotId,waferId,dieX,dieY,centerXmm,centerYmm,status,tested",
            lines[0]);
        return lines
            .Skip(1)
            .Select(line =>
            {
                var fields = line.Split(',');
                Assert.Equal(8, fields.Length);
                return new WaferMapRow(
                    fields[0],
                    fields[1],
                    int.Parse(fields[2], CultureInfo.InvariantCulture),
                    int.Parse(fields[3], CultureInfo.InvariantCulture),
                    double.Parse(fields[4], CultureInfo.InvariantCulture),
                    double.Parse(fields[5], CultureInfo.InvariantCulture),
                    fields[6],
                    bool.Parse(fields[7]));
            })
            .ToList();
    }

    private sealed record WaferMapRow(
        string LotId,
        string WaferId,
        int DieX,
        int DieY,
        double CenterXmm,
        double CenterYmm,
        string Status,
        bool Tested);
}
