using SiliconDraft.TestSupport;

namespace SiliconDraft.Core.Tests;

public sealed class CpGenerationTests
{
    [Fact]
    public void Generate_CreatesOneTestResultPerItemForEveryValidDie()
    {
        var dataset = Generate(TestConfigs.Valid);

        var results = AllResults(dataset);

        Assert.Equal(dataset.DieTotals.GrossDie, results.Count);
        Assert.All(results, result => Assert.Equal(DieDisposition.Valid, result.Die.Disposition));
        Assert.All(results, result => Assert.Equal(dataset.CpProgram.Items.Count, result.Tests.Count));
        Assert.Contains(results.SelectMany(result => result.Tests), test => test.Item is NumericTestItem);
        Assert.Contains(results.SelectMany(result => result.Tests), test => test.Item is FunctionalTestItem);
        Assert.All(results.SelectMany(result => result.Tests), test =>
        {
            Assert.False(string.IsNullOrWhiteSpace(test.Item.Unit));
            Assert.True(test.Item.TestLimits.Low < test.Item.TestLimits.High);
            Assert.True(test.Item.SpecLimits.Low < test.Item.SpecLimits.High);
        });
    }

    [Fact]
    public void Generate_AssignsBinsFromTheFirstFailingTestItem()
    {
        var dataset = Generate(TestConfigs.Valid);
        var bins = dataset.Config.Cp.Bins;
        var results = AllResults(dataset);

        Assert.Contains(results, result => !result.Passed);
        foreach (var result in results)
        {
            if (result.Passed)
            {
                Assert.Equal(bins.PassHardBin, result.HardBin);
                Assert.Equal(bins.PassSoftBin, result.SoftBin);
                continue;
            }

            var failure = result.Tests.First(test => !test.Passed);
            var expectedHardBin = failure.Item is FunctionalTestItem
                ? bins.FunctionalFailHardBin
                : bins.ParametricFailHardBin;
            Assert.Equal(expectedHardBin, result.HardBin);
            Assert.Equal(failure.Item.FailSoftBin, result.SoftBin);
        }
    }

    [Fact]
    public void Generate_ReconcilesBinCountsWithTestedDie()
    {
        var dataset = Generate(TestConfigs.Valid);
        var cp = dataset.Cp;

        Assert.Equal(dataset.DieTotals.GrossDie, cp.TestedDie);
        Assert.Equal(cp.TestedDie, cp.HardBins.Sum(bin => bin.Count));
        Assert.Equal(cp.TestedDie, cp.SoftBins.Sum(bin => bin.Count));
        Assert.Equal(cp.PassedDie, cp.HardBins.Single(bin => bin.Pass).Count);
        Assert.Equal(cp.PassedDie, cp.SoftBins.Single(bin => bin.Pass).Count);
        Assert.Equal(3, cp.HardBins.Count);
        Assert.Equal(dataset.CpProgram.Items.Count + 1, cp.SoftBins.Count);
        Assert.True(cp.PassedDie < cp.TestedDie);
    }

    [Fact]
    public void Generate_WithOverriddenLimitsAndBins_UsesEffectiveDefinitions()
    {
        var dataset = Generate(TestConfigs.WithCp(
            """
            {
              "bins": {
                "passHardBin": 10,
                "passSoftBin": 11,
                "parametricFailHardBin": 12,
                "functionalFailHardBin": 13
              },
              "testItems": [
                { "name": "Vt", "testLimits": { "low": 0.4499, "high": 0.4501 } }
              ]
            }
            """));

        var vT = dataset.CpProgram.Items.Single(item => item.Name == "Vt");
        Assert.Equal(new Limits(0.4499, 0.4501), vT.TestLimits);
        Assert.Equal(10, dataset.Config.Cp.Bins.PassHardBin);
        Assert.Equal(11, dataset.Config.Cp.Bins.PassSoftBin);
        Assert.Contains(AllResults(dataset), result =>
            !result.Passed && result.HardBin == 12 && result.SoftBin == vT.FailSoftBin);
        Assert.DoesNotContain(AllResults(dataset), result => result.HardBin == 1);
    }

    [Fact]
    public void Generate_WithRetainedPartialDie_DoesNotTestThem()
    {
        var dataset = Generate(TestConfigs.WithWafer(
            """
            {
              "diameterMm": 100,
              "edgeExclusionMm": 0,
              "die": { "widthMm": 10, "heightMm": 10, "scribeMm": 0 },
              "partialDiePolicy": "retain"
            }
            """));

        var wafer = Assert.Single(Assert.Single(dataset.Lots).Wafers);
        Assert.Equal(40, wafer.DieMap.PartialDieRetained);
        Assert.Equal(wafer.DieMap.GrossDie, wafer.CpResults.Count);
        Assert.All(wafer.CpResults, result => Assert.True(result.Die.Tested));
    }

    private static List<CpDieResult> AllResults(Dataset dataset) =>
        dataset.Lots
            .SelectMany(lot => lot.Wafers)
            .SelectMany(wafer => wafer.CpResults)
            .ToList();

    private static Dataset Generate(string json)
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var config = pipeline.Validate(workspace.WriteConfig(json)).Config!;
        return pipeline.Generate(config);
    }
}
