using System.Text.Json;
using SiliconDraft.TestSupport;

namespace SiliconDraft.Core.Tests;

public sealed class StdfFtExportTests
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

    private static readonly string[] TraceabilityFields =
        ["SOURCE_WAFER_ID", "SOURCE_DIE_X", "SOURCE_DIE_Y", "ASSEMBLY_LOT"];

    [Fact]
    public void Export_WritesOneStdfFtFilePerLot()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(TwoLots);
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);

        Assert.True(result.Success);
        Assert.Equal(2, result.FtPaths.Count);
        Assert.Equal(Path.Combine(outputDirectory, "LOT-0001_FT.stdf"), result.FtPaths[0]);
        Assert.Equal(Path.Combine(outputDirectory, "LOT-0002_FT.stdf"), result.FtPaths[1]);
        Assert.All(result.FtPaths, path => Assert.True(File.Exists(path)));
    }

    [Fact]
    public void Export_StdfFtHasTheRequiredRecordSequence()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(TwoLots);
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);

        for (var lotIndex = 0; lotIndex < dataset.Lots.Count; lotIndex++)
        {
            var lot = dataset.Lots[lotIndex];
            var records = StdfReader.Read(result.FtPaths[lotIndex]);
            Assert.Equal(new FarInfo(0, 4), StdfReader.ParseFar(records[0]));

            var unitCount = lot.FtUnits.Count;
            for (var atrIndex = 0; atrIndex < unitCount; atrIndex++)
            {
                Assert.Equal((byte)0, records[1 + atrIndex].Type);
                Assert.Equal((byte)20, records[1 + atrIndex].Sub);
                StdfReader.ParseAtr(records[1 + atrIndex]);
            }

            var mirIndex = 1 + unitCount;
            var mir = StdfReader.ParseMir(records[mirIndex]);
            Assert.Equal(lot.Id, mir.LotId);
            Assert.Equal("DemoProduct", mir.PartType);
            Assert.Equal("DemoProduct_FT", mir.JobName);
            Assert.Equal("FT", mir.TestCode);

            var mrrIndex = records.Count - 1;
            Assert.Equal((byte)1, records[mrrIndex].Type);
            Assert.Equal((byte)20, records[mrrIndex].Sub);

            var itemCount = dataset.FtProgram.Items.Count;
            var unitIndex = 0;
            var index = mirIndex + 1;
            while (index < mrrIndex)
            {
                var pir = StdfReader.ParsePir(records[index++]);
                Assert.Equal((byte)1, pir.HeadNumber);
                Assert.Equal((byte)1, pir.SiteNumber);
                for (var testIndex = 0; testIndex < itemCount; testIndex++)
                {
                    Assert.Equal((byte)15, records[index].Type);
                    Assert.Equal((byte)10, records[index].Sub);
                    var ptr = StdfReader.ParsePtr(records[index++]);
                    Assert.Equal((byte)1, ptr.HeadNumber);
                    Assert.Equal((byte)1, ptr.SiteNumber);
                }

                Assert.Equal((byte)5, records[index].Type);
                Assert.Equal((byte)20, records[index].Sub);
                var prr = StdfReader.ParsePrr(records[index++]);
                Assert.Equal((byte)1, prr.HeadNumber);
                Assert.Equal((byte)1, prr.SiteNumber);
                unitIndex++;
            }

            Assert.Equal(mrrIndex, index);
            Assert.Equal(unitCount, unitIndex);
        }
    }

    [Fact]
    public void Export_StdfFtPrrCarriesTheSourceDieAndStableUnitId()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(TwoLots);
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);

        for (var lotIndex = 0; lotIndex < dataset.Lots.Count; lotIndex++)
        {
            var groups = ReadFtGroups(result.FtPaths[lotIndex]);
            var units = dataset.Lots[lotIndex].FtUnits;
            Assert.Equal(units.Count, groups.Count);
            for (var index = 0; index < groups.Count; index++)
            {
                Assert.Equal(units[index].SourceDie.X, groups[index].Prr.X);
                Assert.Equal(units[index].SourceDie.Y, groups[index].Prr.Y);
                Assert.Equal(units[index].Id, groups[index].Prr.PartId);
                Assert.Equal(units[index].HardBin, groups[index].Prr.HardBin);
                Assert.Equal(units[index].SoftBin, groups[index].Prr.SoftBin);
                Assert.Equal(units[index].Tests.Count, groups[index].Prr.NumberOfTests);
                Assert.Equal(units[index].Passed ? (byte)0x00 : (byte)0x08, groups[index].Prr.PartFlags);
            }
        }
    }

    [Fact]
    public void Export_StdfFtAtrCarriesTraceabilityFields()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(TwoLots);
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);

        for (var lotIndex = 0; lotIndex < dataset.Lots.Count; lotIndex++)
        {
            var lot = dataset.Lots[lotIndex];
            var groups = ReadFtGroups(result.FtPaths[lotIndex]);
            for (var index = 0; index < groups.Count; index++)
            {
                var fields = groups[index].Atr.ParseFields();
                Assert.All(TraceabilityFields, field => Assert.True(fields.ContainsKey(field)));
                Assert.Equal(lot.FtUnits[index].Id, fields["UNIT_ID"]);
                Assert.Equal(lot.FtUnits[index].SourceWaferId, fields["SOURCE_WAFER_ID"]);
                Assert.Equal(lot.FtUnits[index].SourceDie.X, int.Parse(fields["SOURCE_DIE_X"]));
                Assert.Equal(lot.FtUnits[index].SourceDie.Y, int.Parse(fields["SOURCE_DIE_Y"]));
                Assert.Equal(lot.AssemblyLot, fields["ASSEMBLY_LOT"]);
                Assert.Equal(lot.FtUnits[index].CpPassed ? "Pass" : "Fail", fields["CP_RESULT"]);
            }
        }
    }

    [Fact]
    public void Export_StdfFtBinsReconcileWithTheManifest()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(TwoLots);
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);
        var prrs = ReadFtGroups(result.FtPaths[0]).Select(group => group.Prr).ToArray();

        using var manifest = JsonDocument.Parse(File.ReadAllText(result.ManifestPath!));
        var lot = manifest.RootElement.GetProperty("dataset").GetProperty("lots")
            .EnumerateArray().First();
        var ft = lot.GetProperty("ft");

        Assert.Equal(ft.GetProperty("unitCount").GetInt32(), prrs.Length);
        var hardBins = ft.GetProperty("hardBins").EnumerateArray()
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

        var softBins = ft.GetProperty("softBins").EnumerateArray()
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
    }

    [Fact]
    public void Export_EscapeUnitsAgreeBetweenManifestAndStdf()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(
            TwoLots, """{ "assemblyLoss": 2, "escapeRate": 0.5 }""");
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);
        using var manifest = JsonDocument.Parse(File.ReadAllText(result.ManifestPath!));
        var datasetElement = manifest.RootElement.GetProperty("dataset");
        var ft = datasetElement.GetProperty("ft");
        var expectedEscapes = ft.GetProperty("escapeUnits").GetInt32();

        var observedEscapes = 0;
        for (var lotIndex = 0; lotIndex < dataset.Lots.Count; lotIndex++)
        {
            foreach (var group in ReadFtGroups(result.FtPaths[lotIndex]))
            {
                if (group.Atr.ParseFields()["CP_RESULT"] == "Fail")
                {
                    observedEscapes++;
                }
            }
        }

        Assert.Equal(expectedEscapes, observedEscapes);
        Assert.Equal(
            datasetElement.GetProperty("cp").GetProperty("passedDie").GetInt32() -
            ft.GetProperty("assemblyLoss").GetInt32() + expectedEscapes,
            ft.GetProperty("unitCount").GetInt32());
    }

    [Fact]
    public void Export_CpAndFtJoinOnSourceWaferAndDie()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(
            TwoLots, """{ "assemblyLoss": 2, "escapeRate": 0.5 }""");
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);

        var cpPassCount = 0;
        var ftCount = 0;
        var escapeCount = 0;
        for (var lotIndex = 0; lotIndex < dataset.Lots.Count; lotIndex++)
        {
            var cpPrrs = StdfReader.Read(result.CpStdfPaths[lotIndex])
                .Where(record => record.Type == 5 && record.Sub == 20)
                .Select(StdfReader.ParsePrr)
                .ToArray();
            var cpByKey = cpPrrs.ToDictionary(
                prr => (prr.PartId.Split(':')[0], (int)prr.X, (int)prr.Y));

            var ftGroups = ReadFtGroups(result.FtPaths[lotIndex]);
            foreach (var group in ftGroups)
            {
                var fields = group.Atr.ParseFields();
                var waferId = fields["SOURCE_WAFER_ID"];
                var x = int.Parse(fields["SOURCE_DIE_X"]);
                var y = int.Parse(fields["SOURCE_DIE_Y"]);
                Assert.True(cpByKey.ContainsKey((waferId, x, y)));
                Assert.Equal((short)x, group.Prr.X);
                Assert.Equal((short)y, group.Prr.Y);
                Assert.Equal($"{waferId}:{x},{y}", group.Prr.PartId);
                Assert.Equal(group.Prr.PartId, fields["UNIT_ID"]);
                var cpPassed = (cpByKey[(waferId, x, y)].PartFlags & 0x08) == 0;
                Assert.Equal(cpPassed, fields["CP_RESULT"] == "Pass");
            }

            cpPassCount += cpPrrs.Count(prr => (prr.PartFlags & 0x08) == 0);
            ftCount += ftGroups.Count;
            escapeCount += ftGroups.Count(group =>
                group.Atr.ParseFields()["CP_RESULT"] == "Fail");
        }

        Assert.Equal(cpPassCount - 2 + escapeCount, ftCount);
    }

    [Fact]
    public void Export_SameConfigAndSeed_WritesIdenticalStdfFt()
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var dataset = Generate(TwoLots, """{ "assemblyLoss": 2, "escapeRate": 0.5 }""");

        var first = pipeline.Export(dataset, workspace.PathFor("run-a"));
        var second = pipeline.Export(dataset, workspace.PathFor("run-b"));

        Assert.True(first.Success);
        Assert.True(second.Success);
        for (var index = 0; index < first.FtPaths.Count; index++)
        {
            Assert.Equal(
                File.ReadAllBytes(first.FtPaths[index]),
                File.ReadAllBytes(second.FtPaths[index]));
        }
    }

    private static List<FtGroup> ReadFtGroups(string path)
    {
        var records = StdfReader.Read(path);
        var atrs = new List<AtrInfo>();
        var index = 1; // FAR
        while (index < records.Count && records[index].Type == 0 && records[index].Sub == 20)
        {
            atrs.Add(StdfReader.ParseAtr(records[index++]));
        }

        Assert.Equal((byte)1, records[index].Type);
        Assert.Equal((byte)10, records[index].Sub);
        StdfReader.ParseMir(records[index++]);

        var units = new List<(PirInfo Pir, IReadOnlyList<PtrInfo> Ptrs, PrrInfo Prr)>();
        while (index < records.Count && records[index].Type == 5 && records[index].Sub == 10)
        {
            var pir = StdfReader.ParsePir(records[index++]);
            var ptrs = new List<PtrInfo>();
            while (records[index].Type == 15 && records[index].Sub == 10)
            {
                ptrs.Add(StdfReader.ParsePtr(records[index++]));
            }

            units.Add((pir, ptrs, StdfReader.ParsePrr(records[index++])));
        }

        Assert.Equal((byte)1, records[index].Type);
        Assert.Equal((byte)20, records[index].Sub);
        Assert.Equal(records.Count - 1, index);

        var atrByUnit = atrs.ToDictionary(
            atr => atr.ParseFields()["UNIT_ID"], atr => atr);
        Assert.Equal(units.Count, atrByUnit.Count);
        return units
            .Select(unit => new FtGroup(
                unit.Pir, unit.Ptrs, unit.Prr, atrByUnit[unit.Prr.PartId]))
            .ToList();
    }

    private static Dataset Generate(
        string lotsJson = TwoLots, string? ftJson = null)
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var json = ftJson is null
            ? TestConfigs.WithWafer(SmallGeometry, lotsJson)
            : TestConfigs.WithFt(ftJson, SmallGeometry, lotsJson);
        return pipeline.Generate(pipeline.Validate(workspace.WriteConfig(json)).Config!);
    }

    private sealed record FtGroup(
        PirInfo Pir,
        IReadOnlyList<PtrInfo> Ptrs,
        PrrInfo Prr,
        AtrInfo Atr);
}
