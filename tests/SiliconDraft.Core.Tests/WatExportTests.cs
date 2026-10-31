using System.Globalization;
using System.Text.Json;
using SiliconDraft.TestSupport;

namespace SiliconDraft.Core.Tests;

public sealed class WatExportTests
{
    [Fact]
    public void Export_ByDefault_WritesOneLongWatFilePerLot()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(
            TwoSites,
            lotsJson: """
                [ { "id": "LOT-0001", "waferCount": 1 }, { "id": "LOT-0002", "waferCount": 1 } ]
                """);
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);

        Assert.True(result.Success);
        Assert.Equal(2, result.WatPaths.Count);
        Assert.Equal(
            Path.Combine(outputDirectory, "LOT-0001_WAT_long.txt"), result.WatPaths[0]);
        Assert.Equal(
            Path.Combine(outputDirectory, "LOT-0002_WAT_long.txt"), result.WatPaths[1]);
        Assert.All(result.WatPaths, path => Assert.True(File.Exists(path)));
    }

    [Fact]
    public void Export_LongFormat_ContainsSiteCoordinatesItemValueUnitAndLimits()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(TwoSites);
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);
        var lines = File.ReadAllLines(Assert.Single(result.WatPaths));

        Assert.Equal("# catalogVersion=wat-catalog-v3", lines[0]);
        Assert.Equal(
            "lotId\twaferId\tsite\tsiteXmm\tsiteYmm\ttestItem\ttestNumber\tvalue\tunit" +
            "\tlowSpec\thighSpec\tlowTest\thighTest",
            lines[1]);

        var wafer = Assert.Single(Assert.Single(dataset.Lots).Wafers);
        var rows = lines.Skip(2).Select(line => line.Split('\t')).ToArray();
        Assert.Equal(wafer.WatResults.Count, rows.Length);
        for (var index = 0; index < rows.Length; index++)
        {
            var measurement = wafer.WatResults[index];
            var item = measurement.Item;
            Assert.Equal(13, rows[index].Length);
            Assert.Equal("LOT-0001", rows[index][0]);
            Assert.Equal(wafer.Id, rows[index][1]);
            Assert.Equal(measurement.Site.Id, rows[index][2]);
            Assert.Equal(measurement.Site.XMm, Parse(rows[index][3]), 6);
            Assert.Equal(measurement.Site.YMm, Parse(rows[index][4]), 6);
            Assert.Equal(item.Name, rows[index][5]);
            Assert.Equal(item.TestNumber, int.Parse(rows[index][6], CultureInfo.InvariantCulture));
            Assert.Equal(Format(measurement.Value), rows[index][7]);
            Assert.Equal(item.Unit, rows[index][8]);
            Assert.Equal(item.SpecLimits.Low, Parse(rows[index][9]), 6);
            Assert.Equal(item.SpecLimits.High, Parse(rows[index][10]), 6);
            Assert.Equal(item.TestLimits.Low, Parse(rows[index][11]), 6);
            Assert.Equal(item.TestLimits.High, Parse(rows[index][12]), 6);
        }
    }

    [Fact]
    public void Export_WideFormat_PutsEveryItemInAColumnAndDefinesUnitsAndLimits()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(
            """
            {
              "sites": [
                { "id": "C", "xMm": 0, "yMm": 0 },
                { "id": "N", "xMm": 0, "yMm": 50 }
              ],
              "template": "wide"
            }
            """,
            lotsJson: """[ { "id": "LOT-0001", "waferCount": 2 } ]""");
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);
        var lines = File.ReadAllLines(Assert.Single(result.WatPaths));
        var items = dataset.WatProgram.Items;

        Assert.Equal("# catalogVersion=wat-catalog-v3", lines[0]);
        Assert.Equal(
            "# testItem\ttestNumber\tunit\tlowSpec\thighSpec\tlowTest\thighTest", lines[1]);
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            Assert.Equal(
                [
                    $"# {item.Name}",
                    item.TestNumber.ToString(CultureInfo.InvariantCulture),
                    item.Unit,
                    Format(item.SpecLimits.Low),
                    Format(item.SpecLimits.High),
                    Format(item.TestLimits.Low),
                    Format(item.TestLimits.High),
                ],
                lines[2 + index].Split('\t'));
        }

        Assert.Equal(
            "lotId\twaferId\tsite\tsiteXmm\tsiteYmm\t" + string.Join('\t', items.Select(item => item.Name)),
            lines[2 + items.Count]);

        var wafers = dataset.Lots.SelectMany(lot => lot.Wafers).ToArray();
        var rows = lines.Skip(3 + items.Count).Select(line => line.Split('\t')).ToArray();
        Assert.Equal(wafers.Length * dataset.Config.Wat.Sites.Count, rows.Length);
        var rowIndex = 0;
        foreach (var wafer in wafers)
        {
            foreach (var site in dataset.Config.Wat.Sites)
            {
                var row = rows[rowIndex++];
                Assert.Equal(5 + items.Count, row.Length);
                Assert.Equal("LOT-0001", row[0]);
                Assert.Equal(wafer.Id, row[1]);
                Assert.Equal(site.Id, row[2]);
                Assert.Equal(site.XMm, Parse(row[3]), 6);
                Assert.Equal(site.YMm, Parse(row[4]), 6);
                for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
                {
                    var measurement = wafer.WatResults.Single(
                        measured => measured.Site == site && measured.Item == items[itemIndex]);
                    Assert.Equal(Format(measurement.Value), row[5 + itemIndex]);
                }
            }
        }
    }

    [Fact]
    public void Export_WithPerWaferOrganization_WritesOneFilePerWafer()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(
            """{ "organization": "perWafer" }""",
            lotsJson: """
                [ { "id": "LOT-0001", "waferCount": 1 }, { "id": "LOT-0002", "waferCount": 1 } ]
                """);
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);

        Assert.True(result.Success);
        Assert.Equal(2, result.WatPaths.Count);
        Assert.Equal(
            Path.Combine(outputDirectory, "LOT-0001-W01_WAT_long.txt"), result.WatPaths[0]);
        Assert.Equal(
            Path.Combine(outputDirectory, "LOT-0002-W01_WAT_long.txt"), result.WatPaths[1]);

        var firstRows = File.ReadAllLines(result.WatPaths[0]).Skip(2).ToArray();
        Assert.NotEmpty(firstRows);
        Assert.All(firstRows, row => Assert.Equal("LOT-0001-W01", row.Split('\t')[1]));
        var secondRows = File.ReadAllLines(result.WatPaths[1]).Skip(2).ToArray();
        Assert.NotEmpty(secondRows);
        Assert.All(secondRows, row => Assert.Equal("LOT-0002-W01", row.Split('\t')[1]));
    }

    [Fact]
    public void Export_WatOutputAgreesWithTheCatalogVersionRecordedInTheManifest()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(TwoSites);
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);

        using var manifest = JsonDocument.Parse(File.ReadAllText(result.ManifestPath!));
        var root = manifest.RootElement;
        var wat = root.GetProperty("dataset").GetProperty("wat");
        Assert.Equal(
            dataset.WatProgram.CatalogVersion, wat.GetProperty("catalogVersion").GetString());
        Assert.Equal(dataset.Config.Wat.Sites.Count, wat.GetProperty("siteCount").GetInt32());
        Assert.Equal(
            dataset.WatProgram.Items.Count, wat.GetProperty("testItemCount").GetInt32());
        Assert.Equal(
            dataset.Lots.SelectMany(lot => lot.Wafers).Sum(wafer => wafer.WatResults.Count),
            wat.GetProperty("measurementCount").GetInt32());

        var longFile = File.ReadAllLines(Assert.Single(result.WatPaths));
        Assert.Equal(
            $"# catalogVersion={wat.GetProperty("catalogVersion").GetString()}", longFile[0]);
        var exportedItems = longFile
            .Skip(2)
            .Select(line => line.Split('\t')[5])
            .Distinct()
            .ToArray();
        Assert.Equal(dataset.WatProgram.Items.Select(item => item.Name), exportedItems);

        var resolvedWat = root.GetProperty("resolvedConfig").GetProperty("wat");
        Assert.Equal("long", resolvedWat.GetProperty("template").GetString());
        Assert.Equal("perLot", resolvedWat.GetProperty("organization").GetString());
        var sites = resolvedWat.GetProperty("sites").EnumerateArray().ToArray();
        Assert.Equal(dataset.Config.Wat.Sites.Count, sites.Length);
        Assert.Equal(
            dataset.Config.Wat.Sites.Select(site => site.Id),
            sites.Select(site => site.GetProperty("id").GetString()));

        var lot = Assert.Single(root.GetProperty("dataset").GetProperty("lots").EnumerateArray());
        var wafer = Assert.Single(lot.GetProperty("wafers").EnumerateArray());
        Assert.Equal(
            Assert.Single(dataset.Lots).Wafers.Single().WatResults.Count,
            lot.GetProperty("watMeasurements").GetInt32());
        Assert.Equal(lot.GetProperty("watMeasurements").GetInt32(), wafer.GetProperty("watMeasurements").GetInt32());
    }

    [Fact]
    public void Export_SameConfigAndSeed_WritesIdenticalWatFiles()
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var dataset = Generate(TwoSites);

        var first = pipeline.Export(dataset, workspace.PathFor("run-a"));
        var second = pipeline.Export(dataset, workspace.PathFor("run-b"));

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(
            File.ReadAllBytes(Assert.Single(first.WatPaths)),
            File.ReadAllBytes(Assert.Single(second.WatPaths)));
    }

    [Fact]
    public void Export_WhenWatFileAlreadyExists_RequiresOverwriteConfirmation()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(TwoSites);
        var outputDirectory = workspace.PathFor("dataset");
        Directory.CreateDirectory(outputDirectory);
        var watPath = Path.Combine(outputDirectory, "LOT-0001_WAT_long.txt");
        File.WriteAllText(watPath, "stale\n");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);

        Assert.False(result.Success);
        Assert.Contains("already exists", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LOT-0001_WAT_long.txt", result.Error, StringComparison.OrdinalIgnoreCase);

        var overwritten = new GenerationPipeline().Export(
            dataset, outputDirectory, overwrite: true);
        Assert.True(overwritten.Success);
        Assert.DoesNotContain("stale", File.ReadAllText(watPath));
    }

    private static string Format(double value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);

    private static double Parse(string value) =>
        double.Parse(value, CultureInfo.InvariantCulture);

    private const string TwoSites = """
        {
          "sites": [
            { "id": "C", "xMm": 0, "yMm": 0 },
            { "id": "N", "xMm": 0, "yMm": 50 }
          ]
        }
        """;

    private static Dataset Generate(
        string watJson, string? waferJson = null, string? lotsJson = null)
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var json = TestConfigs.WithWat(
            watJson,
            waferJson,
            lotsJson ?? """[ { "id": "LOT-0001", "waferCount": 1 } ]""");
        return pipeline.Generate(pipeline.Validate(workspace.WriteConfig(json)).Config!);
    }
}
