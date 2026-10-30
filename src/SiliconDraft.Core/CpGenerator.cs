namespace SiliconDraft.Core;

/// <summary>
/// Generates the CP outcome of every valid Die. The RNG for a Die is derived only from
/// the Dataset seed, the Lot and Wafer ids, and the Die coordinates, so scheduling and
/// wafer order cannot change the data.
/// </summary>
internal static class CpGenerator
{
    public static IReadOnlyList<CpDieResult> Generate(
        CpProgram program,
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

            var random = new SplitMix64(DeterministicSeed.ForDie(seed, lotId, waferId, die.X, die.Y));
            var tests = new TestResult[program.Items.Count];
            for (var index = 0; index < program.Items.Count; index++)
            {
                var item = program.Items[index];
                var value = Sample(item, ref random);
                var passed = value >= item.TestLimits.Low && value <= item.TestLimits.High;
                tests[index] = new TestResult(item, value, passed);
            }

            results.Add(CpDieResult.Create(die, tests, program.Bins));
        }

        return results;
    }

    private static double Sample(TestItem item, ref SplitMix64 random) => item switch
    {
        NumericTestItem numeric => numeric.Distribution.Sample(ref random),
        FunctionalTestItem functional => random.NextDouble() < functional.PassRate ? 1.0 : 0.0,
        _ => throw new ArgumentOutOfRangeException(nameof(item)),
    };
}
