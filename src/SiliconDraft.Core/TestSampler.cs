namespace SiliconDraft.Core;

/// <summary>
/// Samples one Test Item result. CP and FT share the rule: a Numeric item samples its
/// distribution, a Functional item samples its pass rate, and the result passes when it
/// falls inside the item's test limits.
/// </summary>
internal static class TestSampler
{
    public static TestResult Sample(TestItem item, ref SplitMix64 random)
    {
        var value = item switch
        {
            NumericTestItem numeric => numeric.Distribution.Sample(ref random),
            FunctionalTestItem functional => random.NextDouble() < functional.PassRate ? 1.0 : 0.0,
            _ => throw new ArgumentOutOfRangeException(nameof(item)),
        };
        var passed = value >= item.TestLimits.Low && value <= item.TestLimits.High;
        return new TestResult(item, value, passed);
    }

    public static TestResult[] SampleAll(IReadOnlyList<TestItem> items, ref SplitMix64 random)
    {
        var tests = new TestResult[items.Count];
        for (var index = 0; index < items.Count; index++)
        {
            tests[index] = Sample(items[index], ref random);
        }

        return tests;
    }
}
