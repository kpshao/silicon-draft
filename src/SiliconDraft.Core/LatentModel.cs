namespace SiliconDraft.Core;

/// <summary>
/// Converts a target correlation on the measurement scale into the correlation the
/// standardized latent factors must carry, and into the share that the die layer must
/// supply after the shared lot, wafer, and spatial layers contribute their part.
/// </summary>
internal static class LatentCorrelation
{
    /// <summary>
    /// The latent correlation that produces <paramref name="targetR"/> between two Test
    /// Items' measured values, or <c>null</c> when the target cannot be represented.
    /// Normal and lognormal items use closed-form Gaussian identities, including mixed
    /// pairs.
    /// </summary>
    public static double? Calibrate(Distribution cp, Distribution ft, double targetR)
    {
        var value = (cp.Kind, ft.Kind) switch
        {
            (DistributionKind.Normal, DistributionKind.Normal) => targetR,
            (DistributionKind.Lognormal, DistributionKind.Lognormal) =>
                LognormalPair(cp.Sigma, ft.Sigma, targetR),
            (DistributionKind.Normal, DistributionKind.Lognormal) =>
                MixedPair(ft.Sigma, targetR),
            (DistributionKind.Lognormal, DistributionKind.Normal) =>
                MixedPair(cp.Sigma, targetR),
            _ => (double?)null,
        };

        return value is double latent && double.IsFinite(latent) && Math.Abs(latent) <= 1
            ? latent
            : null;
    }

    /// <summary>
    /// The die-layer correlation needed so that the shared layers plus the die layer add up
    /// to the target correlation; <c>null</c> when the variance components cannot reach it.
    /// Spatial loadings scale the shared spatial contribution and rescale every layer so
    /// the item's total model variance stays one.
    /// </summary>
    public static double? RequiredDieCorrelation(
        Distribution cp,
        Distribution ft,
        VarianceComponents variance,
        double cpSpatialLoading,
        double ftSpatialLoading,
        double targetR)
    {
        var latent = Calibrate(cp, ft, targetR);
        if (latent is not double latentR)
        {
            return null;
        }

        var shared = SharedCorrelation(variance, cpSpatialLoading, ftSpatialLoading);
        var dieSensitivity = DieSensitivity(variance, cpSpatialLoading, ftSpatialLoading);
        if (dieSensitivity <= 0)
        {
            return Math.Abs(latentR - shared) <= 1e-12 ? 0 : null;
        }

        var required = (latentR - shared) / dieSensitivity;
        return double.IsFinite(required) && Math.Abs(required) <= 1 + 1e-9 ? required : null;
    }

    /// <summary>Rescales an item's layer weights when its Spatial Field loading is not one.</summary>
    public static double Normalization(VarianceComponents variance, double spatialLoading) =>
        Math.Sqrt(1 - variance.Spatial + (variance.Spatial * spatialLoading * spatialLoading));

    /// <summary>
    /// The correlation the shared lot, wafer, and spatial layers contribute to a pair,
    /// after both items' spatial loadings and normalizations.
    /// </summary>
    public static double SharedCorrelation(
        VarianceComponents variance, double cpSpatialLoading, double ftSpatialLoading)
    {
        var normalization =
            Normalization(variance, cpSpatialLoading) * Normalization(variance, ftSpatialLoading);
        return (variance.Lot +
            variance.Wafer +
            (variance.Spatial * cpSpatialLoading * ftSpatialLoading)) / normalization;
    }

    /// <summary>How strongly the die layer moves the pair's observed correlation.</summary>
    public static double DieSensitivity(
        VarianceComponents variance, double cpSpatialLoading, double ftSpatialLoading)
    {
        var normalization =
            Normalization(variance, cpSpatialLoading) * Normalization(variance, ftSpatialLoading);
        return variance.Die / normalization;
    }

    private static double MixedPair(double logSigma, double targetR) =>
        targetR * Math.Sqrt(Math.Exp(logSigma * logSigma) - 1) / logSigma;

    private static double LognormalPair(double cpSigma, double ftSigma, double targetR)
    {
        var product = targetR *
            Math.Sqrt((Math.Exp(cpSigma * cpSigma) - 1) * (Math.Exp(ftSigma * ftSigma) - 1));
        return Math.Log(1 + product) / (cpSigma * ftSigma);
    }
}

/// <summary>A configured pair together with the die-layer calibration derived from it.</summary>
internal sealed record ResolvedDiePair(
    DieCorrelationPair Pair,
    double DieCorrelation,
    double DieSensitivity);

/// <summary>
/// The dataset-wide latent model: the variance shares every Test Item uses, its Spatial
/// Field loadings, and the calibrated die-level correlation of each CP-FT pair. Step
/// samplers draw values from the shared lot, wafer, and spatial factors plus step-specific
/// die and measurement noise.
/// </summary>
internal sealed class LatentModel
{
    private readonly VarianceComponents _variance;
    private readonly ResolvedDiePair[] _pairs;
    private readonly Dictionary<(TestStep Step, string Item), double> _spatialLoadings;

    private LatentModel(
        VarianceComponents variance,
        ResolvedDiePair[] pairs,
        Dictionary<(TestStep Step, string Item), double> spatialLoadings)
    {
        _variance = variance;
        _pairs = pairs;
        _spatialLoadings = spatialLoadings;
    }

    public static LatentModel Create(
        LatentConfig config,
        TestProgram cp,
        TestProgram ft,
        IReadOnlyList<double>? dieCorrelations = null)
    {
        if (dieCorrelations is not null && dieCorrelations.Count != config.DiePairs.Count)
        {
            throw new ArgumentException(
                "one die correlation is required per configured pair", nameof(dieCorrelations));
        }

        var cpNumeric = cp.Items
            .OfType<NumericTestItem>()
            .ToDictionary(item => item.Name, StringComparer.Ordinal);
        var ftNumeric = ft.Items
            .OfType<NumericTestItem>()
            .ToDictionary(item => item.Name, StringComparer.Ordinal);
        var loadings = config.SpatialLoadings.ToDictionary(
            loading => (loading.Step, loading.Name),
            loading => loading.Loading);

        var pairs = new ResolvedDiePair[config.DiePairs.Count];
        for (var index = 0; index < config.DiePairs.Count; index++)
        {
            var pair = config.DiePairs[index];
            if (!cpNumeric.TryGetValue(pair.CpItem, out var cpItem))
            {
                throw new InvalidOperationException(
                    $"CP test item '{pair.CpItem}' is not numeric or does not exist");
            }

            if (!ftNumeric.TryGetValue(pair.FtItem, out var ftItem))
            {
                throw new InvalidOperationException(
                    $"FT test item '{pair.FtItem}' is not numeric or does not exist");
            }

            var cpLoading = loadings.GetValueOrDefault((TestStep.Cp, pair.CpItem), 1.0);
            var ftLoading = loadings.GetValueOrDefault((TestStep.Ft, pair.FtItem), 1.0);
            double dieCorrelation;
            if (dieCorrelations is not null)
            {
                dieCorrelation = dieCorrelations[index];
            }
            else
            {
                var die = LatentCorrelation.RequiredDieCorrelation(
                    cpItem.Distribution,
                    ftItem.Distribution,
                    config.Variance,
                    cpLoading,
                    ftLoading,
                    pair.TargetR);
                if (die is not double calibrated)
                {
                    throw new InvalidOperationException(
                        $"die-level pair '{pair.CpItem}' / '{pair.FtItem}' cannot reach r={pair.TargetR}");
                }

                dieCorrelation = calibrated;
            }

            pairs[index] = new ResolvedDiePair(
                pair,
                dieCorrelation,
                LatentCorrelation.DieSensitivity(config.Variance, cpLoading, ftLoading));
        }

        return new LatentModel(config.Variance, pairs, loadings);
    }

    /// <summary>The calibrated die-layer correlation of each pair, in configuration order.</summary>
    public IReadOnlyList<double> DieCorrelations =>
        _pairs.Select(pair => pair.DieCorrelation).ToArray();

    /// <summary>How strongly each pair's die layer moves its achieved correlation.</summary>
    public IReadOnlyList<double> DieSensitivities =>
        _pairs.Select(pair => pair.DieSensitivity).ToArray();

    public LatentStepSampler CreateSampler(TestProgram program, TestStep step, long seed)
    {
        var allocations = new List<PairAllocation>?[program.Items.Count];
        for (var pairIndex = 0; pairIndex < _pairs.Length; pairIndex++)
        {
            var resolved = _pairs[pairIndex];
            var itemName = step == TestStep.Cp ? resolved.Pair.CpItem : resolved.Pair.FtItem;
            var itemIndex = TestProgramLookup.IndexOf(program, itemName);
            var sign = step == TestStep.Cp || resolved.DieCorrelation >= 0 ? 1.0 : -1.0;
            var loading = sign * Math.Sqrt(Math.Abs(resolved.DieCorrelation));
            (allocations[itemIndex] ??= []).Add(new PairAllocation(pairIndex, loading));
        }

        var plans = new ItemPlan[program.Items.Count];
        for (var index = 0; index < plans.Length; index++)
        {
            var item = program.Items[index];
            var itemAllocations = allocations[index] ?? [];
            var spatialLoading = _spatialLoadings.GetValueOrDefault((step, item.Name), 1.0);
            var normalization = LatentCorrelation.Normalization(_variance, spatialLoading);
            double budget = 0;
            foreach (var allocation in itemAllocations)
            {
                budget += allocation.Loading * allocation.Loading;
            }

            plans[index] = new ItemPlan(
                item,
                itemAllocations.ToArray(),
                item is NumericTestItem ? Math.Sqrt(Math.Max(0, 1 - budget)) : 0,
                Math.Sqrt(_variance.Lot) / normalization,
                Math.Sqrt(_variance.Wafer) / normalization,
                Math.Sqrt(_variance.Spatial) * spatialLoading / normalization,
                Math.Sqrt(_variance.Die) / normalization,
                Math.Sqrt(_variance.Measurement) / normalization);
        }

        return new LatentStepSampler(plans, _pairs.Length, program, step, seed);
    }
}
