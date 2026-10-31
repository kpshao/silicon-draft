namespace SiliconDraft.Core;

internal readonly record struct PairAllocation(int PairIndex, double Loading);

internal sealed record ItemPlan(
    TestItem Item,
    IReadOnlyList<PairAllocation> Allocations,
    double ResidualLoading,
    double LotWeight,
    double WaferWeight,
    double SpatialWeight,
    double DieWeight,
    double MeasurementWeight);

/// <summary>
/// Samples the Test Items of one step for one Die. The lot, wafer, and spatial factors
/// are supplied by the caller so CP and FT see the same shared factors for a Die; the
/// die residual, pair factors, and measurement noise are seeded from the Die identity.
/// </summary>
internal sealed class LatentStepSampler
{
    private const int StackPairCount = 16;

    private readonly ItemPlan[] _plans;
    private readonly int _pairCount;
    private readonly long _seed;
    private readonly TestStep _step;

    public LatentStepSampler(
        ItemPlan[] plans,
        int pairCount,
        TestProgram program,
        TestStep step,
        long seed)
    {
        _plans = plans;
        _pairCount = pairCount;
        Program = program;
        _step = step;
        _seed = seed;
    }

    public TestProgram Program { get; }

    public IReadOnlyList<TestResult> SampleAll(
        Die die, string lotId, string waferId, int siteNumber, LatentWaferContext context)
    {
        var random = new SplitMix64(
            DeterministicSeed.ForLatentDie(_seed, _step, lotId, waferId, die.X, die.Y));
        var pairRandom = new SplitMix64(
            DeterministicSeed.ForLatentPair(_seed, lotId, waferId, die.X, die.Y));

        Span<double> pairFactors = stackalloc double[StackPairCount];
        if (_pairCount > pairFactors.Length)
        {
            pairFactors = new double[_pairCount];
        }

        for (var index = 0; index < _pairCount; index++)
        {
            pairFactors[index] = pairRandom.NextNormal();
        }

        var lot = context.LotFactor;
        var wafer = context.WaferFactor;
        var spatial = context.SpatialAt(die.X, die.Y);
        var tests = new TestResult[_plans.Length];
        for (var index = 0; index < _plans.Length; index++)
        {
            var plan = _plans[index];
            var value = plan.Item switch
            {
                NumericTestItem numeric => NumericValue(numeric, plan, pairFactors, lot, wafer, spatial),
                FunctionalTestItem functional => random.NextDouble() < functional.PassRate ? 1.0 : 0.0,
                _ => throw new ArgumentOutOfRangeException(nameof(plan)),
            };

            var passed = value >= plan.Item.TestLimits.Low && value <= plan.Item.TestLimits.High;
            tests[index] = new TestResult(plan.Item, value, passed);
        }

        return tests;

        double NumericValue(
            NumericTestItem numeric,
            ItemPlan plan,
            Span<double> pairFactors,
            double lot,
            double wafer,
            double spatial)
        {
            var residual = random.NextNormal();
            var measurement = random.NextNormal();
            var dieFactor = plan.ResidualLoading * residual;
            foreach (var allocation in plan.Allocations)
            {
                dieFactor += allocation.Loading * pairFactors[allocation.PairIndex];
            }

            var latent = (plan.LotWeight * lot) +
                (plan.WaferWeight * wafer) +
                (plan.SpatialWeight * spatial) +
                (plan.DieWeight * dieFactor) +
                (plan.MeasurementWeight * measurement);
            var value = numeric.Distribution.Kind == DistributionKind.Normal
                ? numeric.Distribution.Mean + (numeric.Distribution.Sigma * latent)
                : Math.Exp(numeric.Distribution.Mean + (numeric.Distribution.Sigma * latent));
            return ApplySiteOffset(numeric, value, siteNumber);
        }
    }

    private static double ApplySiteOffset(
        NumericTestItem item, double value, int siteNumber)
    {
        if (item.SiteOffsets.Count == 0)
        {
            return value;
        }

        if (siteNumber < 1 || siteNumber > item.SiteOffsets.Count)
        {
            throw new InvalidOperationException(
                $"site {siteNumber} has no configured offset for test '{item.Name}'");
        }

        return value + item.SiteOffsets[siteNumber - 1];
    }
}
