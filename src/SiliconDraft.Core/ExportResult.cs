namespace SiliconDraft.Core;

public sealed record ExportResult(
    bool Success,
    string? ManifestPath,
    IReadOnlyList<string> WaferMapPaths,
    string? Error)
{
    public static ExportResult Exported(string manifestPath, IReadOnlyList<string> waferMapPaths) =>
        new(true, manifestPath, waferMapPaths, null);

    public static ExportResult Failed(string error) =>
        new(false, null, [], error);
}
