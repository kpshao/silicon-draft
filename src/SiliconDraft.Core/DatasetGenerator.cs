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
        var testProgram = CpCatalog.Resolve(config.Cp);
        var lots = config.Lots
            .Select(lot => new Lot(
                lot.Id,
                Enumerable.Range(1, lot.WaferCount)
                    .Select(index =>
                    {
                        var waferId = $"{lot.Id}-W{index:00}";
                        return new Wafer(
                            waferId,
                            dieMap,
                            CpGenerator.Generate(
                                testProgram, config.Seed, lot.Id, waferId, dieMap));
                    })
                    .ToArray()))
            .ToArray();

        return new Dataset(config, testProgram, lots);
    }
}
