namespace SiliconDraft.Core;

internal static class DatasetGenerator
{
    public static Dataset Generate(ResolvedConfig config)
    {
        var lots = config.Lots
            .Select(lot => new Lot(
                lot.Id,
                Enumerable.Range(1, lot.WaferCount)
                    .Select(index => new Wafer($"{lot.Id}-W{index:00}"))
                    .ToArray()))
            .ToArray();

        return new Dataset(config, lots);
    }
}
