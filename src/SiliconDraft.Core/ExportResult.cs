namespace SiliconDraft.Core;

public sealed record ExportResult(
    bool Success,
    string? ManifestPath,
    IReadOnlyList<string> WaferMapPaths,
    IReadOnlyList<string> CpStdfPaths,
    IReadOnlyList<string> FtPaths,
    IReadOnlyList<string> WatPaths,
    IReadOnlyList<string> CpCsvPaths,
    IReadOnlyList<string> FtCsvPaths,
    string? Error)
{
    public static ExportResult Exported(
        string manifestPath,
        IReadOnlyList<string> waferMapPaths,
        IReadOnlyList<string> cpStdfPaths,
        IReadOnlyList<string> ftPaths,
        IReadOnlyList<string> watPaths,
        IReadOnlyList<string> cpCsvPaths,
        IReadOnlyList<string> ftCsvPaths) =>
        new(
            true,
            manifestPath,
            waferMapPaths,
            cpStdfPaths,
            ftPaths,
            watPaths,
            cpCsvPaths,
            ftCsvPaths,
            null);

    public static ExportResult Failed(string error) =>
        new(false, null, [], [], [], [], [], [], error);
}
