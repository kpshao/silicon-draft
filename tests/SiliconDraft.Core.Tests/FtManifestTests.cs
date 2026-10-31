using System.Text.Json;
using SiliconDraft.TestSupport;

namespace SiliconDraft.Core.Tests;

public sealed class FtManifestTests
{
    private const string SmallGeometry = """
        {
          "diameterMm": 100,
          "edgeExclusionMm": 0,
          "die": { "widthMm": 10, "heightMm": 10, "scribeMm": 0 }
        }
        """;

    private const string TwoLots = """
        [ { "id": "LOT-0001", "waferCount": 1 }, { "id": "LOT-0002", "waferCount": 1 } ]
        """;

    [Fact]
    public void Export_WithDefaultFtSection_ManifestShowsOneUnitPerCpPassingDie()
    {
        var (dataset, manifest) = Export();

        Assert.Equal(8, manifest.RootElement.GetProperty("manifestSchemaVersion").GetInt32());
        var resolvedFt = manifest.RootElement.GetProperty("resolvedConfig").GetProperty("ft");
        Assert.Equal(0, resolvedFt.GetProperty("assemblyLoss").GetInt32());
        Assert.Equal(0, resolvedFt.GetProperty("escapeRate").GetDouble());

        var ft = manifest.RootElement.GetProperty("dataset").GetProperty("ft");
        Assert.Equal("ft-catalog-v2", ft.GetProperty("catalogVersion").GetString());
        Assert.Equal(dataset.Cp.PassedDie, ft.GetProperty("unitCount").GetInt32());
        Assert.Equal(dataset.Cp.PassedDie, ft.GetProperty("cpPassedUnits").GetInt32());
        Assert.Equal(0, ft.GetProperty("escapeUnits").GetInt32());
        Assert.Equal(0, ft.GetProperty("assemblyLoss").GetInt32());
        Assert.Equal(
            ft.GetProperty("unitCount").GetInt32(),
            ft.GetProperty("hardBins").EnumerateArray().Sum(bin => bin.GetProperty("count").GetInt32()));
    }

    [Fact]
    public void Export_WithAssemblyLossAndEscapes_ManifestFtCountsReconcile()
    {
        var (dataset, manifest) = Export("""{ "assemblyLoss": 2, "escapeRate": 0.5 }""");

        var datasetElement = manifest.RootElement.GetProperty("dataset");
        var cp = datasetElement.GetProperty("cp");
        var ft = datasetElement.GetProperty("ft");
        var unitCount = ft.GetProperty("unitCount").GetInt32();
        var assemblyLoss = ft.GetProperty("assemblyLoss").GetInt32();
        var escapeUnits = ft.GetProperty("escapeUnits").GetInt32();
        var cpPassedUnits = ft.GetProperty("cpPassedUnits").GetInt32();
        var passedUnits = ft.GetProperty("passedUnits").GetInt32();

        Assert.Equal(2, assemblyLoss);
        Assert.Equal(
            manifest.RootElement.GetProperty("resolvedConfig").GetProperty("ft")
                .GetProperty("assemblyLoss").GetInt32(),
            assemblyLoss);
        Assert.Equal(unitCount, dataset.Ft.UnitCount);
        Assert.Equal(unitCount, cpPassedUnits + escapeUnits);
        Assert.Equal(
            cp.GetProperty("passedDie").GetInt32() - assemblyLoss + escapeUnits,
            unitCount);
        Assert.Equal(passedUnits, ft.GetProperty("hardBins").EnumerateArray()
            .Single(bin => bin.GetProperty("pass").GetBoolean())
            .GetProperty("count").GetInt32());
        Assert.Equal(unitCount, ft.GetProperty("hardBins").EnumerateArray().Sum(bin => bin.GetProperty("count").GetInt32()));
        Assert.Equal(unitCount, ft.GetProperty("softBins").EnumerateArray().Sum(bin => bin.GetProperty("count").GetInt32()));
        Assert.Equal(
            Math.Round((double)passedUnits / unitCount, 6),
            ft.GetProperty("yield").GetDouble());

        var lotLosses = 0;
        foreach (var lot in datasetElement.GetProperty("lots").EnumerateArray())
        {
            var lotId = lot.GetProperty("lotId").GetString();
            var lotFt = lot.GetProperty("ft");
            lotLosses += lotFt.GetProperty("assemblyLoss").GetInt32();
            Assert.Equal($"ALOT-{lotId}", lot.GetProperty("assemblyLot").GetString());
            Assert.Equal(
                lot.GetProperty("cp").GetProperty("passedDie").GetInt32() -
                lotFt.GetProperty("assemblyLoss").GetInt32() +
                lotFt.GetProperty("escapeUnits").GetInt32(),
                lotFt.GetProperty("unitCount").GetInt32());
            Assert.Equal(
                lotFt.GetProperty("unitCount").GetInt32(),
                lot.GetProperty("wafers").EnumerateArray()
                    .Sum(wafer => wafer.GetProperty("ftUnits").GetInt32()));
            Assert.Equal(
                lotFt.GetProperty("escapeUnits").GetInt32(),
                lot.GetProperty("wafers").EnumerateArray()
                    .Sum(wafer => wafer.GetProperty("ftEscapeUnits").GetInt32()));
        }

        Assert.Equal(assemblyLoss, lotLosses);
    }

    [Fact]
    public void Export_ManifestEscapeUnitsMatchEscapeFtUnits()
    {
        var (dataset, manifest) = Export("""{ "escapeRate": 0.5 }""");

        var expectedEscapes = dataset.Lots.SelectMany(lot => lot.FtUnits)
            .Count(unit => unit.IsEscape);
        var ft = manifest.RootElement.GetProperty("dataset").GetProperty("ft");
        Assert.Equal(expectedEscapes, ft.GetProperty("escapeUnits").GetInt32());
    }

    private static (Dataset Dataset, JsonDocument Manifest) Export(string? ftJson = null)
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var json = ftJson is null
            ? TestConfigs.WithWafer(SmallGeometry, TwoLots)
            : TestConfigs.WithFt(ftJson, SmallGeometry, TwoLots);
        var config = pipeline.Validate(workspace.WriteConfig(json)).Config!;
        var dataset = pipeline.Generate(config);
        var result = pipeline.Export(dataset, workspace.PathFor("dataset"));
        Assert.True(result.Success);

        // The workspace is disposed with the test, so keep only the parsed document.
        return (dataset, JsonDocument.Parse(File.ReadAllText(result.ManifestPath!)));
    }
}
