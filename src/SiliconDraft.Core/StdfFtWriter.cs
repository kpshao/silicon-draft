using System.Globalization;

namespace SiliconDraft.Core;

/// <summary>
/// Writes the minimal STDF V4 FT file: FAR, one ATR per FT Unit, MIR, one PIR/PTR/PRR
/// group per FT Unit, and MRR. The ATRs sit where V4 expects audit records and carry
/// the CP-FT Traceability fields, keyed by UNIT_ID, so a YMS can join FT records back
/// to their source Wafer and Die.
/// </summary>
internal static class StdfFtWriter
{
    public const string FileSuffix = "_FT.stdf";

    public static string FileName(string lotId) => $"{lotId}{FileSuffix}";

    public static void Write(string path, Dataset dataset, Lot lot)
    {
        using var stream = new MemoryStream();
        StdfFormat.WriteFile(
            stream,
            lot.Id,
            dataset.ProductName,
            "FT",
            parts =>
            {
                foreach (var unit in lot.FtUnits)
                {
                    StdfFormat.WritePir(parts);
                    foreach (var test in unit.Tests)
                    {
                        StdfFormat.WritePtr(parts, test);
                    }

                    StdfFormat.WritePrr(
                        parts,
                        unit.SourceDie.X,
                        unit.SourceDie.Y,
                        unit.Passed,
                        unit.Tests.Count,
                        unit.HardBin,
                        unit.SoftBin,
                        unit.Id);
                }
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
