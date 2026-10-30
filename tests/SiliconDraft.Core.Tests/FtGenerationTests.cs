using SiliconDraft.TestSupport;

namespace SiliconDraft.Core.Tests;

public sealed class FtGenerationTests
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
    public void Generate_ByDefault_CreatesOneFtUnitPerCpPassingDie()
    {
        var dataset = Generate();
        var units = AllFtUnits(dataset);
        var cpPassing = AllCpResults(dataset).Where(result => result.Passed).ToArray();
        var cpByKey = CpResultsByKey(dataset);

        Assert.Equal(cpPassing.Length, units.Count);
        Assert.Equal(units.Count, units.Select(unit => unit.Id).Distinct().Count());
        Assert.All(units, unit =>
        {
            Assert.False(unit.IsEscape);
            Assert.Equal($"{unit.SourceWaferId}:{unit.SourceDie.X},{unit.SourceDie.Y}", unit.Id);
            Assert.True(cpByKey[(unit.SourceWaferId, unit.SourceDie.X, unit.SourceDie.Y)].Passed);
            Assert.Equal(dataset.FtProgram.Items.Count, unit.Tests.Count);
        });
    }

    [Fact]
    public void Generate_DefaultFtProgram_DefinesItemsWithUnitsAndLimits()
    {
        var dataset = Generate();

        var program = dataset.FtProgram;
        Assert.Equal("ft-catalog-v1", program.CatalogVersion);
        Assert.Contains(program.Items, item => item is NumericTestItem);
        Assert.Contains(program.Items, item => item is FunctionalTestItem);
        Assert.All(program.Items, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Name));
            Assert.False(string.IsNullOrWhiteSpace(item.Unit));
            Assert.True(item.SpecLimits.Low < item.SpecLimits.High);
            Assert.True(item.TestLimits.Low < item.TestLimits.High);
        });
        Assert.Equal(1, program.Bins.PassHardBin);
        Assert.Equal(1, program.Bins.PassSoftBin);
        Assert.Equal(2, program.Bins.ParametricFailHardBin);
        Assert.Equal(3, program.Bins.FunctionalFailHardBin);
    }

    [Fact]
    public void Generate_AssignsFtBinsFromTheFirstFailingTestItem()
    {
        var dataset = Generate("""{ "escapeRate": 1 }""", TwoLots);
        var bins = dataset.FtProgram.Bins;

        Assert.Contains(AllFtUnits(dataset), unit => !unit.Passed);
        foreach (var unit in AllFtUnits(dataset))
        {
            if (unit.Passed)
            {
                Assert.Equal(bins.PassHardBin, unit.HardBin);
                Assert.Equal(bins.PassSoftBin, unit.SoftBin);
                continue;
            }

            var failure = unit.Tests.First(test => !test.Passed);
            Assert.Equal(
                failure.Item is FunctionalTestItem
                    ? bins.FunctionalFailHardBin
                    : bins.ParametricFailHardBin,
                unit.HardBin);
            Assert.Equal(failure.Item.FailSoftBin, unit.SoftBin);
        }
    }

    [Fact]
    public void Generate_WithAssemblyLoss_DropsThatManyCpPassingDieAcrossTheDataset()
    {
        var dataset = Generate("""{ "assemblyLoss": 3 }""", TwoLots);
        var units = AllFtUnits(dataset);
        var cpPassingIds = CpResultsByKey(dataset)
            .Where(entry => entry.Value.Passed)
            .Select(entry => $"{entry.Key.WaferId}:{entry.Key.X},{entry.Key.Y}")
            .ToArray();
        var unitIds = units.Select(unit => unit.Id).ToHashSet();

        Assert.Equal(cpPassingIds.Length - 3, units.Count);
        Assert.All(units, unit => Assert.False(unit.IsEscape));
        Assert.Equal(3, cpPassingIds.Count(id => !unitIds.Contains(id)));
    }

    [Fact]
    public void Generate_WithEscapeRateOne_AdmitsEveryCpFailingDie()
    {
        var dataset = Generate("""{ "escapeRate": 1 }""");
        var units = AllFtUnits(dataset);
        var cpByKey = CpResultsByKey(dataset);

        Assert.Equal(AllCpResults(dataset).Count, units.Count);
        Assert.All(units, unit =>
        {
            var cp = cpByKey[(unit.SourceWaferId, unit.SourceDie.X, unit.SourceDie.Y)];
            Assert.Equal(!cp.Passed, unit.IsEscape);
            Assert.Equal(cp.Passed, unit.CpPassed);
        });
    }

    [Fact]
    public void Generate_WithAssemblyLossAndEscapes_KeepsEveryUnitTraceableToOneDie()
    {
        var dataset = Generate("""{ "assemblyLoss": 2, "escapeRate": 0.5 }""", TwoLots);

        Assert.All(dataset.Lots, lot =>
        {
            var cpResults = lot.Wafers
                .SelectMany(wafer => wafer.CpResults.Select(result => (WaferId: wafer.Id, Result: result)))
                .ToArray();
            var cpByKey = cpResults.ToDictionary(
                entry => (entry.WaferId, entry.Result.Die.X, entry.Result.Die.Y),
                entry => entry.Result);

            Assert.Equal(lot.FtUnits.Count, lot.FtUnits.Select(unit => unit.Id).Distinct().Count());
            Assert.All(lot.FtUnits, unit =>
            {
                var cp = cpByKey[(unit.SourceWaferId, unit.SourceDie.X, unit.SourceDie.Y)];
                Assert.Equal(cp.Passed, !unit.IsEscape);
            });

            Assert.True(lot.FtUnits.Count(unit => unit.IsEscape) <= cpResults.Count(entry => !entry.Result.Passed));
        });

        Assert.Equal(
            AllCpResults(dataset).Count(result => result.Passed) - 2,
            AllFtUnits(dataset).Count(unit => !unit.IsEscape));
    }

    [Fact]
    public void Generate_WithSameConfigAndSeed_ProducesStableFtUnits()
    {
        var first = Generate("""{ "assemblyLoss": 2, "escapeRate": 0.5 }""", TwoLots);
        var second = Generate("""{ "assemblyLoss": 2, "escapeRate": 0.5 }""", TwoLots);

        Assert.Equal(Signatures(first), Signatures(second));
    }

    [Fact]
    public void Generate_WithOverriddenCpBins_UsesTheSameBinNumberingAtFt()
    {
        var dataset = Generate(
            cpJson: """
                {
                  "bins": {
                    "passHardBin": 10,
                    "passSoftBin": 11,
                    "parametricFailHardBin": 12,
                    "functionalFailHardBin": 13
                  }
                }
                """);

        Assert.Equal(new TestBins(10, 11, 12, 13), dataset.FtProgram.Bins);
    }

    [Fact]
    public void Generate_WithAssemblyLossGreaterThanCpPassingDie_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => Generate("""{ "assemblyLoss": 1000 }"""));

        Assert.Contains("assemblyLoss", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string[] Signatures(Dataset dataset) =>
        AllFtUnits(dataset)
            .Select(unit =>
                $"{unit.Id}|{unit.HardBin}|{unit.SoftBin}|" +
                string.Join(";", unit.Tests.Select(test => test.Value.ToString("R"))))
            .ToArray();

    private static Dictionary<(string WaferId, int X, int Y), CpDieResult> CpResultsByKey(
        Dataset dataset) =>
        dataset.Lots
            .SelectMany(lot => lot.Wafers)
            .SelectMany(wafer => wafer.CpResults.Select(result => (WaferId: wafer.Id, Result: result)))
            .ToDictionary(entry => (entry.WaferId, entry.Result.Die.X, entry.Result.Die.Y), entry => entry.Result);

    private static List<CpDieResult> AllCpResults(Dataset dataset) =>
        dataset.Lots
            .SelectMany(lot => lot.Wafers)
            .SelectMany(wafer => wafer.CpResults)
            .ToList();

    private static List<FtUnit> AllFtUnits(Dataset dataset) =>
        dataset.Lots.SelectMany(lot => lot.FtUnits).ToList();

    private static Dataset Generate(
        string? ftJson = null, string lotsJson = TwoLots, string? cpJson = null)
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var json = (ftJson, cpJson) switch
        {
            (not null, _) => TestConfigs.WithFt(ftJson, SmallGeometry, lotsJson),
            (_, not null) => TestConfigs.WithCp(cpJson, SmallGeometry, lotsJson),
            _ => TestConfigs.WithWafer(SmallGeometry, lotsJson),
        };
        return pipeline.Generate(pipeline.Validate(workspace.WriteConfig(json)).Config!);
    }
}
