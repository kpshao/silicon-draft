namespace SiliconDraft.Core;

/// <summary>
/// Generates the CP outcome of every valid Die. The RNG for a Die is derived only from
/// the Dataset seed, the Lot and Wafer ids, and the Die coordinates, so scheduling and
/// wafer order cannot change the data.
/// </summary>
internal static class CpGenerator
{
    public static IReadOnlyList<CpDieResult> Generate(
        LatentStepSampler sampler,
        TestProgram program,
        long seed,
        string lotId,
        string waferId,
        DieMap dieMap,
        LatentWaferContext context)
    {
        var results = new List<CpDieResult>(dieMap.GrossDie);
        foreach (var die in dieMap.Dies)
        {
            if (!die.Tested)
            {
                continue;
            }

            var siteRandom = new SplitMix64(
                DeterministicSeed.ForCpSite(seed, lotId, waferId, die.X, die.Y));
            var site = 1 + siteRandom.NextInt(program.SiteCount);
            var tests = sampler.SampleAll(die, lotId, waferId, site, context);
            results.Add(CpDieResult.Create(die, site, tests, program.Bins));
        }

        return results;
    }
}
