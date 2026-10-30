using System.Globalization;
using System.Text;

namespace SiliconDraft.Core;

/// <summary>
/// Plans and writes WAT measurements as tab-separated TXT. The long template repeats
/// site, item, unit, and limits on every row; the wide template writes one row per site
/// per wafer and puts the item values in columns, with units and limits in a leading
/// definition block.
/// </summary>
internal static class WatTxtWriter
{
    /// <summary>Resolves the WAT files for a Dataset and the wafers each contains.</summary>
    public static IReadOnlyList<WatFilePlan> Plan(Dataset dataset, string outputDirectory)
    {
        var template = dataset.Config.Wat.Template;
        return dataset.Config.Wat.Organization switch
        {
            WatFileOrganization.PerLot => dataset.Lots
                .Select(lot => new WatFilePlan(
                    Path.Combine(outputDirectory, FileName(lot.Id, template)),
                    lot.Wafers.Select(wafer => new WatFileWafer(lot.Id, wafer)).ToArray()))
                .ToArray(),
            WatFileOrganization.PerWafer => dataset.Lots
                .SelectMany(lot => lot.Wafers.Select(wafer => new WatFilePlan(
                    Path.Combine(outputDirectory, FileName(wafer.Id, template)),
                    [new WatFileWafer(lot.Id, wafer)])))
                .ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(dataset.Config.Wat.Organization)),
        };
    }

    public static void Write(Dataset dataset, WatFilePlan plan)
    {
        var builder = new StringBuilder();
        builder.Append("# catalogVersion=").Append(dataset.WatProgram.CatalogVersion).Append('\n');
        switch (dataset.Config.Wat.Template)
        {
            case WatTemplate.Long:
                WriteLong(builder, plan.Wafers);
                break;
            case WatTemplate.Wide:
                WriteWide(builder, dataset.WatProgram, plan.Wafers);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(dataset.Config.Wat.Template));
        }

        File.WriteAllText(
            plan.Path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void WriteLong(StringBuilder builder, IReadOnlyList<WatFileWafer> wafers)
    {
        builder.Append(
            "lotId\twaferId\tsite\tsiteXmm\tsiteYmm\ttestItem\ttestNumber\tvalue\tunit" +
            "\tlowSpec\thighSpec\tlowTest\thighTest\n");
        foreach (var source in wafers)
        {
            foreach (var measurement in source.Wafer.WatResults)
            {
                var item = measurement.Item;
                AppendSite(builder, source.LotId, source.Wafer.Id, measurement.Site);
                builder
                    .Append(item.Name).Append('\t')
                    .Append(item.TestNumber.ToString(CultureInfo.InvariantCulture)).Append('\t')
                    .Append(Format(measurement.Value)).Append('\t')
                    .Append(item.Unit).Append('\t');
                AppendLimits(builder, item);
            }
        }
    }

    private static void WriteWide(
        StringBuilder builder,
        WatProgram program,
        IReadOnlyList<WatFileWafer> wafers)
    {
        builder.Append("# testItem\ttestNumber\tunit\tlowSpec\thighSpec\tlowTest\thighTest\n");
        foreach (var item in program.Items)
        {
            builder
                .Append("# ").Append(item.Name).Append('\t')
                .Append(item.TestNumber.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(item.Unit).Append('\t');
            AppendLimits(builder, item);
        }

        builder.Append("lotId\twaferId\tsite\tsiteXmm\tsiteYmm");
        foreach (var item in program.Items)
        {
            builder.Append('\t').Append(item.Name);
        }

        builder.Append('\n');

        foreach (var source in wafers)
        {
            foreach (var siteGroup in source.Wafer.WatResults.GroupBy(
                measurement => measurement.Site))
            {
                var values = siteGroup.ToDictionary(
                    measurement => measurement.Item.Name, measurement => measurement.Value);
                AppendSite(builder, source.LotId, source.Wafer.Id, siteGroup.Key);
                for (var index = 0; index < program.Items.Count; index++)
                {
                    if (index > 0)
                    {
                        builder.Append('\t');
                    }

                    builder.Append(Format(values[program.Items[index].Name]));
                }

                builder.Append('\n');
            }
        }
    }

    private static void AppendSite(
        StringBuilder builder, string lotId, string waferId, PcmSite site)
    {
        builder
            .Append(lotId).Append('\t')
            .Append(waferId).Append('\t')
            .Append(site.Id).Append('\t')
            .Append(Format(site.XMm)).Append('\t')
            .Append(Format(site.YMm)).Append('\t');
    }

    private static void AppendLimits(StringBuilder builder, WatTestItem item)
    {
        builder
            .Append(Format(item.SpecLimits.Low)).Append('\t')
            .Append(Format(item.SpecLimits.High)).Append('\t')
            .Append(Format(item.TestLimits.Low)).Append('\t')
            .Append(Format(item.TestLimits.High)).Append('\n');
    }

    private static string FileName(string stem, WatTemplate template) =>
        $"{stem}_WAT_{TemplateName(template)}.txt";

    private static string TemplateName(WatTemplate template) => template switch
    {
        WatTemplate.Long => "long",
        WatTemplate.Wide => "wide",
        _ => throw new ArgumentOutOfRangeException(nameof(template)),
    };

    private static string Format(double value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);
}

/// <summary>One WAT file to write and the wafers whose measurements it contains.</summary>
internal sealed record WatFilePlan(string Path, IReadOnlyList<WatFileWafer> Wafers);

internal sealed record WatFileWafer(string LotId, Wafer Wafer);
