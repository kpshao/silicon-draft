namespace SiliconDraft.Core;

/// <summary>
/// Writes one Lot's STDF V4 CP file. The record stream is FAR, MIR, SDR, then per Wafer
/// WIR/WCR, one PIR/test/PRR group per tested Die, and WRR; the lot summaries TSR, HBR,
/// SBR, and PCR follow, and MRR closes the file.
/// </summary>
internal static class StdfCpWriter
{
    public const string FileSuffix = "_CP.stdf";

    public static string FileName(string lotId) => $"{lotId}{FileSuffix}";

    public static void Write(string path, Dataset dataset, Lot lot)
    {
        var program = dataset.CpProgram;
        var siteCount = program.SiteCount;
        var (centerX, centerY) = dataset.DieMap.CenterDieIndex();
        var parts = new List<StdfPartOutcome>(lot.Wafers.Sum(wafer => wafer.CpResults.Count));
        using var stream = new MemoryStream();
        StdfFormat.WriteFile(stream, lot.Id, dataset.ProductName, "CP", body =>
        {
            StdfFormat.WriteSdr(body, siteCount);
            foreach (var wafer in lot.Wafers)
            {
                StdfFormat.WriteWir(body, wafer.Id);
                StdfFormat.WriteWcr(body, dataset.Config.Wafer, centerX, centerY);
                foreach (var result in wafer.CpResults)
                {
                    StdfFormat.WritePir(body, result.Site);
                    foreach (var test in result.Tests)
                    {
                        StdfFormat.WriteTestRecord(body, test, result.Site);
                    }

                    StdfFormat.WritePrr(
                        body,
                        result.Site,
                        result.Die.X,
                        result.Die.Y,
                        result.Passed,
                        result.Tests.Count,
                        result.HardBin,
                        result.SoftBin,
                        DieIdentity.UnitId(wafer.Id, result.Die));
                    parts.Add(new StdfPartOutcome(
                        result.Site, result.Tests, result.HardBin, result.SoftBin));
                }

                StdfFormat.WriteWrr(body, wafer.Id, wafer.CpResults.Count, wafer.NetDie);
            }

            StdfSummaryWriter.Write(body, program, siteCount, parts);
        });
        File.WriteAllBytes(path, stream.ToArray());
    }
}
