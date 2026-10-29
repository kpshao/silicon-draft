using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SiliconDraft.Core;

internal static class ManifestWriter
{
    public const int ManifestSchemaVersion = 2;
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

        if (!overwrite)
        {
            var existing = new[] { manifestPath }
                .Concat(waferMapPaths)
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

        return ExportResult.Exported(manifestPath, waferMapPaths);
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
        IReadOnlyList<LotManifest> Lots)
    {
        public static DatasetManifest Create(Dataset dataset)
        {
            var lots = dataset.Lots.Select(LotManifest.Create).ToArray();
            return new DatasetManifest(
                dataset.ProductName,
                dataset.LotCount,
                dataset.WaferCount,
                lots.Sum(lot => lot.GrossDie),
                lots.Sum(lot => lot.NetDie),
                lots.Sum(lot => lot.PartialDie),
                lots.Sum(lot => lot.PartialDieRetained),
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
        IReadOnlyList<WaferManifest> Wafers)
    {
        public static LotManifest Create(Lot lot)
        {
            var wafers = lot.Wafers
                .Select(wafer => new WaferManifest(
                    wafer.Id,
                    wafer.DieMap.GrossDie,
                    wafer.DieMap.NetDie,
                    wafer.DieMap.PartialDieFound,
                    wafer.DieMap.PartialDieRetained))
                .ToArray();
            return new LotManifest(
                lot.Id,
                wafers.Length,
                wafers.Sum(wafer => wafer.GrossDie),
                wafers.Sum(wafer => wafer.NetDie),
                wafers.Sum(wafer => wafer.PartialDie),
                wafers.Sum(wafer => wafer.PartialDieRetained),
                wafers);
        }
    }

    private sealed record WaferManifest(
        string WaferId,
        int GrossDie,
        int NetDie,
        int PartialDie,
        int PartialDieRetained);
}
