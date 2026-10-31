using System.Text.Json;
using SiliconDraft.TestSupport;

namespace SiliconDraft.Core.Tests;

/// <summary>
/// Locks the default catalog v1 contract: the item counts the spec requires, the metadata
/// every item declares, how default failure Soft Bins reconcile with the Hard/Soft Bins,
/// the catalog versions recorded in the manifest, and config overrides on catalog items.
/// </summary>
public sealed class CatalogTests
{
    private const string SmallGeometry = """
        {
          "diameterMm": 100,
          "edgeExclusionMm": 0,
          "die": { "widthMm": 10, "heightMm": 10, "scribeMm": 0 }
        }
        """;

    [Fact]
    public void DefaultCatalogs_ContainTheSpecItemCounts()
    {
        var dataset = Generate(TestConfigs.Valid);

        Assert.Equal(20, dataset.CpProgram.Items.OfType<NumericTestItem>().Count());
        Assert.Equal(3, dataset.CpProgram.Items.OfType<FunctionalTestItem>().Count());
        Assert.Equal(15, dataset.FtProgram.Items.OfType<NumericTestItem>().Count());
        Assert.Equal(3, dataset.FtProgram.Items.OfType<FunctionalTestItem>().Count());
        Assert.Equal(12, dataset.WatProgram.Items.Count);
    }

    [Fact]
    public void EveryCatalogItem_DeclaresBusinessMeaningUnitLimitsAndDefaultDistribution()
    {
        var dataset = Generate(TestConfigs.Valid);

        foreach (var item in dataset.CpProgram.Items.Concat(dataset.FtProgram.Items))
        {
            AssertDeclaresMetadata(item);
            Assert.InRange(item.FailSoftBin, 1, 65535);

            switch (item)
            {
                case NumericTestItem numeric:
                    Assert.True(
                        double.IsFinite(numeric.Distribution.Mean),
                        $"'{numeric.Name}' has a non-finite mean");
                    Assert.True(
                        numeric.Distribution.Sigma > 0,
                        $"'{numeric.Name}' has a non-positive sigma");
                    break;
                case FunctionalTestItem functional:
                    Assert.True(
                        functional.PassRate > 0 && functional.PassRate < 1,
                        $"'{functional.Name}' has an out-of-range pass rate");
                    break;
                default:
                    Assert.Fail($"'{item.Name}' has an unknown item type");
                    break;
            }
        }

        foreach (var item in dataset.WatProgram.Items)
        {
            AssertDeclaresMetadata(item);
            Assert.True(
                double.IsFinite(item.Distribution.Mean),
                $"'{item.Name}' has a non-finite mean");
            Assert.True(item.Distribution.Sigma > 0, $"'{item.Name}' has a non-positive sigma");
        }
    }

    private static void AssertDeclaresMetadata(TestItem item) =>
        AssertDeclaresMetadata(
            item.Name, item.Unit, item.TestNumber, item.SpecLimits, item.TestLimits);

    private static void AssertDeclaresMetadata(WatTestItem item) =>
        AssertDeclaresMetadata(
            item.Name, item.Unit, item.TestNumber, item.SpecLimits, item.TestLimits);

    private static void AssertDeclaresMetadata(
        string name, string unit, int testNumber, Limits specLimits, Limits testLimits)
    {
        Assert.NotEmpty(name);
        Assert.NotEmpty(unit);
        Assert.True(testNumber > 0, $"'{name}' has no test number");
        Assert.True(specLimits.Low < specLimits.High, $"'{name}' has inverted spec limits");
        Assert.True(testLimits.Low < testLimits.High, $"'{name}' has inverted test limits");
        // The default catalog keeps Test Limits inside Spec Limits. The schema does not
        // require that, so this locks the catalog's convention, not a validation rule.
        Assert.True(
            specLimits.Low <= testLimits.Low && testLimits.High <= specLimits.High,
            $"'{name}' test limits must lie within its spec limits");
    }

    [Fact]
    public void DefaultCatalogs_UseUniqueNamesAndTestNumbers()
    {
        var dataset = Generate(TestConfigs.Valid);

        foreach (var program in new[] { dataset.CpProgram, dataset.FtProgram })
        {
            Assert.Equal(
                program.Items.Count,
                program.Items.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(
                program.Items.Count,
                program.Items.Select(item => item.TestNumber).Distinct().Count());
        }

        Assert.Equal(
            dataset.WatProgram.Items.Count,
            dataset.WatProgram.Items.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            dataset.WatProgram.Items.Count,
            dataset.WatProgram.Items.Select(item => item.TestNumber).Distinct().Count());
    }

    [Fact]
    public void DefaultFailureSoftBins_ReconcileWithTheHardAndSoftBins()
    {
        var dataset = Generate(TestConfigs.Valid);
        var bins = dataset.Config.Cp.Bins;

        var cpFailSoftBins = dataset.CpProgram.Items.Select(item => item.FailSoftBin).ToArray();
        Assert.Equal(cpFailSoftBins.Length, cpFailSoftBins.Distinct().Count());
        Assert.DoesNotContain(bins.PassSoftBin, cpFailSoftBins);

        var ftFailSoftBins = dataset.FtProgram.Items.Select(item => item.FailSoftBin).ToArray();
        Assert.Equal(ftFailSoftBins.Length, ftFailSoftBins.Distinct().Count());
        Assert.DoesNotContain(bins.PassSoftBin, ftFailSoftBins);

        // The manifest declares the pass Soft Bin plus one per Test Item, each named after
        // the item whose failure it reports.
        Assert.Equal(dataset.CpProgram.Items.Count + 1, dataset.Cp.SoftBins.Count);
        Assert.Equal(dataset.FtProgram.Items.Count + 1, dataset.Ft.SoftBins.Count);
        Assert.All(dataset.CpProgram.Items, item => Assert.Contains(
            dataset.Cp.SoftBins,
            bin => bin.Number == item.FailSoftBin && bin.Name == item.Name && !bin.Pass));
        Assert.All(dataset.FtProgram.Items, item => Assert.Contains(
            dataset.Ft.SoftBins,
            bin => bin.Number == item.FailSoftBin && bin.Name == item.Name && !bin.Pass));

        // The first failing item decides the outcome: a Numeric failure lands in the
        // Parametric Fail Hard Bin, a Functional failure in the Functional Fail Hard Bin.
        var cpResults = dataset.Lots
            .SelectMany(lot => lot.Wafers)
            .SelectMany(wafer => wafer.CpResults)
            .ToArray();
        Assert.Contains(cpResults, result => !result.Passed);
        foreach (var result in cpResults.Where(result => !result.Passed))
        {
            var failure = result.FirstFailure!;
            Assert.Equal(failure.Item.FailSoftBin, result.SoftBin);
            Assert.Equal(
                failure.Item is FunctionalTestItem
                    ? bins.FunctionalFailHardBin
                    : bins.ParametricFailHardBin,
                result.HardBin);
        }

        var ftUnits = dataset.Lots.SelectMany(lot => lot.FtUnits).ToArray();
        Assert.Contains(ftUnits, unit => !unit.Passed);
        foreach (var unit in ftUnits.Where(unit => !unit.Passed))
        {
            var failure = unit.FirstFailure!;
            Assert.Equal(failure.Item.FailSoftBin, unit.SoftBin);
            Assert.Equal(
                failure.Item is FunctionalTestItem
                    ? bins.FunctionalFailHardBin
                    : bins.ParametricFailHardBin,
                unit.HardBin);
        }
    }

    [Fact]
    public void ExportedManifest_RecordsTheCatalogVersions()
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var config = pipeline.Validate(
            workspace.WriteConfig(TestConfigs.WithWafer(SmallGeometry))).Config!;
        var dataset = pipeline.Generate(config);
        var result = pipeline.Export(dataset, workspace.PathFor("dataset"));
        Assert.True(result.Success);

        Assert.Equal("cp-catalog-v3", dataset.CpProgram.CatalogVersion);
        Assert.Equal("ft-catalog-v2", dataset.FtProgram.CatalogVersion);
        Assert.Equal("wat-catalog-v2", dataset.WatProgram.CatalogVersion);

        using var manifest = JsonDocument.Parse(File.ReadAllText(result.ManifestPath!));
        var exported = manifest.RootElement.GetProperty("dataset");
        Assert.Equal(
            dataset.CpProgram.CatalogVersion,
            exported.GetProperty("cp").GetProperty("catalogVersion").GetString());
        Assert.Equal(
            dataset.FtProgram.CatalogVersion,
            exported.GetProperty("ft").GetProperty("catalogVersion").GetString());
        Assert.Equal(
            dataset.WatProgram.CatalogVersion,
            exported.GetProperty("wat").GetProperty("catalogVersion").GetString());
    }

    [Fact]
    public void Validate_WithOverridesForNewCatalogItems_RecordsThemInTheResolvedConfig()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(TestConfigs.WithCp(
            """
            {
              "testItems": [
                {
                  "name": "Dibl",
                  "specLimits": { "low": 30, "high": 70 },
                  "testLimits": { "low": 40, "high": 60 },
                  "failSoftBin": 210
                },
                {
                  "name": "Vfb",
                  "testLimits": { "low": -0.9, "high": -0.8 }
                },
                {
                  "name": "Mbist",
                  "failSoftBin": 211
                }
              ]
            }
            """));

        var result = new GenerationPipeline().Validate(configPath);

        Assert.True(result.IsValid);
        Assert.Collection(
            result.Config!.Cp.TestItems,
            item =>
            {
                Assert.Equal("Dibl", item.Name);
                Assert.Equal(new Limits(30, 70), item.SpecLimits);
                Assert.Equal(new Limits(40, 60), item.TestLimits);
                Assert.Equal(210, item.FailSoftBin);
            },
            item =>
            {
                Assert.Equal("Vfb", item.Name);
                Assert.Null(item.SpecLimits);
                Assert.Equal(new Limits(-0.9, -0.8), item.TestLimits);
                Assert.Null(item.FailSoftBin);
            },
            item =>
            {
                Assert.Equal("Mbist", item.Name);
                Assert.Equal(211, item.FailSoftBin);
            });
    }

    [Fact]
    public void Generate_WithOverriddenNewCatalogItem_UsesTheEffectiveDefinition()
    {
        var dataset = Generate(TestConfigs.WithCp(
            """
            {
              "testItems": [
                {
                  "name": "Dibl",
                  "testLimits": { "low": 49.99, "high": 50.01 },
                  "failSoftBin": 210
                }
              ]
            }
            """));

        var dibl = dataset.CpProgram.Items.Single(item => item.Name == "Dibl");
        Assert.Equal(new Limits(49.99, 50.01), dibl.TestLimits);
        Assert.Equal(210, dibl.FailSoftBin);
        Assert.Contains(
            dataset.Lots.SelectMany(lot => lot.Wafers).SelectMany(wafer => wafer.CpResults),
            result => !result.Passed && result.HardBin == dataset.Config.Cp.Bins.ParametricFailHardBin);
    }

    private static Dataset Generate(string json)
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var config = pipeline.Validate(workspace.WriteConfig(json)).Config!;
        return pipeline.Generate(config);
    }
}
