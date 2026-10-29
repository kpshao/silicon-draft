namespace SiliconDraft.Core;

public sealed record ExportResult(bool Success, string? ManifestPath, string? Error)
{
    public static ExportResult Exported(string manifestPath) => new(true, manifestPath, null);

    public static ExportResult Failed(string error) => new(false, null, error);
}
