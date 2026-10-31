using System.Globalization;
using System.Text;

namespace SiliconDraft.Core;

/// <summary>
/// Writes one Wafer's CP or FT results as a human-readable CSV. Each file starts with a
/// hashtag-prefixed metadata block carrying the identifiers, the catalog and Bin
/// definitions, and the counts a reader needs to interpret the data, followed by a blank
/// line and one wide row per tested Die or FT Unit. The data block stays a plain CSV so a
/// spreadsheet can open it directly; the metadata block carries the units and limits that
/// a column header cannot.
/// </summary>
internal static class TestDataCsvWriter
{
    public const string DirectoryName = "csv";
    public const string CpFileSuffix = "_CP.csv";
    public const string FtFileSuffix = "_FT.csv";
    public const int FormatVersion = 1;

    private const int FtSite = 1;

    public static string CpFileName(string waferId) => $"{waferId}{CpFileSuffix}";

    public static string FtFileName(string waferId) => $"{waferId}{FtFileSuffix}";

    public static void WriteCp(string path, Dataset dataset, Lot lot, Wafer wafer)
    {
        var program = dataset.CpProgram;
        var builder = new StringBuilder();

        WriteScalar(builder, "formatVersion", FormatInt(FormatVersion));
        WriteScalar(builder, "step", "CP");
        WriteScalar(builder, "product", dataset.ProductName);
        WriteScalar(builder, "lotId", lot.Id);
        WriteScalar(builder, "waferId", wafer.Id);
        WriteScalar(builder, "seed", FormatLong(dataset.Seed));
        WriteScalar(builder, "catalogVersion", program.CatalogVersion);
        WriteScalar(builder, "siteCount", FormatInt(program.SiteCount));

        var counts = wafer.DieMap.Counts;
        WriteScalar(builder, "grossDie", FormatInt(counts.GrossDie));
        WriteScalar(builder, "testedDie", FormatInt(wafer.CpResults.Count));
        WriteScalar(builder, "passedDie", FormatInt(wafer.NetDie));
        WriteScalar(builder, "yield", FormatYield(wafer.NetDie, wafer.CpResults.Count));
        WriteScalar(builder, "partialDieFound", FormatInt(counts.PartialDieFound));
        WriteScalar(builder, "partialDieRetained", FormatInt(counts.PartialDieRetained));
        WriteScalar(builder, "testItemCount", FormatInt(program.Items.Count));

        WriteItemDefinitions(builder, program);
        WriteBinDefinitions(builder, program);
        builder.Append('\n');

        builder.Append("dieX,dieY,site,hardBin,softBin,pass,firstFail");
        foreach (var item in program.Items)
        {
            builder.Append(',');
            AppendCsvField(builder, item.Name);
        }

        builder.Append('\n');

        foreach (var result in wafer.CpResults
                     .OrderBy(result => result.Die.Y)
                     .ThenBy(result => result.Die.X))
        {
            AppendInt(builder, result.Die.X);
            builder.Append(',');
            AppendInt(builder, result.Die.Y);
            builder.Append(',');
            AppendInt(builder, result.Site);
            builder.Append(',');
            AppendInt(builder, result.HardBin);
            builder.Append(',');
            AppendInt(builder, result.SoftBin);
            builder.Append(',');
            builder.Append(result.Passed ? "true" : "false");
            builder.Append(',');
            AppendCsvField(builder, result.FirstFailure?.Item.Name ?? string.Empty);
            foreach (var test in result.Tests)
            {
                builder.Append(',');
                AppendDouble(builder, test.Value);
            }

            builder.Append('\n');
        }

        WriteFile(path, builder);
    }

    public static void WriteFt(
        string path,
        Dataset dataset,
        Lot lot,
        Wafer wafer,
        IReadOnlyList<FtUnit> units)
    {
        var program = dataset.FtProgram;
        var cpPassedUnits = units.Count(unit => !unit.IsEscape);
        var escapeUnits = units.Count - cpPassedUnits;
        var assemblyLoss = wafer.NetDie - cpPassedUnits;
        var passedUnits = units.Count(unit => unit.Passed);

        var builder = new StringBuilder();
        WriteScalar(builder, "formatVersion", FormatInt(FormatVersion));
        WriteScalar(builder, "step", "FT");
        WriteScalar(builder, "product", dataset.ProductName);
        WriteScalar(builder, "lotId", lot.Id);
        WriteScalar(builder, "assemblyLot", lot.AssemblyLot);
        WriteScalar(builder, "waferId", wafer.Id);
        WriteScalar(builder, "seed", FormatLong(dataset.Seed));
        WriteScalar(builder, "catalogVersion", program.CatalogVersion);
        WriteScalar(builder, "siteCount", FormatInt(program.SiteCount));
        WriteScalar(builder, "cpPassedDie", FormatInt(wafer.NetDie));
        WriteScalar(builder, "assemblyLoss", FormatInt(assemblyLoss));
        WriteScalar(builder, "escapeUnits", FormatInt(escapeUnits));
        WriteScalar(builder, "ftUnits", FormatInt(units.Count));
        WriteScalar(builder, "passedUnits", FormatInt(passedUnits));
        WriteScalar(builder, "yield", FormatYield(passedUnits, units.Count));
        WriteScalar(builder, "testItemCount", FormatInt(program.Items.Count));

        WriteItemDefinitions(builder, program);
        WriteBinDefinitions(builder, program);
        builder.Append('\n');

        builder.Append("unitId,dieX,dieY,cpResult,site,hardBin,softBin,pass,firstFail");
        foreach (var item in program.Items)
        {
            builder.Append(',');
            AppendCsvField(builder, item.Name);
        }

        builder.Append('\n');

        foreach (var unit in units
                     .OrderBy(unit => unit.SourceDie.Y)
                     .ThenBy(unit => unit.SourceDie.X))
        {
            AppendCsvField(builder, unit.Id);
            builder.Append(',');
            AppendInt(builder, unit.SourceDie.X);
            builder.Append(',');
            AppendInt(builder, unit.SourceDie.Y);
            builder.Append(',');
            builder.Append(unit.CpPassed ? "Pass" : "Fail");
            builder.Append(',');
            AppendInt(builder, FtSite);
            builder.Append(',');
            AppendInt(builder, unit.HardBin);
            builder.Append(',');
            AppendInt(builder, unit.SoftBin);
            builder.Append(',');
            builder.Append(unit.Passed ? "true" : "false");
            builder.Append(',');
            AppendCsvField(builder, unit.FirstFailure?.Item.Name ?? string.Empty);
            foreach (var test in unit.Tests)
            {
                builder.Append(',');
                AppendDouble(builder, test.Value);
            }

            builder.Append('\n');
        }

        WriteFile(path, builder);
    }

    private static void WriteItemDefinitions(StringBuilder builder, TestProgram program)
    {
        builder.Append(
            "#testItem,name,testNumber,type,unit,lowSpec,highSpec,lowTest,highTest,failSoftBin\n");
        foreach (var item in program.Items)
        {
            builder.Append("#testItem,");
            AppendCsvField(builder, item.Name);
            builder.Append(',');
            AppendInt(builder, item.TestNumber);
            builder.Append(',');
            AppendCsvField(builder, ItemTypeName(item));
            builder.Append(',');
            AppendCsvField(builder, item.Unit);
            builder.Append(',');
            AppendDouble(builder, item.SpecLimits.Low);
            builder.Append(',');
            AppendDouble(builder, item.SpecLimits.High);
            builder.Append(',');
            AppendDouble(builder, item.TestLimits.Low);
            builder.Append(',');
            AppendDouble(builder, item.TestLimits.High);
            builder.Append(',');
            AppendInt(builder, item.FailSoftBin);
            builder.Append('\n');
        }
    }

    private static void WriteBinDefinitions(StringBuilder builder, TestProgram program)
    {
        var hardBins = new (int Number, string Name, bool Pass)[]
        {
            (program.Bins.PassHardBin, "Pass", true),
            (program.Bins.ParametricFailHardBin, "Parametric Fail", false),
            (program.Bins.FunctionalFailHardBin, "Functional Fail", false),
        };
        builder.Append("#hardBin,number,name,pass\n");
        foreach (var bin in hardBins.OrderBy(bin => bin.Number))
        {
            WriteBin(builder, "hardBin", bin.Number, bin.Name, bin.Pass);
        }

        var softBins = new List<(int Number, string Name, bool Pass)>
        {
            (program.Bins.PassSoftBin, "Pass", true),
        };
        softBins.AddRange(program.Items.Select(item => (item.FailSoftBin, item.Name, false)));
        builder.Append("#softBin,number,name,pass\n");
        foreach (var bin in softBins.OrderBy(bin => bin.Number))
        {
            WriteBin(builder, "softBin", bin.Number, bin.Name, bin.Pass);
        }
    }

    private static void WriteBin(
        StringBuilder builder, string table, int number, string name, bool pass)
    {
        builder.Append('#').Append(table).Append(',');
        AppendInt(builder, number);
        builder.Append(',');
        AppendCsvField(builder, name);
        builder.Append(',');
        builder.Append(pass ? "true" : "false");
        builder.Append('\n');
    }

    private static void WriteScalar(StringBuilder builder, string key, string value)
    {
        builder.Append('#').Append(key).Append('=');
        foreach (var character in value)
        {
            builder.Append(character is '\r' or '\n' ? ' ' : character);
        }

        builder.Append('\n');
    }

    private static string ItemTypeName(TestItem item) =>
        item is FunctionalTestItem ? "functional" : "numeric";

    private static string FormatInt(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string FormatLong(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string FormatYield(int passed, int total) =>
        (total == 0 ? 0 : Math.Round((double)passed / total, 6))
        .ToString("0.######", CultureInfo.InvariantCulture);

    private static void AppendInt(StringBuilder builder, int value) =>
        builder.Append(value.ToString(CultureInfo.InvariantCulture));

    private static void AppendDouble(StringBuilder builder, double value) =>
        builder.Append(value.ToString("0.######", CultureInfo.InvariantCulture));

    private static void AppendCsvField(StringBuilder builder, string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            builder.Append(value);
            return;
        }

        builder.Append('"').Append(value.Replace("\"", "\"\"")).Append('"');
    }

    private static void WriteFile(string path, StringBuilder builder) =>
        File.WriteAllText(
            path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}
