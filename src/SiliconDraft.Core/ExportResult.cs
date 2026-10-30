namespace SiliconDraft.Core;

public sealed record ExportResult(
    bool Success,
    string? ManifestPath,
    IReadOnlyList<string> WaferMapPaths,
    IReadOnlyList<string> StdfPaths,
    IReadOnlyList<string> WatPaths,
    string? Error)
{
    public static ExportResult Exported(
        string manifestPath,
        IReadOnlyList<string> waferMapPaths,
        IReadOnlyList<string> stdfPaths,
        IReadOnlyList<string> watPaths) =>
        new(true, manifestPath, waferMapPaths, stdfPaths, watPaths, null);

    public static ExportResult Failed(string error) =>
        new(false, null, [], [], [], error);
}
