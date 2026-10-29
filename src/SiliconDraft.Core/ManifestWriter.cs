using System.Text;
using System.Text.Json;

namespace SiliconDraft.Core;

internal static class ManifestWriter
{
    public const int ManifestSchemaVersion = 1;
    public const string TruthDirectoryName = "_truth";
    public const string ManifestFileName = "manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static ExportResult Write(Dataset dataset, string outputDirectory, bool overwrite)
    {
        var manifest = new ManifestDocument(
            ManifestSchemaVersion,
            ConfigLoader.ConfigSchemaVersion,
            dataset.Seed,
            dataset.Config,
            new DatasetManifest(
                dataset.ProductName,
                dataset.LotCount,
                dataset.WaferCount,
                dataset.Lots
                    .Select(lot => new LotManifest(
                        lot.Id,
                        lot.Wafers.Count,
                        lot.Wafers.Select(wafer => wafer.Id).ToArray()))
                    .ToArray()));

        var manifestPath = Path.Combine(outputDirectory, TruthDirectoryName, ManifestFileName);
        if (!overwrite && File.Exists(manifestPath))
        {
            return ExportResult.Failed(
                $"'{manifestPath}' already exists; overwrite confirmation is required");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);

        var json = JsonSerializer.Serialize(manifest, JsonOptions).ReplaceLineEndings("\n") + "\n";
        File.WriteAllText(manifestPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return ExportResult.Exported(manifestPath);
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
        IReadOnlyList<LotManifest> Lots);

    private sealed record LotManifest(
        string LotId,
        int WaferCount,
        IReadOnlyList<string> WaferIds);
}
