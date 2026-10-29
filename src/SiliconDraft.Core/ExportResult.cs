namespace SiliconDraft.Core;

public sealed record ExportResult(
    bool Success,
    string? ManifestPath,
    IReadOnlyList<string> WaferMapPaths,
    IReadOnlyList<string> StdfPaths,
    string? Error)
{
    public static ExportResult Exported(
        string manifestPath,
        IReadOnlyList<string> waferMapPaths,
        IReadOnlyList<string> stdfPaths) =>
        new(true, manifestPath, waferMapPaths, stdfPaths, null);

    public static ExportResult Failed(string error) =>
        new(false, null, [], [], error);
}
