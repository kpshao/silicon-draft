namespace SiliconDraft.Core;

internal static class DatasetGenerator
{
    /// <summary>
    /// FT Units are the CP-passing subset, so conditioning on CP changes the observed
    /// die-level correlation slightly. Regenerate with adjusted die-layer correlations
    /// until every configured pair lands inside its tolerance, or the attempt budget runs out.
    /// </summary>
    private const int MaxCalibrationPasses = 4;

    public static Dataset Generate(ResolvedConfig config)
    {
        var dieMapResult = DieMapGenerator.TryGenerate(config.Wafer);
        if (dieMapResult.Map is null)
        {
            throw new InvalidOperationException(dieMapResult.Error);
        }

        var dieMap = dieMapResult.Map;
        var testProgram = CpCatalog.Resolve(config.Cp);
        var ftProgram = FtCatalog.CreateProgram(config.Ft, testProgram.Bins);
        var watProgram = WatCatalog.CreateProgram(config.Wat);

        IReadOnlyList<double>? dieCorrelations = null;
        Dataset? dataset = null;
        for (var pass = 0; pass < MaxCalibrationPasses; pass++)
        {
            var latentModel = LatentModel.Create(
                config.Latent, testProgram, ftProgram, dieCorrelations);
            dieCorrelations = latentModel.DieCorrelations;
            dataset = GenerateOnce(
                config, dieMap, testProgram, ftProgram, watProgram, latentModel);

            var adjusted = AdjustDieCorrelations(
                config.Latent,
                dieCorrelations,
                latentModel.DieSensitivities,
                dataset.DieCorrelations);
            if (adjusted is null)
            {
                break;
            }

            dieCorrelations = adjusted;
        }

        return dataset!;
    }

    private static Dataset GenerateOnce(
        ResolvedConfig config,
        DieMap dieMap,
        TestProgram testProgram,
        TestProgram ftProgram,
        WatProgram watProgram,
        LatentModel latentModel)
    {
        var cpSampler = latentModel.CreateSampler(testProgram, TestStep.Cp, config.Seed);
        var ftSampler = latentModel.CreateSampler(ftProgram, TestStep.Ft, config.Seed);

        var contexts = new Dictionary<(string LotId, string WaferId), LatentWaferContext>();
        var wafersByLot = config.Lots
            .Select(lot =>
            {
                var lotFactor = new SplitMix64(
                    DeterministicSeed.ForLatentLot(config.Seed, lot.Id)).NextNormal();
                var wafers = (IReadOnlyList<Wafer>)Enumerable.Range(1, lot.WaferCount)
                    .Select(index =>
                    {
                        var waferId = $"{lot.Id}-W{index:00}";
                        var context = LatentWaferContext.Create(
                            config.Seed,
                            lot.Id,
                            waferId,
                            lotFactor,
                            dieMap,
                            config.Wafer.DiameterMm);
                        contexts[(lot.Id, waferId)] = context;
                        return new Wafer(
                            waferId,
                            dieMap,
                            CpGenerator.Generate(
                                cpSampler,
                                testProgram,
                                config.Seed,
                                lot.Id,
                                waferId,
                                dieMap,
                                context),
                            WatGenerator.Generate(
                                watProgram, config.Wat.Sites, config.Seed, lot.Id, waferId));
                    })
                    .ToArray();
                return (LotId: lot.Id, Wafers: wafers);
            })
            .ToArray();
        var assemblyLoss = FtGenerator.SelectAssemblyLoss(
            config.Seed, config.Ft.AssemblyLoss, wafersByLot);
        var lots = wafersByLot
            .Select(entry => new Lot(
                entry.LotId,
                FtGenerator.AssemblyLotId(entry.LotId),
                entry.Wafers,
                FtGenerator.Generate(
                    ftSampler,
                    config.Ft,
                    config.Seed,
                    entry.LotId,
                    entry.Wafers,
                    assemblyLoss,
                    contexts)))
            .ToArray();

        return new Dataset(config, testProgram, ftProgram, watProgram, lots);
    }

    /// <summary>
    /// Returns null when every pair already lands within its tolerance. Otherwise returns
    /// adjusted die-layer correlations; each passes the same per-item budget the validator
    /// enforces.
    /// </summary>
    private static IReadOnlyList<double>? AdjustDieCorrelations(
        LatentConfig latent,
        IReadOnlyList<double> current,
        IReadOnlyList<double> sensitivities,
        IReadOnlyList<DieCorrelationResult> achieved)
    {
        if (latent.DiePairs.Count == 0)
        {
            return null;
        }

        var adjusted = current.ToArray();
        var changed = false;
        for (var index = 0; index < latent.DiePairs.Count; index++)
        {
            var pair = latent.DiePairs[index];
            if (sensitivities[index] <= 0 ||
                achieved[index].AchievedR is not double observed)
            {
                continue;
            }

            var error = pair.TargetR - observed;
            if (Math.Abs(error) <= pair.Tolerance)
            {
                continue;
            }

            changed = true;
            adjusted[index] = Math.Clamp(
                current[index] + (error / sensitivities[index]), -1, 1);
        }

        if (!changed)
        {
            return null;
        }

        ClampToDieBudgets(latent, adjusted);
        return adjusted;
    }

    private static void ClampToDieBudgets(LatentConfig latent, double[] dieCorrelations)
    {
        var budgets = new Dictionary<(TestStep Step, string Item), double>();
        for (var index = 0; index < latent.DiePairs.Count; index++)
        {
            var pair = latent.DiePairs[index];
            var magnitude = Math.Abs(dieCorrelations[index]);
            AddBudget(budgets, (TestStep.Cp, pair.CpItem), magnitude);
            AddBudget(budgets, (TestStep.Ft, pair.FtItem), magnitude);
        }

        for (var index = 0; index < latent.DiePairs.Count; index++)
        {
            var pair = latent.DiePairs[index];
            var magnitude = Math.Abs(dieCorrelations[index]);
            var cpOther = budgets[(TestStep.Cp, pair.CpItem)] - magnitude;
            var ftOther = budgets[(TestStep.Ft, pair.FtItem)] - magnitude;
            var allowed = Math.Min(1 - cpOther, 1 - ftOther);
            if (magnitude > allowed)
            {
                dieCorrelations[index] = Math.Sign(dieCorrelations[index]) * Math.Max(0, allowed);
            }
        }
    }

    private static void AddBudget(
        Dictionary<(TestStep Step, string Item), double> budgets,
        (TestStep Step, string Item) key,
        double magnitude) =>
        budgets[key] = budgets.GetValueOrDefault(key) + magnitude;
}
