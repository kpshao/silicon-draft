namespace SiliconDraft.Core;

internal static class DatasetGenerator
{
    public static Dataset Generate(ResolvedConfig config)
    {
        var dieMapResult = DieMapGenerator.TryGenerate(config.Wafer);
        if (dieMapResult.Map is null)
        {
            throw new InvalidOperationException(dieMapResult.Error);
        }

        var dieMap = dieMapResult.Map;
        var lots = config.Lots
            .Select(lot => new Lot(
                lot.Id,
                Enumerable.Range(1, lot.WaferCount)
                    .Select(index => new Wafer($"{lot.Id}-W{index:00}", dieMap))
                    .ToArray()))
            .ToArray();

        return new Dataset(config, lots);
    }
}
