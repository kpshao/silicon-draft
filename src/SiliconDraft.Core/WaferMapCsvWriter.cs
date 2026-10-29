using System.Globalization;
using System.Text;

namespace SiliconDraft.Core;

internal static class WaferMapCsvWriter
{
    public const string FileSuffix = "_wafermap.csv";

    public static string FileName(string lotId) => $"{lotId}{FileSuffix}";

    public static void Write(string path, Lot lot)
    {
        var builder = new StringBuilder();
        builder.Append("lotId,waferId,dieX,dieY,centerXmm,centerYmm,status,tested\n");
        foreach (var wafer in lot.Wafers)
        {
            foreach (var die in wafer.DieMap.Dies)
            {
                builder
                    .Append(lot.Id).Append(',')
                    .Append(wafer.Id).Append(',')
                    .Append(die.X.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(die.Y.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(Format(die.CenterXmm)).Append(',')
                    .Append(Format(die.CenterYmm)).Append(',')
                    .Append(die.Disposition == DieDisposition.Valid ? "valid" : "partial").Append(',')
                    .Append(die.Tested ? "true" : "false").Append('\n');
            }
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string Format(double value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);
}
