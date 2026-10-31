namespace SiliconDraft.Core;

/// <summary>
/// Generates the CP outcome of every valid Die. The RNG for a Die is derived only from
/// the Dataset seed, the Lot and Wafer ids, and the Die coordinates, so scheduling and
/// wafer order cannot change the data.
/// </summary>
internal static class CpGenerator
{
    public static IReadOnlyList<CpDieResult> Generate(
        TestProgram program,
        long seed,
        string lotId,
        string waferId,
        DieMap dieMap)
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
            var random = new SplitMix64(DeterministicSeed.ForDie(seed, lotId, waferId, die.X, die.Y));
            var tests = TestSampler.SampleAll(program.Items, ref random, site);
            results.Add(CpDieResult.Create(die, site, tests, program.Bins));
        }

        return results;
    }
}
