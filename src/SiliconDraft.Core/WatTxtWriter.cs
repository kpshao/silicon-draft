using System.Globalization;
using System.Text;

namespace SiliconDraft.Core;

/// <summary>
/// Writes WAT measurements as tab-separated TXT. The long template repeats site, item,
/// unit, and limits on every row; the wide template measures one row per site and puts
/// the item values in columns, with units and limits in a leading definition block.
/// </summary>
internal static class WatTxtWriter
{
    public static string FileName(string stem, WatTemplate template) =>
        $"{stem}_WAT_{TemplateName(template)}.txt";

    public static void WriteLot(string path, Dataset dataset, Lot lot, WatTemplate template) =>
        Write(path, dataset, lot.Wafers.Select(wafer => (lot.Id, wafer)), template);

    public static void WriteWafer(
        string path, Dataset dataset, string lotId, Wafer wafer, WatTemplate template) =>
        Write(path, dataset, [(lotId, wafer)], template);

    private static void Write(
        string path,
        Dataset dataset,
        IEnumerable<(string LotId, Wafer Wafer)> wafers,
        WatTemplate template)
    {
        var builder = new StringBuilder();
        builder.Append("# catalogVersion=").Append(dataset.WatProgram.CatalogVersion).Append('\n');
        switch (template)
        {
            case WatTemplate.Long:
                WriteLong(builder, wafers);
                break;
            case WatTemplate.Wide:
                WriteWide(builder, dataset.WatProgram, wafers);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(template));
        }

        File.WriteAllText(
            path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void WriteLong(
        StringBuilder builder, IEnumerable<(string LotId, Wafer Wafer)> wafers)
    {
        builder.Append(
            "lotId\twaferId\tsite\tsiteXmm\tsiteYmm\ttestItem\ttestNumber\tvalue\tunit" +
            "\tlowSpec\thighSpec\tlowTest\thighTest\n");
        foreach (var (lotId, wafer) in wafers)
        {
            foreach (var measurement in wafer.WatResults)
            {
                var item = measurement.Item;
                builder
                    .Append(lotId).Append('\t')
                    .Append(wafer.Id).Append('\t')
                    .Append(measurement.Site.Id).Append('\t')
                    .Append(Format(measurement.Site.XMm)).Append('\t')
                    .Append(Format(measurement.Site.YMm)).Append('\t')
                    .Append(item.Name).Append('\t')
                    .Append(item.TestNumber.ToString(CultureInfo.InvariantCulture)).Append('\t')
                    .Append(Format(measurement.Value)).Append('\t')
                    .Append(item.Unit).Append('\t')
                    .Append(Format(item.SpecLimits.Low)).Append('\t')
                    .Append(Format(item.SpecLimits.High)).Append('\t')
                    .Append(Format(item.TestLimits.Low)).Append('\t')
                    .Append(Format(item.TestLimits.High)).Append('\n');
            }
        }
    }

    private static void WriteWide(
        StringBuilder builder,
        WatProgram program,
        IEnumerable<(string LotId, Wafer Wafer)> wafers)
    {
        builder.Append("# testItem\ttestNumber\tunit\tlowSpec\thighSpec\tlowTest\thighTest\n");
        foreach (var item in program.Items)
        {
            builder
                .Append("# ").Append(item.Name).Append('\t')
                .Append(item.TestNumber.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(item.Unit).Append('\t')
                .Append(Format(item.SpecLimits.Low)).Append('\t')
                .Append(Format(item.SpecLimits.High)).Append('\t')
                .Append(Format(item.TestLimits.Low)).Append('\t')
                .Append(Format(item.TestLimits.High)).Append('\n');
        }

        builder.Append("lotId\twaferId\tsite\tsiteXmm\tsiteYmm");
        foreach (var item in program.Items)
        {
            builder.Append('\t').Append(item.Name);
        }

        builder.Append('\n');

        foreach (var (lotId, wafer) in wafers)
        {
            foreach (var siteGroup in wafer.WatResults.GroupBy(measurement => measurement.Site))
            {
                var values = siteGroup.ToDictionary(
                    measurement => measurement.Item.Name, measurement => measurement.Value);
                var site = siteGroup.Key;
                builder
                    .Append(lotId).Append('\t')
                    .Append(wafer.Id).Append('\t')
                    .Append(site.Id).Append('\t')
                    .Append(Format(site.XMm)).Append('\t')
                    .Append(Format(site.YMm));
                foreach (var item in program.Items)
                {
                    builder.Append('\t').Append(Format(values[item.Name]));
                }

                builder.Append('\n');
            }
        }
    }

    private static string TemplateName(WatTemplate template) => template switch
    {
        WatTemplate.Long => "long",
        WatTemplate.Wide => "wide",
        _ => throw new ArgumentOutOfRangeException(nameof(template)),
    };

    private static string Format(double value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);
}
