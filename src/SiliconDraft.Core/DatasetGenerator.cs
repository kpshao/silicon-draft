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
        var ftProgram = FtCatalog.CreateProgram(testProgram.Bins);
        var watProgram = WatCatalog.CreateProgram();
        var wafersByLot = config.Lots
            .Select(lot => (
                LotId: lot.Id,
                Wafers: (IReadOnlyList<Wafer>)Enumerable.Range(1, lot.WaferCount)
                    .Select(index =>
                    {
                        var waferId = $"{lot.Id}-W{index:00}";
                        return new Wafer(
                            waferId,
                            dieMap,
                            CpGenerator.Generate(
                                testProgram, config.Seed, lot.Id, waferId, dieMap),
                            WatGenerator.Generate(
                                watProgram, config.Wat.Sites, config.Seed, lot.Id, waferId));
                    })
                    .ToArray()))
            .ToArray();
        var assemblyLoss = FtGenerator.SelectAssemblyLoss(
            config.Seed, config.Ft.AssemblyLoss, wafersByLot);
        var lots = wafersByLot
            .Select(entry => new Lot(
                entry.LotId,
                FtGenerator.AssemblyLotId(entry.LotId),
                entry.Wafers,
                FtGenerator.Generate(
                    ftProgram, config.Ft, config.Seed, entry.LotId, entry.Wafers, assemblyLoss)))
            .ToArray();

        return new Dataset(config, testProgram, ftProgram, watProgram, lots);
    }
}
