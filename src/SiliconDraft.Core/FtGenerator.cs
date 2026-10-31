namespace SiliconDraft.Core;

/// <summary>
/// Selects the FT Units of a Dataset from its CP results and generates their FT outcome.
/// Assembly Loss removes a configured number of CP-passing Die from the whole Dataset;
/// Escapes admit CP-failing Die at the configured rate. Every selection is seeded from
/// the Dataset seed and the Die identity, so scheduling and iteration order cannot
/// change it.
/// </summary>
internal static class FtGenerator
{
    public static string AssemblyLotId(string lotId) => $"ALOT-{lotId}";

    /// <summary>
    /// Chooses the CP-passing Die lost to Assembly Loss across the Dataset. The loss is
    /// applied to the Dataset as a whole so the configured count matches the remaining
    /// FT Unit count exactly.
    /// </summary>
    public static IReadOnlySet<FtDieKey> SelectAssemblyLoss(
        long seed,
        int count,
        IReadOnlyList<(string LotId, IReadOnlyList<Wafer> Wafers)> lots)
    {
        var passing = lots
            .SelectMany(lot => lot.Wafers.SelectMany(wafer => wafer.CpResults
                .Where(result => result.Passed)
                .Select(result => new FtDieKey(
                    lot.LotId, wafer.Id, result.Die.X, result.Die.Y))))
            .OrderBy(key => key.LotId, StringComparer.Ordinal)
            .ThenBy(key => key.WaferId, StringComparer.Ordinal)
            .ThenBy(key => key.Y)
            .ThenBy(key => key.X)
            .ToArray();

        if (count > passing.Length)
        {
            throw new InvalidOperationException(
                $"the Dataset has {passing.Length} CP-passing die but ft.assemblyLoss is {count}");
        }

        if (count == 0)
        {
            return new HashSet<FtDieKey>();
        }

        var random = new SplitMix64(DeterministicSeed.ForFtAssemblyLoss(seed));
        for (var index = passing.Length - 1; index > 0; index--)
        {
            var swap = random.NextInt(index + 1);
            (passing[index], passing[swap]) = (passing[swap], passing[index]);
        }

        return passing[..count].ToHashSet();
    }

    public static IReadOnlyList<FtUnit> Generate(
        LatentStepSampler sampler,
        FtConfig config,
        long seed,
        string lotId,
        IReadOnlyList<Wafer> wafers,
        IReadOnlySet<FtDieKey> assemblyLoss,
        IReadOnlyDictionary<(string LotId, string WaferId), LatentWaferContext> contexts)
    {
        var sources = wafers
            .SelectMany(wafer => wafer.CpResults.Select(result => new FtSource(wafer.Id, result)))
            .ToArray();

        var units = new List<FtUnit>(sources.Length);
        foreach (var source in sources)
        {
            var admitted = source.Result.Passed
                ? !assemblyLoss.Contains(
                    new FtDieKey(lotId, source.WaferId, source.Result.Die.X, source.Result.Die.Y))
                : IsEscape(config.EscapeRate, seed, lotId, source);
            if (admitted)
            {
                units.Add(CreateUnit(
                    sampler, lotId, source, contexts[(lotId, source.WaferId)]));
            }
        }

        units.Sort((first, second) =>
        {
            var waferComparison = string.CompareOrdinal(first.SourceWaferId, second.SourceWaferId);
            if (waferComparison != 0)
            {
                return waferComparison;
            }

            var rowComparison = first.SourceDie.Y.CompareTo(second.SourceDie.Y);
            return rowComparison != 0
                ? rowComparison
                : first.SourceDie.X.CompareTo(second.SourceDie.X);
        });
        return units;
    }

    private static bool IsEscape(double escapeRate, long seed, string lotId, FtSource source) =>
        escapeRate > 0 &&
        new SplitMix64(DeterministicSeed.ForFtEscape(
                seed, lotId, source.WaferId, source.Result.Die.X, source.Result.Die.Y))
            .NextDouble() < escapeRate;

    private static FtUnit CreateUnit(
        LatentStepSampler sampler,
        string lotId,
        FtSource source,
        LatentWaferContext context)
    {
        var die = source.Result.Die;
        var tests = sampler.SampleAll(
            die, lotId, source.WaferId, siteNumber: 1, context);
        return FtUnit.Create(
            source.WaferId, die, source.Result.Passed, tests, sampler.Program.Bins);
    }

    private sealed record FtSource(string WaferId, CpDieResult Result);
}

/// <summary>A CP-passing Die identified across the Dataset for Assembly Loss selection.</summary>
internal readonly record struct FtDieKey(string LotId, string WaferId, int X, int Y);
