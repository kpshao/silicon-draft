using SiliconDraft.TestSupport;

namespace SiliconDraft.Core.Tests;

public sealed class WatGenerationTests
{
    [Fact]
    public void Generate_CoversEveryCatalogItemAtEverySiteOnEveryWafer()
    {
        var dataset = Generate(
            ThreeSites,
            lotsJson: """[ { "id": "LOT-0001", "waferCount": 2 } ]""");

        Assert.Equal("wat-catalog-v3", dataset.WatProgram.CatalogVersion);
        var items = dataset.WatProgram.Items;
        Assert.NotEmpty(items);
        Assert.All(items, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Name));
            Assert.False(string.IsNullOrWhiteSpace(item.Unit));
            Assert.True(item.SpecLimits.Low < item.SpecLimits.High);
            Assert.True(item.TestLimits.Low < item.TestLimits.High);
        });

        var sites = dataset.Config.Wat.Sites;
        Assert.Equal(3, sites.Count);
        Assert.All(dataset.Lots.SelectMany(lot => lot.Wafers), wafer =>
        {
            Assert.Equal(sites.Count * items.Count, wafer.WatResults.Count);
            foreach (var site in sites)
            {
                var atSite = wafer.WatResults.Where(result => result.Site == site).ToArray();
                Assert.Equal(items.Select(item => item.Name), atSite.Select(result => result.Item.Name));
            }

            Assert.All(wafer.WatResults, result => Assert.True(double.IsFinite(result.Value)));
        });
    }

    [Fact]
    public void Generate_SameSeedReproducesWatValuesAndADifferentSeedChangesThem()
    {
        var first = Generate(ThreeSites);
        var second = Generate(ThreeSites);
        var other = Generate(ThreeSites, seed: 43);

        Assert.Equal(Values(first), Values(second));
        Assert.NotEqual(Values(first), Values(other));
    }

    private static (string Site, string Item, double Value)[] Values(Dataset dataset) =>
        dataset.Lots
            .SelectMany(lot => lot.Wafers)
            .SelectMany(wafer => wafer.WatResults)
            .Select(result => (result.Site.Id, result.Item.Name, result.Value))
            .ToArray();

    private static Dataset Generate(string watJson, string? lotsJson = null, long seed = 42)
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var json = TestConfigs.WithWat(
            watJson,
            lotsJson: lotsJson ?? """[ { "id": "LOT-0001", "waferCount": 1 } ]""");
        if (seed != 42)
        {
            json = json.Replace("\"seed\": 42", $"\"seed\": {seed}");
        }

        return pipeline.Generate(pipeline.Validate(workspace.WriteConfig(json)).Config!);
    }

    private const string ThreeSites = """
        {
          "sites": [
            { "id": "C", "xMm": 0, "yMm": 0 },
            { "id": "N", "xMm": 0, "yMm": 50 },
            { "id": "E", "xMm": 50, "yMm": 0 }
          ]
        }
        """;
}
