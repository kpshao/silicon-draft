using System.Text.Json;
using SiliconDraft.TestSupport;

namespace SiliconDraft.Core.Tests;

public sealed class LatentGenerationTests
{
    private const string TenLots = """
        [
          { "id": "LOT-0001", "waferCount": 2 },
          { "id": "LOT-0002", "waferCount": 2 },
          { "id": "LOT-0003", "waferCount": 2 },
          { "id": "LOT-0004", "waferCount": 2 },
          { "id": "LOT-0005", "waferCount": 2 },
          { "id": "LOT-0006", "waferCount": 2 },
          { "id": "LOT-0007", "waferCount": 2 },
          { "id": "LOT-0008", "waferCount": 2 },
          { "id": "LOT-0009", "waferCount": 2 },
          { "id": "LOT-0010", "waferCount": 2 }
        ]
        """;

    private const string VarianceLots = """
        [
          { "id": "LOT-0001", "waferCount": 5 },
          { "id": "LOT-0002", "waferCount": 5 },
          { "id": "LOT-0003", "waferCount": 5 },
          { "id": "LOT-0004", "waferCount": 5 },
          { "id": "LOT-0005", "waferCount": 5 },
          { "id": "LOT-0006", "waferCount": 5 },
          { "id": "LOT-0007", "waferCount": 5 },
          { "id": "LOT-0008", "waferCount": 5 },
          { "id": "LOT-0009", "waferCount": 5 },
          { "id": "LOT-0010", "waferCount": 5 }
        ]
        """;

    private const string FourWafers = """
        [
          { "id": "LOT-0001", "waferCount": 2 },
          { "id": "LOT-0002", "waferCount": 2 }
        ]
        """;

    private const string SmallGeometry = """
        {
          "diameterMm": 100,
          "edgeExclusionMm": 0,
          "die": { "widthMm": 5, "heightMm": 5, "scribeMm": 0 }
        }
        """;

    [Fact]
    public void Generate_WithDefaultLatentModel_AchievesEveryDefaultDiePairTarget()
    {
        var dataset = Generate(TestConfigs.WithWafer(SmallGeometry, TenLots));

        Assert.True(dataset.Ft.UnitCount > 1000, $"only {dataset.Ft.UnitCount} FT units");
        var failures = new List<string>();
        foreach (var pair in dataset.Config.Latent.DiePairs)
        {
            var achieved = Correlation(dataset, pair.CpItem, pair.FtItem);
            if (achieved is null ||
                achieved.Value < pair.TargetR - pair.Tolerance ||
                achieved.Value > pair.TargetR + pair.Tolerance)
            {
                failures.Add(
                    $"{pair.CpItem}/{pair.FtItem}: target {pair.TargetR}, " +
                    $"achieved {achieved?.ToString() ?? "null"}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("; ", failures));
    }

    [Fact]
    public void Generate_WithFewSamples_CalibratesTheAchievedCorrelationIntoTheTolerance()
    {
        var dataset = Generate(TestConfigs.WithLatent(
            """
            {
              "variance": {
                "lot": 0.05,
                "wafer": 0.1,
                "spatial": 0.1,
                "die": 0.6,
                "measurement": 0.15
              },
              "diePairs": [
                { "cpItem": "Idsat", "ftItem": "Ioh", "targetR": 0.65 },
                { "cpItem": "Vt", "ftItem": "Fmax", "targetR": -0.2 },
                { "cpItem": "Cin", "ftItem": "Cin", "targetR": 0.75 }
              ]
            }
            """,
            SmallGeometry,
            FourWafers));

        Assert.All(dataset.DieCorrelations, result =>
        {
            Assert.NotNull(result.AchievedR);
            Assert.InRange(
                result.AchievedR!.Value,
                result.TargetR - result.Tolerance,
                result.TargetR + result.Tolerance);
        });
    }

    [Fact]
    public void Export_WithDefaultLatentModel_ManifestRecordsAchievedDieCorrelations()
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var config = pipeline.Validate(
            workspace.WriteConfig(TestConfigs.WithWafer(SmallGeometry, TenLots))).Config!;
        var dataset = pipeline.Generate(config);

        var result = pipeline.Export(dataset, workspace.PathFor("dataset"));
        Assert.True(result.Success);

        using var manifest = JsonDocument.Parse(
            File.ReadAllText(result.ManifestPath!));
        var root = manifest.RootElement;
        Assert.Equal(8, root.GetProperty("manifestSchemaVersion").GetInt32());

        var resolvedLatent = root.GetProperty("resolvedConfig").GetProperty("latent");
        var resolvedVariance = resolvedLatent.GetProperty("variance");
        Assert.Equal(config.Latent.Variance.Lot, resolvedVariance.GetProperty("lot").GetDouble());
        Assert.Equal(
            config.Latent.DiePairs.Count,
            resolvedLatent.GetProperty("diePairs").GetArrayLength());

        var correlations = root.GetProperty("dataset")
            .GetProperty("correlations")
            .GetProperty("die");
        Assert.Equal(config.Latent.DiePairs.Count, correlations.GetArrayLength());
        foreach (var entry in correlations.EnumerateArray())
        {
            var cpItem = entry.GetProperty("cpItem").GetString();
            var ftItem = entry.GetProperty("ftItem").GetString();
            var target = entry.GetProperty("targetR").GetDouble();
            var tolerance = entry.GetProperty("tolerance").GetDouble();
            var achieved = entry.GetProperty("achievedR").GetDouble();
            var withinTolerance = entry.GetProperty("withinTolerance").GetBoolean();
            var sampleCount = entry.GetProperty("sampleCount").GetInt32();

            var configured = config.Latent.DiePairs.Single(
                pair => pair.CpItem == cpItem && pair.FtItem == ftItem);
            Assert.Equal(configured.TargetR, target);
            Assert.Equal(configured.Tolerance, tolerance);
            Assert.True(sampleCount > 0);
            Assert.True(withinTolerance);
            Assert.InRange(achieved, target - tolerance, target + tolerance);
        }
    }

    [Fact]
    public void Export_WithNoDiePairs_ManifestCorrelationSectionIsEmpty()
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var config = pipeline.Validate(workspace.WriteConfig(
            TestConfigs.WithLatent("""{ "diePairs": [] }"""))).Config!;
        var dataset = pipeline.Generate(config);

        var result = pipeline.Export(dataset, workspace.PathFor("dataset"));
        Assert.True(result.Success);

        using var manifest = JsonDocument.Parse(
            File.ReadAllText(result.ManifestPath!));
        Assert.Empty(dataset.DieCorrelations);
        Assert.Equal(
            0,
            manifest.RootElement.GetProperty("dataset")
                .GetProperty("correlations")
                .GetProperty("die")
                .GetArrayLength());
    }

    [Fact]
    public void Generate_WithConfiguredVarianceComponents_ShiftsVarianceToTheConfiguredLayer()
    {
        var lotHeavy = Generate(TestConfigs.WithLatent(
            """
            {
              "variance": {
                "lot": 0.5,
                "wafer": 0.05,
                "spatial": 0.05,
                "die": 0.2,
                "measurement": 0.2
              },
              "diePairs": []
            }
            """,
            SmallGeometry,
            VarianceLots));
        var waferHeavy = Generate(TestConfigs.WithLatent(
            """
            {
              "variance": {
                "lot": 0.02,
                "wafer": 0.6,
                "spatial": 0.03,
                "die": 0.15,
                "measurement": 0.2
              },
              "diePairs": []
            }
            """,
            SmallGeometry,
            VarianceLots));

        var lotHeavySpread = Spread(lotHeavy, "Vt");
        var waferHeavySpread = Spread(waferHeavy, "Vt");

        Assert.True(
            lotHeavySpread.BetweenLot > lotHeavySpread.BetweenWafer * 4,
            $"lot-heavy config: between-lot {lotHeavySpread.BetweenLot:g4}, " +
            $"between-wafer {lotHeavySpread.BetweenWafer:g4}");
        Assert.True(
            waferHeavySpread.BetweenWafer > waferHeavySpread.BetweenLot * 2.5,
            $"wafer-heavy config: between-lot {waferHeavySpread.BetweenLot:g4}, " +
            $"between-wafer {waferHeavySpread.BetweenWafer:g4}");
    }

    [Fact]
    public void Generate_WithPerItemSpatialLoadings_ChangesHowStronglyItemsShareThePattern()
    {
        const string baseVariance = """
            "variance": {
              "lot": 0.2,
              "wafer": 0.2,
              "spatial": 0.5,
              "die": 0.05,
              "measurement": 0.05
            },
            "diePairs": []
            """;
        var baseline = Generate(TestConfigs.WithLatent(
            $$"""{ {{baseVariance}} }""", SmallGeometry, TenLots));
        var loaded = Generate(TestConfigs.WithLatent(
            $$"""
            {
              {{baseVariance}},
              "spatialLoadings": [
                { "step": "cp", "name": "Vt", "loading": 3 },
                { "step": "cp", "name": "Idsat", "loading": 3 }
              ]
            }
            """,
            SmallGeometry,
            TenLots));

        var baselineR = ItemCorrelation(baseline, "Vt", "Idsat");
        var loadedR = ItemCorrelation(loaded, "Vt", "Idsat");

        Assert.True(
            loadedR > baselineR + 0.03,
            $"baseline {baselineR:g4}, loaded {loadedR:g4}");
    }

    [Fact]
    public void Generate_SameSeedAndConfig_ProducesTheSameNumericValues()
    {
        var json = TestConfigs.WithWafer(SmallGeometry, TenLots);

        var first = Generate(json);
        var second = Generate(json);

        Assert.Equal(Values(first), Values(second));
    }

    [Fact]
    public void Generate_DifferentSeed_ChangesTheNumericValues()
    {
        var first = Generate(TestConfigs.WithWafer(SmallGeometry, TenLots));
        var second = Generate(TestConfigs.WithWafer(SmallGeometry, TenLots)
            .Replace("\"seed\": 42", "\"seed\": 43"));

        Assert.NotEqual(Values(first), Values(second));
    }

    private static (double BetweenLot, double BetweenWafer) Spread(Dataset dataset, string itemName)
    {
        var wafers = dataset.Lots
            .SelectMany(lot => lot.Wafers.Select(wafer => (
                LotId: lot.Id,
                WaferId: wafer.Id,
                Mean: wafer.CpResults
                    .Select(result => result.Tests.Single(test => test.Item.Name == itemName).Value)
                    .Average())))
            .ToArray();
        var lotMeans = wafers
            .GroupBy(wafer => wafer.LotId)
            .Select(group => group.Average(wafer => wafer.Mean))
            .ToArray();
        var betweenLot = Variance(lotMeans);
        var withinLot = wafers
            .GroupBy(wafer => wafer.LotId)
            .SelectMany(group => group.Select(wafer => wafer.Mean - group.Average(w => w.Mean)))
            .ToArray();
        return (betweenLot, Variance(withinLot));
    }

    private static double Variance(IReadOnlyList<double> values)
    {
        var mean = values.Average();
        return values.Sum(value => (value - mean) * (value - mean)) / Math.Max(1, values.Count - 1);
    }

    private static string Values(Dataset dataset) =>
        string.Join(
            "|",
            dataset.Lots.SelectMany(lot => lot.Wafers).SelectMany(wafer => wafer.CpResults)
                .SelectMany(result => result.Tests)
                .Where(test => test.Item is NumericTestItem)
                .Select(test => $"{test.Item.Name}:{test.Value:R}"))
        + "||" +
        string.Join(
            "|",
            dataset.Lots.SelectMany(lot => lot.FtUnits).SelectMany(unit => unit.Tests)
                .Where(test => test.Item is NumericTestItem)
                .Select(test => $"{test.Item.Name}:{test.Value:R}"));

    private static Dataset Generate(string json)
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var config = pipeline.Validate(workspace.WriteConfig(json)).Config!;
        return pipeline.Generate(config);
    }

    /// <summary>
    /// Independently joins CP and FT records and recomputes Pearson r, so the test does
    /// not trust the production <see cref="CorrelationSummary"/> for its expected value.
    /// </summary>
    private static double? Correlation(Dataset dataset, string cpItem, string ftItem)
    {
        var values = new List<(double Cp, double Ft)>();
        foreach (var lot in dataset.Lots)
        {
            foreach (var wafer in lot.Wafers)
            {
                var cpByDie = wafer.CpResults.ToDictionary(
                    result => (result.Die.X, result.Die.Y));
                foreach (var unit in lot.FtUnits)
                {
                    if (unit.SourceWaferId != wafer.Id ||
                        !cpByDie.TryGetValue((unit.SourceDie.X, unit.SourceDie.Y), out var cp))
                    {
                        continue;
                    }

                    var cpValue = cp.Tests.Single(test => test.Item.Name == cpItem).Value;
                    var ftValue = unit.Tests.Single(test => test.Item.Name == ftItem).Value;
                    values.Add((cpValue, ftValue));
                }
            }
        }

        return Pearson(values);
    }

    private static double ItemCorrelation(Dataset dataset, string first, string second)
    {
        var values = dataset.Lots
            .SelectMany(lot => lot.Wafers)
            .SelectMany(wafer => wafer.CpResults)
            .Select(result => (
                First: result.Tests.Single(test => test.Item.Name == first).Value,
                Second: result.Tests.Single(test => test.Item.Name == second).Value))
            .ToArray();
        return Pearson(values.Select(value => (value.First, value.Second)).ToArray())!.Value;
    }

    private static double? Pearson(IReadOnlyList<(double X, double Y)> values)
    {
        if (values.Count < 2)
        {
            return null;
        }

        var meanX = 0.0;
        var meanY = 0.0;
        var sumXX = 0.0;
        var sumYY = 0.0;
        var sumXY = 0.0;
        var count = 0;
        foreach (var (x, y) in values)
        {
            count++;
            var dx = x - meanX;
            var dy = y - meanY;
            meanX += dx / count;
            meanY += dy / count;
            sumXX += dx * (x - meanX);
            sumYY += dy * (y - meanY);
            sumXY += dx * (y - meanY);
        }

        return sumXX <= 0 || sumYY <= 0
            ? null
            : Math.Round(sumXY / Math.Sqrt(sumXX * sumYY), 12);
    }
}
