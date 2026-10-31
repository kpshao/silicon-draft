using System.Globalization;
using System.Text;
using SiliconDraft.TestSupport;

namespace SiliconDraft.Core.Tests;

public sealed class TestDataCsvExportTests
{
    [Fact]
    public void Export_WritesOneCpAndFtCsvPerWafer()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(lotsJson: """[ { "id": "LOT-0001", "waferCount": 2 } ]""");
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);

        Assert.True(result.Success);
        Assert.Equal(2, result.CpCsvPaths.Count);
        Assert.Equal(2, result.FtCsvPaths.Count);
        Assert.Equal(
            Path.Combine(outputDirectory, "csv", "LOT-0001-W01_CP.csv"),
            result.CpCsvPaths[0]);
        Assert.Equal(
            Path.Combine(outputDirectory, "csv", "LOT-0001-W02_CP.csv"),
            result.CpCsvPaths[1]);
        Assert.Equal(
            Path.Combine(outputDirectory, "csv", "LOT-0001-W01_FT.csv"),
            result.FtCsvPaths[0]);
        Assert.Equal(
            Path.Combine(outputDirectory, "csv", "LOT-0001-W02_FT.csv"),
            result.FtCsvPaths[1]);
        Assert.All(result.CpCsvPaths, path => Assert.True(File.Exists(path)));
        Assert.All(result.FtCsvPaths, path => Assert.True(File.Exists(path)));
    }

    [Fact]
    public void CpCsv_HasWideTestColumnsAndReconcilesWithManifest()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate();
        var outputDirectory = workspace.PathFor("dataset");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);

        var wafer = dataset.Lots[0].Wafers[0];
        var csv = ReadCsv(Assert.Single(result.CpCsvPaths));

        Assert.Equal("CP", MetadataValue(csv.Metadata, "step"));
        Assert.Equal("DemoProduct", MetadataValue(csv.Metadata, "product"));
        Assert.Equal("LOT-0001", MetadataValue(csv.Metadata, "lotId"));
        Assert.Equal("LOT-0001-W01", MetadataValue(csv.Metadata, "waferId"));
        Assert.Equal(Number(dataset.Seed), MetadataValue(csv.Metadata, "seed"));
        Assert.Equal(
            dataset.CpProgram.CatalogVersion, MetadataValue(csv.Metadata, "catalogVersion"));
        Assert.Equal(
            Number(dataset.CpProgram.SiteCount), MetadataValue(csv.Metadata, "siteCount"));
        Assert.Equal(
            Number(wafer.DieMap.Counts.GrossDie), MetadataValue(csv.Metadata, "grossDie"));
        Assert.Equal(Number(wafer.CpResults.Count), MetadataValue(csv.Metadata, "testedDie"));
        Assert.Equal(Number(wafer.NetDie), MetadataValue(csv.Metadata, "passedDie"));
        Assert.Equal(
            Number(wafer.DieMap.Counts.PartialDieFound),
            MetadataValue(csv.Metadata, "partialDieFound"));
        Assert.Equal(
            Number(wafer.DieMap.Counts.PartialDieRetained),
            MetadataValue(csv.Metadata, "partialDieRetained"));
        Assert.Equal(
            Number(dataset.CpProgram.Items.Count),
            MetadataValue(csv.Metadata, "testItemCount"));
        Assert.Equal(
            Math.Round((double)wafer.NetDie / wafer.CpResults.Count, 6)
                .ToString("0.######", CultureInfo.InvariantCulture),
            MetadataValue(csv.Metadata, "yield"));

        Assert.Equal(dataset.CpProgram.Items.Count + 7, csv.Header.Length);
        Assert.Equal(
            new[] { "dieX", "dieY", "site", "hardBin", "softBin", "pass", "firstFail" },
            csv.Header.Take(7));
        Assert.Equal(
            dataset.CpProgram.Items.Select(item => item.Name),
            csv.Header.Skip(7));
        Assert.Equal(wafer.CpResults.Count, csv.Rows.Count);

        var hardBins = new[]
        {
            dataset.CpProgram.Bins.PassHardBin,
            dataset.CpProgram.Bins.ParametricFailHardBin,
            dataset.CpProgram.Bins.FunctionalFailHardBin,
        };
        var softBins = new[] { dataset.CpProgram.Bins.PassSoftBin }
            .Concat(dataset.CpProgram.Items.Select(item => item.FailSoftBin))
            .ToArray();
        var itemNames = dataset.CpProgram.Items
            .Select(item => item.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var row in csv.Rows)
        {
            Assert.Equal(dataset.CpProgram.Items.Count + 7, row.Length);
            Assert.InRange(
                int.Parse(row[2], CultureInfo.InvariantCulture),
                1,
                dataset.CpProgram.SiteCount);
            Assert.Contains(int.Parse(row[3], CultureInfo.InvariantCulture), hardBins);
            Assert.Contains(int.Parse(row[4], CultureInfo.InvariantCulture), softBins);

            var passed = bool.Parse(row[5]);
            if (passed)
            {
                Assert.Equal(string.Empty, row[6]);
            }
            else
            {
                Assert.Contains(row[6], itemNames);
            }

            for (var index = 7; index < row.Length; index++)
            {
                Assert.True(double.TryParse(
                    row[index], NumberStyles.Float, CultureInfo.InvariantCulture, out _));
            }
        }

        AssertRowsSortedByYThenX(csv.Rows, xIndex: 0, yIndex: 1);

        var itemDefinitions = csv.Metadata
            .Where(line => line.StartsWith("#testItem,", StringComparison.Ordinal))
            .Skip(1)
            .Select(ParseLine)
            .ToArray();
        Assert.Equal(dataset.CpProgram.Items.Count, itemDefinitions.Length);
        var vt = itemDefinitions.Single(fields => fields[1] == "Vt");
        Assert.Equal(
            new[]
            {
                "#testItem", "Vt", "1001", "numeric", "V",
                "0.39", "0.51", "0.4", "0.5", "100",
            },
            vt);

        var hardBinDefinitions = csv.Metadata
            .Where(line => line.StartsWith("#hardBin,", StringComparison.Ordinal))
            .Skip(1)
            .Select(ParseLine)
            .ToArray();
        Assert.Equal(3, hardBinDefinitions.Length);
        Assert.Contains(
            hardBinDefinitions,
            fields => fields[1] == "1" && fields[2] == "Pass" && fields[3] == "true");

        var softBinDefinitions = csv.Metadata
            .Where(line => line.StartsWith("#softBin,", StringComparison.Ordinal))
            .Skip(1)
            .Select(ParseLine)
            .ToArray();
        Assert.Equal(dataset.CpProgram.Items.Count + 1, softBinDefinitions.Length);
        Assert.Contains(
            softBinDefinitions,
            fields => fields[1] == "100" && fields[2] == "Vt" && fields[3] == "false");
    }

    [Fact]
    public void FtCsv_HasOneRowPerFtUnitAndMarksEscapes()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate(ftJson: """{ "assemblyLoss": 2, "escapeRate": 1 }""");
        var lot = dataset.Lots[0];
        var wafer = lot.Wafers[0];
        var summary = lot.GetFtSummary(dataset.FtProgram);
        Assert.Equal(2, summary.AssemblyLoss);
        Assert.True(summary.EscapeUnits > 0);

        var outputDirectory = workspace.PathFor("dataset");
        var result = new GenerationPipeline().Export(dataset, outputDirectory);
        var csv = ReadCsv(Assert.Single(result.FtCsvPaths));

        Assert.Equal("FT", MetadataValue(csv.Metadata, "step"));
        Assert.Equal(lot.AssemblyLot, MetadataValue(csv.Metadata, "assemblyLot"));
        Assert.Equal(Number(wafer.NetDie), MetadataValue(csv.Metadata, "cpPassedDie"));
        Assert.Equal(Number(summary.AssemblyLoss), MetadataValue(csv.Metadata, "assemblyLoss"));
        Assert.Equal(Number(summary.EscapeUnits), MetadataValue(csv.Metadata, "escapeUnits"));
        Assert.Equal(Number(summary.UnitCount), MetadataValue(csv.Metadata, "ftUnits"));
        Assert.Equal(Number(summary.PassedUnits), MetadataValue(csv.Metadata, "passedUnits"));
        Assert.Equal(
            summary.Yield.ToString("0.######", CultureInfo.InvariantCulture),
            MetadataValue(csv.Metadata, "yield"));
        Assert.Equal(
            int.Parse(MetadataValue(csv.Metadata, "cpPassedDie"), CultureInfo.InvariantCulture)
                - int.Parse(MetadataValue(csv.Metadata, "assemblyLoss"), CultureInfo.InvariantCulture)
                + int.Parse(MetadataValue(csv.Metadata, "escapeUnits"), CultureInfo.InvariantCulture),
            int.Parse(MetadataValue(csv.Metadata, "ftUnits"), CultureInfo.InvariantCulture));

        Assert.Equal(dataset.FtProgram.Items.Count + 9, csv.Header.Length);
        Assert.Equal(
            new[]
            {
                "unitId", "dieX", "dieY", "cpResult", "site",
                "hardBin", "softBin", "pass", "firstFail",
            },
            csv.Header.Take(9));
        Assert.Equal(dataset.FtProgram.Items.Select(item => item.Name), csv.Header.Skip(9));
        Assert.Equal(summary.UnitCount, csv.Rows.Count);
        Assert.Equal(
            lot.FtUnits.Select(unit => unit.Id).OrderBy(id => id, StringComparer.Ordinal),
            csv.Rows.Select(row => row[0]).OrderBy(id => id, StringComparer.Ordinal));

        var cpResults = csv.Rows.ToDictionary(
            row => row[0], row => row[3], StringComparer.Ordinal);
        foreach (var unit in lot.FtUnits)
        {
            Assert.Equal(unit.CpPassed ? "Pass" : "Fail", cpResults[unit.Id]);
        }

        Assert.Equal(summary.EscapeUnits, csv.Rows.Count(row => row[3] == "Fail"));
        Assert.All(csv.Rows, row => Assert.Equal("1", row[4]));

        var hardBins = new[]
        {
            dataset.FtProgram.Bins.PassHardBin,
            dataset.FtProgram.Bins.ParametricFailHardBin,
            dataset.FtProgram.Bins.FunctionalFailHardBin,
        };
        var softBins = new[] { dataset.FtProgram.Bins.PassSoftBin }
            .Concat(dataset.FtProgram.Items.Select(item => item.FailSoftBin))
            .ToArray();
        var itemNames = dataset.FtProgram.Items
            .Select(item => item.Name)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var row in csv.Rows)
        {
            Assert.Equal(dataset.FtProgram.Items.Count + 9, row.Length);
            Assert.Contains(int.Parse(row[5], CultureInfo.InvariantCulture), hardBins);
            Assert.Contains(int.Parse(row[6], CultureInfo.InvariantCulture), softBins);
            var passed = bool.Parse(row[7]);
            if (passed)
            {
                Assert.Equal(string.Empty, row[8]);
            }
            else
            {
                Assert.Contains(row[8], itemNames);
            }
        }

        AssertRowsSortedByYThenX(csv.Rows, xIndex: 1, yIndex: 2);

        var itemDefinitions = csv.Metadata
            .Where(line => line.StartsWith("#testItem,", StringComparison.Ordinal))
            .Skip(1)
            .Select(ParseLine)
            .ToArray();
        Assert.Equal(dataset.FtProgram.Items.Count, itemDefinitions.Length);
        var icc = itemDefinitions.Single(fields => fields[1] == "Icc");
        Assert.Equal(
            new[]
            {
                "#testItem", "Icc", "4001", "numeric", "uA",
                "0", "21", "0", "18", "300",
            },
            icc);
    }

    [Fact]
    public void FtCsv_WithoutFtUnits_WritesMetadataAndHeaderOnly()
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var config = pipeline.Validate(workspace.WriteConfig(
            TestConfigs.WithCp(
                """
                { "testItems": [
                    { "name": "Vt", "testLimits": { "low": 100, "high": 200 } }
                ] }
                """,
                SmallGeometry))).Config!;
        var dataset = pipeline.Generate(config);
        Assert.Equal(0, dataset.Cp.PassedDie);
        Assert.Equal(0, dataset.Ft.UnitCount);

        var outputDirectory = workspace.PathFor("dataset");
        var result = pipeline.Export(dataset, outputDirectory);

        var csv = ReadCsv(Assert.Single(result.FtCsvPaths));
        Assert.Equal("0", MetadataValue(csv.Metadata, "cpPassedDie"));
        Assert.Equal("0", MetadataValue(csv.Metadata, "assemblyLoss"));
        Assert.Equal("0", MetadataValue(csv.Metadata, "escapeUnits"));
        Assert.Equal("0", MetadataValue(csv.Metadata, "ftUnits"));
        Assert.Equal("0", MetadataValue(csv.Metadata, "passedUnits"));
        Assert.Equal("0", MetadataValue(csv.Metadata, "yield"));
        Assert.Empty(csv.Rows);
        Assert.Equal(dataset.FtProgram.Items.Count + 9, csv.Header.Length);

        var cpCsv = ReadCsv(Assert.Single(result.CpCsvPaths));
        Assert.Equal(dataset.Cp.TestedDie, cpCsv.Rows.Count);
    }

    [Fact]
    public void Export_SameConfigAndSeed_ProducesIdenticalCsvFiles()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate();
        var pipeline = new GenerationPipeline();

        var first = pipeline.Export(dataset, workspace.PathFor("run-a"));
        var second = pipeline.Export(dataset, workspace.PathFor("run-b"));

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(
            File.ReadAllBytes(Assert.Single(first.CpCsvPaths)),
            File.ReadAllBytes(Assert.Single(second.CpCsvPaths)));
        Assert.Equal(
            File.ReadAllBytes(Assert.Single(first.FtCsvPaths)),
            File.ReadAllBytes(Assert.Single(second.FtCsvPaths)));
    }

    [Fact]
    public void Export_WhenCsvAlreadyExists_RequiresOverwriteConfirmation()
    {
        using var workspace = new TestWorkspace();
        var dataset = Generate();
        var outputDirectory = workspace.PathFor("dataset");
        var csvPath = Path.Combine(outputDirectory, "csv", "LOT-0001-W01_CP.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(csvPath)!);
        File.WriteAllText(csvPath, "stale\n");

        var result = new GenerationPipeline().Export(dataset, outputDirectory);

        Assert.False(result.Success);
        Assert.Contains("already exists", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LOT-0001-W01_CP.csv", result.Error, StringComparison.OrdinalIgnoreCase);

        var overwritten = new GenerationPipeline().Export(
            dataset, outputDirectory, overwrite: true);
        Assert.True(overwritten.Success);
        Assert.DoesNotContain("stale", File.ReadAllText(csvPath));
    }

    private static void AssertRowsSortedByYThenX(
        IReadOnlyList<string[]> rows, int xIndex, int yIndex)
    {
        var previous = (Y: int.MinValue, X: int.MinValue);
        foreach (var row in rows)
        {
            var current = (
                Y: int.Parse(row[yIndex], CultureInfo.InvariantCulture),
                X: int.Parse(row[xIndex], CultureInfo.InvariantCulture));
            Assert.True(
                current.Y > previous.Y || (current.Y == previous.Y && current.X >= previous.X),
                "rows must be ordered by Y then X");
            previous = current;
        }
    }

    private static CsvFile ReadCsv(string path)
    {
        var lines = File.ReadAllLines(path);
        var blankIndex = Array.FindIndex(lines, line => line.Length == 0);
        Assert.True(blankIndex > 0, "expected a blank line between metadata and data");
        Assert.All(lines.Take(blankIndex), line => Assert.StartsWith("#", line));
        var metadata = lines.Take(blankIndex).ToArray();
        var header = ParseLine(lines[blankIndex + 1]);
        var rows = lines
            .Skip(blankIndex + 2)
            .Where(line => line.Length > 0)
            .Select(ParseLine)
            .ToArray();
        return new CsvFile(metadata, header, rows);
    }

    private static string MetadataValue(IReadOnlyList<string> metadata, string key)
    {
        var prefix = $"#{key}=";
        var line = metadata.Single(
            line => line.StartsWith(prefix, StringComparison.Ordinal));
        return line[prefix.Length..];
    }

    private static string[] ParseLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (quoted)
            {
                if (character == '"')
                {
                    if (index + 1 < line.Length && line[index + 1] == '"')
                    {
                        current.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    current.Append(character);
                }
            }
            else if (character == '"')
            {
                quoted = true;
            }
            else if (character == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static Dataset Generate(
        string? ftJson = null,
        string lotsJson = """[ { "id": "LOT-0001", "waferCount": 1 } ]""")
    {
        using var workspace = new TestWorkspace();
        var json = ftJson is null
            ? TestConfigs.WithWafer(SmallGeometry, lotsJson)
            : TestConfigs.WithFt(ftJson, SmallGeometry, lotsJson);
        var pipeline = new GenerationPipeline();
        return pipeline.Generate(pipeline.Validate(workspace.WriteConfig(json)).Config!);
    }

    private const string SmallGeometry = """
        {
          "diameterMm": 100,
          "edgeExclusionMm": 0,
          "die": { "widthMm": 10, "heightMm": 10, "scribeMm": 0 }
        }
        """;

    private sealed record CsvFile(
        IReadOnlyList<string> Metadata,
        string[] Header,
        IReadOnlyList<string[]> Rows);
}
