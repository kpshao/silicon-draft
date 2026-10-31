using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SiliconDraft.Core;

/// <summary>Writes a Dataset's ground-truth manifest and its per-lot exports.</summary>
internal static class DatasetWriter
{
    public const int ManifestSchemaVersion = 8;
    public const string TruthDirectoryName = "_truth";
    public const string ManifestFileName = "manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static ExportResult Write(Dataset dataset, string outputDirectory, bool overwrite)
    {
        var manifestPath = Path.Combine(outputDirectory, TruthDirectoryName, ManifestFileName);
        var waferMapPaths = dataset.Lots
            .Select(lot => Path.Combine(outputDirectory, WaferMapCsvWriter.FileName(lot.Id)))
            .ToArray();
        var cpStdfPaths = dataset.Lots
            .Select(lot => Path.Combine(outputDirectory, StdfCpWriter.FileName(lot.Id)))
            .ToArray();
        var ftPaths = dataset.Lots
            .Select(lot => Path.Combine(outputDirectory, StdfFtWriter.FileName(lot.Id)))
            .ToArray();
        var watFiles = WatTxtWriter.Plan(dataset, outputDirectory);
        var watPaths = watFiles.Select(file => file.Path).ToArray();

        if (!overwrite)
        {
            var existing = new[] { manifestPath }
                .Concat(waferMapPaths)
                .Concat(cpStdfPaths)
                .Concat(ftPaths)
                .Concat(watPaths)
                .FirstOrDefault(File.Exists);
            if (existing is not null)
            {
                return ExportResult.Failed(
                    $"'{existing}' already exists; overwrite confirmation is required");
            }
        }

        Directory.CreateDirectory(outputDirectory);
        for (var index = 0; index < dataset.Lots.Count; index++)
        {
            WaferMapCsvWriter.Write(waferMapPaths[index], dataset.Lots[index]);
            StdfCpWriter.Write(cpStdfPaths[index], dataset, dataset.Lots[index]);
            StdfFtWriter.Write(ftPaths[index], dataset, dataset.Lots[index]);
        }

        foreach (var watFile in watFiles)
        {
            WatTxtWriter.Write(dataset, watFile);
        }

        var manifest = new ManifestDocument(
            ManifestSchemaVersion,
            ConfigLoader.ConfigSchemaVersion,
            dataset.Seed,
            dataset.Config,
            DatasetManifest.Create(dataset));

        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        var json = JsonSerializer.Serialize(manifest, JsonOptions).ReplaceLineEndings("\n") + "\n";
        File.WriteAllText(manifestPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return ExportResult.Exported(manifestPath, waferMapPaths, cpStdfPaths, ftPaths, watPaths);
    }

    private sealed record ManifestDocument(
        int ManifestSchemaVersion,
        int ConfigSchemaVersion,
        long Seed,
        ResolvedConfig ResolvedConfig,
        DatasetManifest Dataset);

    private sealed record DatasetManifest(
        string Product,
        int LotCount,
        int WaferCount,
        int GrossDie,
        int NetDie,
        int PartialDie,
        int PartialDieRetained,
        CpManifest Cp,
        FtManifest Ft,
        WatManifest Wat,
        CorrelationsManifest Correlations,
        IReadOnlyList<LotManifest> Lots)
    {
        public static DatasetManifest Create(Dataset dataset)
        {
            var lots = dataset.Lots
                .Select(lot => LotManifest.Create(lot, dataset.CpProgram, dataset.FtProgram))
                .ToArray();
            var totals = dataset.DieTotals;
            return new DatasetManifest(
                dataset.ProductName,
                dataset.LotCount,
                dataset.WaferCount,
                totals.GrossDie,
                dataset.Cp.PassedDie,
                totals.PartialDieFound,
                totals.PartialDieRetained,
                CpManifest.Create(dataset.Cp, dataset.CpProgram.CatalogVersion),
                FtManifest.Create(dataset.Ft, dataset.FtProgram.CatalogVersion),
                WatManifest.Create(dataset),
                CorrelationsManifest.Create(dataset.DieCorrelations),
                lots);
        }
    }

    private sealed record CorrelationsManifest(IReadOnlyList<DieCorrelationManifest> Die)
    {
        public static CorrelationsManifest Create(IReadOnlyList<DieCorrelationResult> die) =>
            new(die
                .Select(result => new DieCorrelationManifest(
                    result.CpItem,
                    result.FtItem,
                    result.TargetR,
                    result.Tolerance,
                    result.AchievedR,
                    result.WithinTolerance,
                    result.SampleCount))
                .ToArray());
    }

    private sealed record DieCorrelationManifest(
        string CpItem,
        string FtItem,
        double TargetR,
        double Tolerance,
        double? AchievedR,
        bool? WithinTolerance,
        int SampleCount);

    private sealed record LotManifest(
        string LotId,
        string AssemblyLot,
        int WaferCount,
        int GrossDie,
        int NetDie,
        int PartialDie,
        int PartialDieRetained,
        CpManifest Cp,
        FtManifest Ft,
        int WatMeasurements,
        IReadOnlyList<WaferManifest> Wafers)
    {
        public static LotManifest Create(Lot lot, TestProgram cpProgram, TestProgram ftProgram)
        {
            var totals = DieCounts.Sum(lot.Wafers.Select(wafer => wafer.DieMap.Counts));
            var ftByWafer = lot.FtUnits
                .GroupBy(unit => unit.SourceWaferId)
                .ToDictionary(
                    group => group.Key,
                    group => new WaferFtCounts(
                        group.Count(), group.Count(unit => unit.IsEscape)));
            var wafers = lot.Wafers
                .Select(wafer => WaferManifest.Create(
                    wafer, ftByWafer.GetValueOrDefault(wafer.Id)))
                .ToArray();
            var cp = lot.GetCpSummary(cpProgram);
            var ft = lot.GetFtSummary(ftProgram);
            return new LotManifest(
                lot.Id,
                lot.AssemblyLot,
                wafers.Length,
                totals.GrossDie,
                cp.PassedDie,
                totals.PartialDieFound,
                totals.PartialDieRetained,
                CpManifest.Create(cp, cpProgram.CatalogVersion),
                FtManifest.Create(ft, ftProgram.CatalogVersion),
                lot.WatMeasurementCount,
                wafers);
        }
    }

    private sealed record WaferManifest(
        string WaferId,
        int GrossDie,
        int NetDie,
        int PartialDie,
        int PartialDieRetained,
        int WatMeasurements,
        int FtUnits,
        int FtEscapeUnits)
    {
        public static WaferManifest Create(Wafer wafer, WaferFtCounts ft)
        {
            var counts = wafer.DieMap.Counts;
            return new WaferManifest(
                wafer.Id,
                counts.GrossDie,
                wafer.NetDie,
                counts.PartialDieFound,
                counts.PartialDieRetained,
                wafer.WatMeasurementCount,
                ft.Units,
                ft.Escapes);
        }
    }

    private readonly record struct WaferFtCounts(int Units, int Escapes);

    private sealed record CpManifest(
        string CatalogVersion,
        int TestedDie,
        int PassedDie,
        double Yield,
        IReadOnlyList<BinManifest> HardBins,
        IReadOnlyList<BinManifest> SoftBins)
    {
        public static CpManifest Create(CpSummary summary, string catalogVersion) =>
            new(
                catalogVersion,
                summary.TestedDie,
                summary.PassedDie,
                summary.Yield,
                ToManifest(summary.HardBins),
                ToManifest(summary.SoftBins));
    }

    private sealed record FtManifest(
        string CatalogVersion,
        int UnitCount,
        int CpPassedUnits,
        int EscapeUnits,
        int AssemblyLoss,
        int PassedUnits,
        double Yield,
        IReadOnlyList<BinManifest> HardBins,
        IReadOnlyList<BinManifest> SoftBins)
    {
        public static FtManifest Create(FtSummary summary, string catalogVersion) =>
            new(
                catalogVersion,
                summary.UnitCount,
                summary.CpPassedUnits,
                summary.EscapeUnits,
                summary.AssemblyLoss,
                summary.PassedUnits,
                summary.Yield,
                ToManifest(summary.HardBins),
                ToManifest(summary.SoftBins));
    }

    private sealed record BinManifest(int Number, string Name, bool Pass, int Count);

    private static IReadOnlyList<BinManifest> ToManifest(IReadOnlyList<BinCount> bins) =>
        bins
            .Select(bin => new BinManifest(bin.Number, bin.Name, bin.Pass, bin.Count))
            .ToArray();

    private sealed record WatManifest(
        string CatalogVersion,
        int SiteCount,
        int TestItemCount,
        int MeasurementCount)
    {
        public static WatManifest Create(Dataset dataset) =>
            new(
                dataset.WatProgram.CatalogVersion,
                dataset.Config.Wat.Sites.Count,
                dataset.WatProgram.Items.Count,
                dataset.WatMeasurementCount);
    }
}
