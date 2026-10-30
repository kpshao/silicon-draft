using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SiliconDraft.Core;

/// <summary>Writes a Dataset's ground-truth manifest and per-lot Wafer Map CSVs.</summary>
internal static class DatasetWriter
{
    public const int ManifestSchemaVersion = 4;
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
        var stdfPaths = dataset.Lots
            .Select(lot => Path.Combine(outputDirectory, StdfCpWriter.FileName(lot.Id)))
            .ToArray();
        var watFiles = WatTxtWriter.Plan(dataset, outputDirectory);
        var watPaths = watFiles.Select(file => file.Path).ToArray();

        if (!overwrite)
        {
            var existing = new[] { manifestPath }
                .Concat(waferMapPaths)
                .Concat(stdfPaths)
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
            StdfCpWriter.Write(stdfPaths[index], dataset, dataset.Lots[index]);
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

        return ExportResult.Exported(manifestPath, waferMapPaths, stdfPaths, watPaths);
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
        WatManifest Wat,
        IReadOnlyList<LotManifest> Lots)
    {
        public static DatasetManifest Create(Dataset dataset)
        {
            var lots = dataset.Lots
                .Select(lot => LotManifest.Create(lot, dataset.TestProgram))
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
                CpManifest.Create(dataset.Cp, dataset.TestProgram.CatalogVersion),
                WatManifest.Create(dataset),
                lots);
        }
    }

    private sealed record LotManifest(
        string LotId,
        int WaferCount,
        int GrossDie,
        int NetDie,
        int PartialDie,
        int PartialDieRetained,
        CpManifest Cp,
        int WatMeasurements,
        IReadOnlyList<WaferManifest> Wafers)
    {
        public static LotManifest Create(Lot lot, TestProgram program)
        {
            var totals = DieCounts.Sum(lot.Wafers.Select(wafer => wafer.DieMap.Counts));
            var wafers = lot.Wafers.Select(WaferManifest.Create).ToArray();
            var cp = lot.GetCpSummary(program);
            return new LotManifest(
                lot.Id,
                wafers.Length,
                totals.GrossDie,
                cp.PassedDie,
                totals.PartialDieFound,
                totals.PartialDieRetained,
                CpManifest.Create(cp, program.CatalogVersion),
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
        int WatMeasurements)
    {
        public static WaferManifest Create(Wafer wafer)
        {
            var counts = wafer.DieMap.Counts;
            return new WaferManifest(
                wafer.Id,
                counts.GrossDie,
                wafer.NetDie,
                counts.PartialDieFound,
                counts.PartialDieRetained,
                wafer.WatMeasurementCount);
        }
    }

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
                summary.HardBins
                    .Select(bin => new BinManifest(bin.Number, bin.Name, bin.Pass, bin.Count))
                    .ToArray(),
                summary.SoftBins
                    .Select(bin => new BinManifest(bin.Number, bin.Name, bin.Pass, bin.Count))
                    .ToArray());
    }

    private sealed record BinManifest(int Number, string Name, bool Pass, int Count);

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
