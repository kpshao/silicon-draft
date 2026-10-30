using System.Text.Json;
using SiliconDraft.TestSupport;

namespace SiliconDraft.Core.Tests;

public sealed class StdfCpExportTests
{
    private const string LotId = "LOT-0001";

    [Fact]
    public void Export_WritesOneStdfCpFilePerLot()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(
            SmallGeometry,
            lotsJson: """
                [ { "id": "LOT-0001", "waferCount": 1 }, { "id": "LOT-0002", "waferCount": 1 } ]
                """);
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);

        Assert.True(result.Success);
        Assert.Equal(2, result.CpStdfPaths.Count);
        Assert.Equal(Path.Combine(outputDirectory, "LOT-0001_CP.stdf"), result.CpStdfPaths[0]);
        Assert.Equal(Path.Combine(outputDirectory, "LOT-0002_CP.stdf"), result.CpStdfPaths[1]);
        Assert.All(result.CpStdfPaths, path => Assert.True(File.Exists(path)));
    }

    [Fact]
    public void Export_StdfCpHasTheRequiredRecordSequence()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(SmallGeometry);
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);

        var records = StdfReader.Read(Assert.Single(result.CpStdfPaths));
        var far = StdfReader.ParseFar(records[0]);
        Assert.Equal(new FarInfo(0, 4), far);

        var mir = StdfReader.ParseMir(records[1]);
        Assert.Equal(LotId, mir.LotId);
        Assert.Equal("DemoProduct", mir.PartType);
        Assert.Equal("CP", mir.TestCode);
        Assert.Equal('P', mir.ModeCode);

        var mrrIndex = records.Count - 1;
        Assert.Equal((byte)1, records[mrrIndex].Type);
        Assert.Equal((byte)20, records[mrrIndex].Sub);

        var itemCount = dataset.CpProgram.Items.Count;
        var pirCount = 0;
        var prrCount = 0;
        var ptrCount = 0;
        var index = 2;
        while (index < mrrIndex)
        {
            var pir = records[index++];
            StdfReader.ParsePir(pir);
            pirCount++;
            for (var testIndex = 0; testIndex < itemCount; testIndex++)
            {
                var ptr = records[index++];
                Assert.Equal((byte)15, ptr.Type);
                Assert.Equal((byte)10, ptr.Sub);
                ptrCount++;
            }

            var prr = records[index++];
            Assert.Equal((byte)5, prr.Type);
            Assert.Equal((byte)20, prr.Sub);
            prrCount++;
        }

        Assert.Equal(mrrIndex, index);
        Assert.Equal(dataset.DieTotals.GrossDie, pirCount);
        Assert.Equal(dataset.DieTotals.GrossDie, prrCount);
        Assert.Equal(dataset.DieTotals.GrossDie * itemCount, ptrCount);
    }

    [Fact]
    public void Export_StdfCpCarriesUnitsAndLimitsForNumericAndFunctionalItems()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(SmallGeometry);
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);
        var ptrs = StdfReader.Read(Assert.Single(result.CpStdfPaths))
            .Where(record => record.Type == 15 && record.Sub == 10)
            .Select(StdfReader.ParsePtr)
            .ToArray();

        Assert.Equal(dataset.CpProgram.Items.Count * dataset.DieTotals.GrossDie, ptrs.Length);
        Assert.All(dataset.CpProgram.Items, item =>
            Assert.Contains(ptrs, ptr => ptr.TestText == item.Name));

        Assert.All(ptrs, ptr =>
        {
            Assert.False(string.IsNullOrWhiteSpace(ptr.TestText));
            Assert.False(string.IsNullOrWhiteSpace(ptr.Unit));
            Assert.Equal(0, ptr.OptionalFlags);
            Assert.Equal(0, ptr.ResultScale);
            Assert.True(ptr.LowLimit < ptr.HighLimit);
            Assert.True(ptr.LowSpec < ptr.HighSpec);
            Assert.True(ptr.LowSpec <= ptr.LowLimit);
            Assert.True(ptr.HighLimit <= ptr.HighSpec);
            Assert.Equal(ptr.Passed, !ptr.Failed);
            Assert.Equal(
                ptr.Passed,
                ptr.Result >= ptr.LowLimit && ptr.Result <= ptr.HighLimit);
        });

        var vT = ptrs.First(ptr => ptr.TestText == "Vt");
        Assert.Equal(1001u, vT.TestNumber);
        Assert.Equal("V", vT.Unit);
        Assert.Equal(0.40f, vT.LowLimit);
        Assert.Equal(0.50f, vT.HighLimit);
        Assert.Equal(0.39f, vT.LowSpec);
        Assert.Equal(0.51f, vT.HighSpec);

        var scan = ptrs.First(ptr => ptr.TestText == "Scan");
        Assert.Equal(2001u, scan.TestNumber);
        Assert.Equal("P/F", scan.Unit);
        Assert.Equal(0.5f, scan.LowLimit);
        Assert.Equal(1.5f, scan.HighLimit);
    }

    [Fact]
    public void Export_StdfBinsReconcileWithManifest()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(SmallGeometry, lotsJson: """[ { "id": "LOT-0001", "waferCount": 2 } ]""");
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);
        var prrs = StdfReader.Read(Assert.Single(result.CpStdfPaths))
            .Where(record => record.Type == 5 && record.Sub == 20)
            .Select(StdfReader.ParsePrr)
            .ToArray();

        using var manifest = JsonDocument.Parse(File.ReadAllText(result.ManifestPath!));
        var lot = Assert.Single(
            manifest.RootElement.GetProperty("dataset").GetProperty("lots").EnumerateArray());
        var cp = lot.GetProperty("cp");
        var testedDie = cp.GetProperty("testedDie").GetInt32();
        var passedDie = cp.GetProperty("passedDie").GetInt32();

        Assert.Equal(testedDie, prrs.Length);
        Assert.Equal(passedDie, prrs.Count(prr => prr.HardBin == 1));
        Assert.Equal(dataset.CpProgram.Items.Count, prrs[0].NumberOfTests);
        Assert.All(prrs, prr => Assert.Equal(dataset.CpProgram.Items.Count, prr.NumberOfTests));

        var hardBins = cp.GetProperty("hardBins").EnumerateArray()
            .ToDictionary(
                bin => bin.GetProperty("number").GetInt32(),
                bin => bin.GetProperty("count").GetInt32());
        var observedHardBins = prrs
            .GroupBy(prr => (int)prr.HardBin)
            .ToDictionary(group => group.Key, group => group.Count());
        foreach (var (number, count) in hardBins)
        {
            Assert.Equal(count, observedHardBins.GetValueOrDefault(number));
        }

        Assert.All(observedHardBins.Keys, number => Assert.Contains(number, hardBins.Keys));
        Assert.Equal(testedDie, hardBins.Values.Sum());

        var softBins = cp.GetProperty("softBins").EnumerateArray()
            .ToDictionary(
                bin => bin.GetProperty("number").GetInt32(),
                bin => bin.GetProperty("count").GetInt32());
        var observedSoftBins = prrs
            .GroupBy(prr => (int)prr.SoftBin)
            .ToDictionary(group => group.Key, group => group.Count());
        foreach (var (number, count) in softBins)
        {
            Assert.Equal(count, observedSoftBins.GetValueOrDefault(number));
        }

        Assert.All(observedSoftBins.Keys, number => Assert.Contains(number, softBins.Keys));
        Assert.Equal(testedDie, softBins.Values.Sum());
    }

    [Fact]
    public void Export_StdfPrrCoordinatesMatchTheRanDie()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(SmallGeometry, lotsJson: """[ { "id": "LOT-0001", "waferCount": 2 } ]""");
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);
        var prrs = StdfReader.Read(Assert.Single(result.CpStdfPaths))
            .Where(record => record.Type == 5 && record.Sub == 20)
            .Select(StdfReader.ParsePrr)
            .ToArray();

        var expected = dataset.Lots
            .SelectMany(lot => lot.Wafers)
            .SelectMany(wafer => wafer.CpResults.Select(result => (Result: result, Wafer: wafer.Id)))
            .ToArray();
        Assert.Equal(expected.Length, prrs.Length);
        for (var index = 0; index < prrs.Length; index++)
        {
            var dieResult = expected[index].Result;
            Assert.Equal(dieResult.Die.X, prrs[index].X);
            Assert.Equal(dieResult.Die.Y, prrs[index].Y);
            Assert.Equal(
                $"{expected[index].Wafer}:{dieResult.Die.X},{dieResult.Die.Y}",
                prrs[index].PartId);
            Assert.Equal(dieResult.HardBin, prrs[index].HardBin);
            Assert.Equal(dieResult.SoftBin, prrs[index].SoftBin);
            // PART_FLG bit 3 is 1 for a failing part; bit 4 is 0 when the P/F flag is valid.
            Assert.Equal(dieResult.Passed ? (byte)0x00 : (byte)0x08, prrs[index].PartFlags);
        }
    }

    [Fact]
    public void Export_SameConfigAndSeed_WritesIdenticalStdf()
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var dataset = Generate(SmallGeometry);

        var first = pipeline.Export(dataset, workspace.PathFor("run-a"));
        var second = pipeline.Export(dataset, workspace.PathFor("run-b"));

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(
            File.ReadAllBytes(Assert.Single(first.CpStdfPaths)),
            File.ReadAllBytes(Assert.Single(second.CpStdfPaths)));
    }

    private const string SmallGeometry = """
        {
          "diameterMm": 100,
          "edgeExclusionMm": 0,
          "die": { "widthMm": 10, "heightMm": 10, "scribeMm": 0 }
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
}
