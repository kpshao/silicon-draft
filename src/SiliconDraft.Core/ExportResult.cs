namespace SiliconDraft.Core;

public sealed record ExportResult(
    bool Success,
    string? ManifestPath,
    IReadOnlyList<string> WaferMapPaths,
    IReadOnlyList<string> CpStdfPaths,
    IReadOnlyList<string> FtPaths,
    IReadOnlyList<string> WatPaths,
    string? Error)
{
    public static ExportResult Exported(
        string manifestPath,
        IReadOnlyList<string> waferMapPaths,
        IReadOnlyList<string> cpStdfPaths,
        IReadOnlyList<string> ftPaths,
        IReadOnlyList<string> watPaths) =>
        new(true, manifestPath, waferMapPaths, cpStdfPaths, ftPaths, watPaths, null);

    public static ExportResult Failed(string error) =>
        new(false, null, [], [], [], [], error);
}
