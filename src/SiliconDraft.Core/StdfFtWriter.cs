using System.Globalization;

namespace SiliconDraft.Core;

/// <summary>
/// Writes one Lot's STDF V4 FT file. FT is package test, not wafer sort, so it carries
/// no WIR/WCR/WRR; source Wafer traceability lives in the per-Unit ATRs instead. The
/// ATRs sit where V4 expects audit records and are keyed by UNIT_ID, so a YMS can join
/// FT records back to their source Wafer and Die.
/// </summary>
internal static class StdfFtWriter
{
    public const string FileSuffix = "_FT.stdf";

    private const int FtSite = 1;

    public static string FileName(string lotId) => $"{lotId}{FileSuffix}";

    public static void Write(string path, Dataset dataset, Lot lot)
    {
        var program = dataset.FtProgram;
        using var stream = new MemoryStream();
        StdfFormat.WriteFile(
            stream,
            lot.Id,
            dataset.ProductName,
            "FT",
            body =>
            {
                StdfFormat.WriteSdr(body, program.SiteCount);
                var parts = new List<StdfPartOutcome>(lot.FtUnits.Count);
                foreach (var unit in lot.FtUnits)
                {
                    StdfFormat.WritePir(body, FtSite);
                    foreach (var test in unit.Tests)
                    {
                        StdfFormat.WriteTestRecord(body, test, FtSite);
                    }

                    StdfFormat.WritePrr(
                        body,
                        FtSite,
                        unit.SourceDie.X,
                        unit.SourceDie.Y,
                        unit.Passed,
                        unit.Tests.Count,
                        unit.HardBin,
                        unit.SoftBin,
                        unit.Id);
                    parts.Add(new StdfPartOutcome(
                        FtSite, unit.Tests, unit.HardBin, unit.SoftBin));
                }

                StdfSummaryWriter.Write(body, program, program.SiteCount, parts);
            },
            audit =>
            {
                foreach (var unit in lot.FtUnits)
                {
                    StdfFormat.WriteAtr(audit, AuditCommand(lot, unit));
                }
            });
        File.WriteAllBytes(path, stream.ToArray());
    }

    /// <summary>
    /// The ATR CMD_LINE for one FT Unit. STDF V4 ATR has a free-text command line, so
    /// traceability is encoded as semicolon-separated <c>KEY=VALUE</c> pairs.
    /// </summary>
    internal static string AuditCommand(Lot lot, FtUnit unit) =>
        string.Concat(
            "UNIT_ID=", unit.Id, ";",
            "SOURCE_WAFER_ID=", unit.SourceWaferId, ";",
            "SOURCE_DIE_X=", unit.SourceDie.X.ToString(CultureInfo.InvariantCulture), ";",
            "SOURCE_DIE_Y=", unit.SourceDie.Y.ToString(CultureInfo.InvariantCulture), ";",
            "ASSEMBLY_LOT=", lot.AssemblyLot, ";",
            "CP_RESULT=", unit.CpPassed ? "Pass" : "Fail");
}
