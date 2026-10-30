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
    public void Export_StdfFtHasTheFullV4RecordSetWithoutWaferRecords()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(TwoLots);
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);

        for (var lotIndex = 0; lotIndex < dataset.Lots.Count; lotIndex++)
        {
            var lot = dataset.Lots[lotIndex];
            var file = ReadFtFile(result.FtPaths[lotIndex]);
            var siteCount = dataset.FtProgram.SiteCount;
            var itemCount = dataset.FtProgram.Items.Count;

            Assert.Equal((byte)siteCount, file.Sdr.SiteCount);
            Assert.Equal(lot.FtUnits.Count, file.Atrs.Count);
            Assert.Equal("FT", file.Mir.TestCode);
            Assert.DoesNotContain(file.AllRecords, record =>
                record.Type == 2 && record.Sub is 10 or 20 or 30);

            var units = file.Units;
            Assert.Equal(lot.FtUnits.Count, units.Count);
            Assert.All(units, unit =>
            {
                Assert.Equal((byte)1, unit.Pir.SiteNumber);
                Assert.Equal((byte)1, unit.Prr.SiteNumber);
                Assert.All(unit.Ptrs, ptr => Assert.Equal((byte)1, ptr.SiteNumber));
                Assert.All(unit.Ftrs, ftr => Assert.Equal((byte)1, ftr.SiteNumber));
            });

            Assert.Equal(itemCount, file.Tsrs.Count);
            for (var itemIndex = 0; itemIndex < itemCount; itemIndex++)
            {
                var item = dataset.FtProgram.Items[itemIndex];
                var tsr = file.Tsrs[itemIndex];
                Assert.Equal(item.Name, tsr.TestName);
                Assert.Equal((byte)1, tsr.SiteNumber);
                Assert.Equal(item is FunctionalTestItem ? 'F' : 'P', tsr.TestType);
                Assert.Equal((uint)lot.FtUnits.Count, tsr.ExecutionCount);
                Assert.Equal(
                    (uint)lot.FtUnits.Count(unit => !unit.Tests[itemIndex].Passed),
                    tsr.FailCount);
            }

            Assert.Equal(3, file.Hbrs.Count);
            Assert.Equal(itemCount + 1, file.Sbrs.Count);
            var pcr = Assert.Single(file.Pcrs);
            Assert.Equal((uint)lot.FtUnits.Count, pcr.PartCount);
            Assert.Equal((uint)lot.FtUnits.Count(unit => unit.Passed), pcr.GoodCount);

            var recordKinds = file.AllRecords.Select(record => (record.Type, record.Sub)).ToHashSet();
            foreach (var kind in StdfRecordKinds.FtV4Set)
            {
                Assert.Contains(kind, recordKinds);
            }
        }
    }

    [Fact]
    public void Export_StdfFtKeepsSourceWaferTraceabilityInAtrs()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(lotsJson: """[ { "id": "LOT-0001", "waferCount": 2 } ]""");
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);
        var file = ReadFtFile(Assert.Single(result.FtPaths));

        var sourceWafers = dataset.Lots[0].Wafers.Select(wafer => wafer.Id).ToHashSet();
        Assert.All(file.Units, unit =>
            Assert.Contains(unit.Atr.ParseFields()["SOURCE_WAFER_ID"], sourceWafers));
        Assert.Equal(
            dataset.Lots[0].FtUnits.Count,
            file.Units.Count);
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
            var units = AllUnits(ReadFtFile(result.FtPaths[lotIndex]));
            var expected = dataset.Lots[lotIndex].FtUnits;
            Assert.Equal(expected.Count, units.Count);
            for (var index = 0; index < units.Count; index++)
            {
                Assert.Equal(expected[index].SourceDie.X, units[index].Prr.X);
                Assert.Equal(expected[index].SourceDie.Y, units[index].Prr.Y);
                Assert.Equal(expected[index].Id, units[index].Prr.PartId);
                Assert.Equal(expected[index].HardBin, units[index].Prr.HardBin);
                Assert.Equal(expected[index].SoftBin, units[index].Prr.SoftBin);
                Assert.Equal(expected[index].Tests.Count, units[index].Prr.NumberOfTests);
                Assert.Equal(
                    expected[index].Passed ? (byte)0x00 : (byte)0x08,
                    units[index].Prr.PartFlags);
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
            var units = AllUnits(ReadFtFile(result.FtPaths[lotIndex]));
            for (var index = 0; index < units.Count; index++)
            {
                var fields = units[index].Atr.ParseFields();
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
        var prrs = AllUnits(ReadFtFile(result.FtPaths[0]))
            .Select(unit => unit.Prr)
            .ToArray();

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
            foreach (var unit in AllUnits(ReadFtFile(result.FtPaths[lotIndex])))
            {
                if (unit.Atr.ParseFields()["CP_RESULT"] == "Fail")
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

            var ftUnits = AllUnits(ReadFtFile(result.FtPaths[lotIndex]));
            foreach (var unit in ftUnits)
            {
                var fields = unit.Atr.ParseFields();
                var waferId = fields["SOURCE_WAFER_ID"];
                var x = int.Parse(fields["SOURCE_DIE_X"]);
                var y = int.Parse(fields["SOURCE_DIE_Y"]);
                Assert.True(cpByKey.ContainsKey((waferId, x, y)));
                Assert.Equal((short)x, unit.Prr.X);
                Assert.Equal((short)y, unit.Prr.Y);
                Assert.Equal($"{waferId}:{x},{y}", unit.Prr.PartId);
                Assert.Equal(unit.Prr.PartId, fields["UNIT_ID"]);
                var cpPassed = (cpByKey[(waferId, x, y)].PartFlags & 0x08) == 0;
                Assert.Equal(cpPassed, fields["CP_RESULT"] == "Pass");
            }

            cpPassCount += cpPrrs.Count(prr => (prr.PartFlags & 0x08) == 0);
            ftCount += ftUnits.Count;
            escapeCount += ftUnits.Count(unit =>
                unit.Atr.ParseFields()["CP_RESULT"] == "Fail");
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

    private static FtFile ReadFtFile(string path)
    {
        var records = StdfReader.Read(path);
        var index = 0;
        Assert.Equal(new FarInfo(0, 4), StdfReader.ParseFar(records[index++]));

        var atrs = new List<AtrInfo>();
        while (records[index].Type == 0 && records[index].Sub == 20)
        {
            atrs.Add(StdfReader.ParseAtr(records[index++]));
        }

        var mir = StdfReader.ParseMir(records[index++]);
        var sdr = StdfReader.ParseSdr(records[index++]);
        var atrByUnit = atrs.ToDictionary(atr => atr.ParseFields()["UNIT_ID"], atr => atr);

        var units = new List<FtUnitRecord>();
        while (records[index].Type == 5 && records[index].Sub == 10)
        {
            var pir = StdfReader.ParsePir(records[index++]);
            var ptrs = new List<PtrInfo>();
            var ftrs = new List<FtrInfo>();
            while (records[index].Type == 15)
            {
                if (records[index].Sub == 10)
                {
                    ptrs.Add(StdfReader.ParsePtr(records[index++]));
                }
                else if (records[index].Sub == 20)
                {
                    ftrs.Add(StdfReader.ParseFtr(records[index++]));
                }
                else
                {
                    Assert.Fail($"unexpected test record ({records[index].Type},{records[index].Sub})");
                }
            }

            var prr = StdfReader.ParsePrr(records[index++]);
            units.Add(new FtUnitRecord(
                pir, ptrs, ftrs, prr, atrByUnit[prr.PartId]));
        }

        var tsrs = new List<TsrInfo>();
        while (records[index].Type == 10 && records[index].Sub == 30)
        {
            tsrs.Add(StdfReader.ParseTsr(records[index++]));
        }

        var hbrs = new List<HbrInfo>();
        while (records[index].Type == 1 && records[index].Sub == 40)
        {
            hbrs.Add(StdfReader.ParseHbr(records[index++]));
        }

        var sbrs = new List<SbrInfo>();
        while (records[index].Type == 1 && records[index].Sub == 50)
        {
            sbrs.Add(StdfReader.ParseSbr(records[index++]));
        }

        var pcrs = new List<PcrInfo>();
        while (records[index].Type == 1 && records[index].Sub == 30)
        {
            pcrs.Add(StdfReader.ParsePcr(records[index++]));
        }

        var mrr = StdfReader.ParseMrr(records[index++]);
        Assert.Equal(records.Count, index);

        Assert.Equal(units.Count, atrByUnit.Count);
        return new FtFile(atrs, mir, sdr, units, tsrs, hbrs, sbrs, pcrs, mrr, records);
    }

    private static IReadOnlyList<FtUnitRecord> AllUnits(FtFile file) => file.Units;

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

    private sealed record FtFile(
        IReadOnlyList<AtrInfo> Atrs,
        MirInfo Mir,
        SdrInfo Sdr,
        IReadOnlyList<FtUnitRecord> Units,
        IReadOnlyList<TsrInfo> Tsrs,
        IReadOnlyList<HbrInfo> Hbrs,
        IReadOnlyList<SbrInfo> Sbrs,
        IReadOnlyList<PcrInfo> Pcrs,
        MrrInfo Mrr,
        IReadOnlyList<StdfRecord> AllRecords);

    private sealed record FtUnitRecord(
        PirInfo Pir,
        IReadOnlyList<PtrInfo> Ptrs,
        IReadOnlyList<FtrInfo> Ftrs,
        PrrInfo Prr,
        AtrInfo Atr);
}
