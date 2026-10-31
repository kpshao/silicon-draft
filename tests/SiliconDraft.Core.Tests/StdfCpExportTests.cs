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
    public void Export_StdfCpHasTheFullV4RecordSetInCanonicalOrder()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(SmallGeometry);
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);

        var records = StdfReader.Read(Assert.Single(result.CpStdfPaths));
        var index = 0;
        Assert.Equal(new FarInfo(2, 4), StdfReader.ParseFar(records[index++]));

        var mir = StdfReader.ParseMir(records[index++]);
        Assert.Equal("CP", mir.TestCode);

        var siteCount = dataset.CpProgram.SiteCount;
        var itemCount = dataset.CpProgram.Items.Count;
        var sdr = StdfReader.ParseSdr(records[index++]);
        Assert.Equal((byte)siteCount, sdr.SiteCount);
        Assert.Equal(Enumerable.Range(1, siteCount).Select(number => (byte)number), sdr.SiteNumbers);

        var observedSites = new HashSet<byte>();
        var wafer = Assert.Single(Assert.Single(dataset.Lots).Wafers);
        var wir = StdfReader.ParseWir(records[index++]);
        Assert.Equal(wafer.Id, wir.WaferId);
        var wcr = StdfReader.ParseWcr(records[index++]);
        Assert.Equal(100f, wcr.WaferSize);
        Assert.Equal(10f, wcr.DieWidth);
        Assert.Equal(10f, wcr.DieHeight);
        Assert.Equal((byte)3, wcr.WaferUnits); // mm
        foreach (var dieResult in wafer.CpResults)
        {
            var pir = StdfReader.ParsePir(records[index++]);
            Assert.InRange(pir.SiteNumber, (byte)1, (byte)siteCount);
            observedSites.Add(pir.SiteNumber);
            for (var itemIndex = 0; itemIndex < itemCount; itemIndex++)
            {
                var item = dataset.CpProgram.Items[itemIndex];
                if (item is FunctionalTestItem)
                {
                    var ftr = StdfReader.ParseFtr(records[index++]);
                    Assert.Equal(item.Name, ftr.TestText);
                    Assert.Equal(pir.SiteNumber, ftr.SiteNumber);
                    Assert.Equal(dieResult.Tests[itemIndex].Passed, ftr.Passed);
                }
                else
                {
                    var ptr = StdfReader.ParsePtr(records[index++]);
                    Assert.Equal(item.Name, ptr.TestText);
                    Assert.Equal(pir.SiteNumber, ptr.SiteNumber);
                }
            }

            var prr = StdfReader.ParsePrr(records[index++]);
            Assert.Equal(pir.SiteNumber, prr.SiteNumber);
        }

        var wrr = StdfReader.ParseWrr(records[index++]);
        Assert.Equal(wafer.Id, wrr.WaferId);
        Assert.Equal((uint)wafer.CpResults.Count, wrr.PartCount);
        Assert.Equal((uint)wafer.NetDie, wrr.GoodCount);
        Assert.Equal((uint)wafer.CpResults.Count, wrr.FunctionalCount);

        for (var site = 1; site <= siteCount; site++)
        {
            for (var itemIndex = 0; itemIndex < itemCount; itemIndex++)
            {
                var tsr = StdfReader.ParseTsr(records[index++]);
                var item = dataset.CpProgram.Items[itemIndex];
                Assert.Equal((byte)site, tsr.SiteNumber);
                Assert.Equal(item.Name, tsr.TestName);
                Assert.Equal(
                    item is FunctionalTestItem ? 'F' : 'P',
                    tsr.TestType);
                Assert.Equal((uint)wafer.CpResults.Count(result => result.Site == site), tsr.ExecutionCount);
                Assert.Equal(
                    (uint)wafer.CpResults.Count(result =>
                        result.Site == site &&
                        !result.Tests[itemIndex].Passed),
                    tsr.FailCount);
            }
        }

        foreach (var hardBin in dataset.Cp.HardBins)
        {
            for (var site = 1; site <= siteCount; site++)
            {
                var hbr = StdfReader.ParseHbr(records[index++]);
                Assert.Equal((byte)site, hbr.SiteNumber);
                Assert.Equal((ushort)hardBin.Number, hbr.HardBinNumber);
                Assert.Equal(hardBin.Name, hbr.Name);
                Assert.Equal(hardBin.Pass, hbr.Pass);
                Assert.Equal(
                    (uint)wafer.CpResults.Count(result =>
                        result.Site == site && result.HardBin == hardBin.Number),
                    hbr.Count);
            }
        }

        foreach (var softBin in dataset.Cp.SoftBins)
        {
            for (var site = 1; site <= siteCount; site++)
            {
                var sbr = StdfReader.ParseSbr(records[index++]);
                Assert.Equal((byte)site, sbr.SiteNumber);
                Assert.Equal((ushort)softBin.Number, sbr.SoftBinNumber);
                Assert.Equal(softBin.Name, sbr.Name);
                Assert.Equal(softBin.Pass, sbr.Pass);
                Assert.Equal(
                    (uint)wafer.CpResults.Count(result =>
                        result.Site == site && result.SoftBin == softBin.Number),
                    sbr.Count);
            }
        }

        for (var site = 1; site <= siteCount; site++)
        {
            var pcr = StdfReader.ParsePcr(records[index++]);
            Assert.Equal((byte)site, pcr.SiteNumber);
            Assert.Equal((uint)wafer.CpResults.Count(result => result.Site == site), pcr.PartCount);
            Assert.Equal(
                (uint)wafer.CpResults.Count(result => result.Site == site && result.Passed),
                pcr.GoodCount);
        }

        Assert.Equal(records.Count - 1, index);
        Assert.Equal((byte)1, records[index].Type);
        Assert.Equal((byte)20, records[index].Sub);

        Assert.Equal(Enumerable.Range(1, siteCount).Select(number => (byte)number), observedSites.Order());

        var recordKinds = records.Select(record => (record.Type, record.Sub)).ToHashSet();
        foreach (var kind in StdfRecordKinds.CpV4Set)
        {
            Assert.Contains(kind, recordKinds);
        }
    }

    [Fact]
    public void Export_StdfCpParametricPtrsCarryUnitsLimitsAndSites()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(SmallGeometry);
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);
        var records = StdfReader.Read(Assert.Single(result.CpStdfPaths));
        var ptrs = records.Where(record => record.Type == 15 && record.Sub == 10)
            .Select(StdfReader.ParsePtr)
            .ToArray();

        var numericItems = dataset.CpProgram.Items.OfType<NumericTestItem>().ToArray();
        Assert.Equal(numericItems.Length * dataset.DieTotals.GrossDie, ptrs.Length);

        Assert.All(numericItems, item =>
        {
            var itemPtrs = ptrs.Where(ptr => ptr.TestText == item.Name).ToArray();
            Assert.Equal(dataset.DieTotals.GrossDie, itemPtrs.Length);
            Assert.All(itemPtrs, ptr =>
            {
                Assert.Equal((uint)item.TestNumber, ptr.TestNumber);
                Assert.False(string.IsNullOrWhiteSpace(ptr.TestText));
                Assert.False(string.IsNullOrWhiteSpace(ptr.Unit));
                Assert.InRange(ptr.SiteNumber, (byte)1, (byte)dataset.CpProgram.SiteCount);
                Assert.Equal(0x02, ptr.OptionalFlags); // bit 1 is reserved and must be 1
                Assert.Equal(0, ptr.ResultScale);
                Assert.Equal((float)item.TestLimits.Low, ptr.LowLimit);
                Assert.Equal((float)item.TestLimits.High, ptr.HighLimit);
                Assert.Equal((float)item.SpecLimits.Low, ptr.LowSpec);
                Assert.Equal((float)item.SpecLimits.High, ptr.HighSpec);
                Assert.Equal(
                    ptr.Passed,
                    ptr.Result >= ptr.LowLimit && ptr.Result <= ptr.HighLimit);
            });
        });

        var vT = ptrs.First(ptr => ptr.TestText == "Vt");
        Assert.Equal(1001u, vT.TestNumber);
        Assert.Equal("V", vT.Unit);
        Assert.Equal(0.40f, vT.LowLimit);
        Assert.Equal(0.50f, vT.HighLimit);
        Assert.Equal(0.39f, vT.LowSpec);
        Assert.Equal(0.51f, vT.HighSpec);
    }

    [Fact]
    public void Export_StdfCpFunctionalFtrsCarryNameSiteAndResult()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(SmallGeometry);
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);
        var ftrs = StdfReader.Read(Assert.Single(result.CpStdfPaths))
            .Where(record => record.Type == 15 && record.Sub == 20)
            .Select(StdfReader.ParseFtr)
            .ToArray();

        var functionalItems = dataset.CpProgram.Items.OfType<FunctionalTestItem>().ToArray();
        Assert.Equal(functionalItems.Length * dataset.DieTotals.GrossDie, ftrs.Length);

        Assert.All(functionalItems, item =>
        {
            var itemFtrs = ftrs.Where(ftr => ftr.TestText == item.Name).ToArray();
            Assert.Equal(dataset.DieTotals.GrossDie, itemFtrs.Length);
            Assert.All(itemFtrs, ftr =>
            {
                Assert.Equal((uint)item.TestNumber, ftr.TestNumber);
                Assert.InRange(ftr.SiteNumber, (byte)1, (byte)dataset.CpProgram.SiteCount);
                Assert.Equal(0xFF, ftr.OptionalFlags);
                Assert.Equal(ftr.Passed ? "PASS" : "FAIL", ftr.ResultText);
                // Bit 6 clear means the P/F flag in bit 7 is valid.
                Assert.True((ftr.TestFlags & 0x40) == 0);
            });
        });
    }

    [Fact]
    public void Export_StdfCpWithConfiguredSiteCount_DeclaresAndUsesOnlyThoseSites()
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var config = pipeline.Validate(workspace.WriteConfig(TestConfigs.WithCp(
            """{ "siteCount": 2 }""", SmallGeometry))).Config!;
        var dataset = pipeline.Generate(config);
        var outputDirectory = workspace.PathFor("dataset");

        var result = pipeline.Export(dataset, outputDirectory);
        var records = StdfReader.Read(Assert.Single(result.CpStdfPaths));

        var sdr = StdfReader.ParseSdr(
            records.First(record => record.Type == 1 && record.Sub == 80));
        Assert.Equal((byte)2, sdr.SiteCount);
        Assert.Equal(new byte[] { 1, 2 }, sdr.SiteNumbers);

        var partSites = records
            .Where(record => record.Type == 5 && record.Sub == 10)
            .Select(record => StdfReader.ParsePir(record).SiteNumber)
            .Distinct()
            .Order()
            .ToArray();
        Assert.Equal(new byte[] { 1, 2 }, partSites);

        var prrSites = records
            .Where(record => record.Type == 5 && record.Sub == 20)
            .Select(record => StdfReader.ParsePrr(record).SiteNumber)
            .Distinct()
            .Order()
            .ToArray();
        Assert.Equal(new byte[] { 1, 2 }, prrSites);

        var pcrSites = records
            .Where(record => record.Type == 1 && record.Sub == 30)
            .Select(record => StdfReader.ParsePcr(record).SiteNumber)
            .ToArray();
        Assert.Equal(new byte[] { 1, 2 }, pcrSites);
    }

    [Fact]
    public void Export_StdfCpWaferRecordsDescribeEachWaferAndOrientation()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(
            """
            {
              "diameterMm": 100,
              "edgeExclusionMm": 0,
              "notchAngleDeg": 90,
              "die": { "widthMm": 10, "heightMm": 10, "scribeMm": 0 },
              "grid": { "rotationDeg": 180, "mirrorX": true }
            }
            """,
            lotsJson: """[ { "id": "LOT-0001", "waferCount": 2 } ]""");
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);
        var records = StdfReader.Read(Assert.Single(result.CpStdfPaths)).Skip(3).ToArray();
        var waferRecords = new List<(WirInfo Wir, WcrInfo Wcr, WrrInfo Wrr)>();
        var index = 0;
        while (records[index].Type == 2 && records[index].Sub == 10)
        {
            var wir = StdfReader.ParseWir(records[index++]);
            var wcr = StdfReader.ParseWcr(records[index++]);
            while (records[index].Type != 2 || records[index].Sub != 20)
            {
                index++;
            }

            waferRecords.Add((wir, wcr, StdfReader.ParseWrr(records[index++])));
        }

        var wafers = Assert.Single(dataset.Lots).Wafers;
        Assert.Equal(wafers.Count, waferRecords.Count);
        for (var waferIndex = 0; waferIndex < wafers.Count; waferIndex++)
        {
            var wafer = wafers[waferIndex];
            var (wir, wcr, wrr) = waferRecords[waferIndex];
            Assert.Equal(wafer.Id, wir.WaferId);
            Assert.Equal(100f, wcr.WaferSize);
            Assert.Equal(10f, wcr.DieWidth);
            Assert.Equal(10f, wcr.DieHeight);
            Assert.Equal((byte)3, wcr.WaferUnits); // mm
            Assert.Equal((byte)0, wcr.WaferFlat); // notch at 90° points up
            Assert.Equal(0, wcr.CenterX);
            Assert.Equal(0, wcr.CenterY);
            Assert.Equal((sbyte)1, wcr.PositiveX); // mirrorX + 180° maps +X to the right
            Assert.Equal((sbyte)2, wcr.PositiveY); // 180° maps +Y down
            Assert.Equal(wafer.Id, wrr.WaferId);
            Assert.Equal((uint)wafer.CpResults.Count, wrr.PartCount);
            Assert.Equal((uint)wafer.NetDie, wrr.GoodCount);
        }
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
    public void Export_ManifestRecordsTheEffectiveSiteConfiguration()
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var config = pipeline.Validate(workspace.WriteConfig(TestConfigs.WithCp(
            """
            {
              "siteCount": 3,
              "testItems": [ { "name": "Vt", "siteOffsets": [0.001, 0.002, 0.003] } ]
            }
            """,
            SmallGeometry))).Config!;
        var dataset = pipeline.Generate(config);
        var result = pipeline.Export(dataset, workspace.PathFor("dataset"));

        using var manifest = JsonDocument.Parse(File.ReadAllText(result.ManifestPath!));
        var cp = manifest.RootElement.GetProperty("resolvedConfig").GetProperty("cp");
        Assert.Equal(3, cp.GetProperty("siteCount").GetInt32());
        var item = Assert.Single(cp.GetProperty("testItems").EnumerateArray());
        Assert.Equal("Vt", item.GetProperty("name").GetString());
        Assert.Equal(
            new[] { 0.001, 0.002, 0.003 },
            item.GetProperty("siteOffsets").EnumerateArray()
                .Select(value => value.GetDouble()));
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
