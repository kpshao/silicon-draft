namespace SiliconDraft.Core;

/// <summary>
/// Writes the minimal STDF V4 CP file: FAR, MIR, one PIR/PTR/PRR group per tested Die,
/// and MRR. Functional Test Items are exported as PTRs with a 1/0 result, P/F units,
/// and limits so every item carries units and limits; FTR and multi-site records arrive
/// in T6.
/// </summary>
internal static class StdfCpWriter
{
    public const string FileSuffix = "_CP.stdf";

    public static string FileName(string lotId) => $"{lotId}{FileSuffix}";

    public static void Write(string path, Dataset dataset, Lot lot)
    {
        using var stream = new MemoryStream();
        StdfFormat.WriteFile(stream, lot.Id, dataset.ProductName, "CP", parts =>
        {
            foreach (var wafer in lot.Wafers)
            {
                foreach (var result in wafer.CpResults)
                {
                    StdfFormat.WritePir(parts);
                    foreach (var test in result.Tests)
                    {
                        StdfFormat.WritePtr(parts, test);
                    }

                    StdfFormat.WritePrr(
                        parts,
                        result.Die.X,
                        result.Die.Y,
                        result.Passed,
                        result.Tests.Count,
                        result.HardBin,
                        result.SoftBin,
                        DieIdentity.UnitId(wafer.Id, result.Die));
                }
            }
        });
        File.WriteAllBytes(path, stream.ToArray());
    }
}
