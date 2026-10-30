namespace SiliconDraft.Core;

/// <summary>
/// The CP outcome of one valid Die: every Test Item result, the Tester Site that tested
/// it, and the assigned Bins.
/// </summary>
public sealed record CpDieResult(
    Die Die,
    int Site,
    IReadOnlyList<TestResult> Tests,
    int HardBin,
    int SoftBin)
{
    public bool Passed => FirstFailure is null;

    /// <summary>The first failing Test Item; its type and Soft Bin determine the Bins.</summary>
    public TestResult? FirstFailure => Tests.FirstOrDefault(test => !test.Passed);

    internal static CpDieResult Create(
        Die die, int site, IReadOnlyList<TestResult> tests, TestBins bins)
    {
        var (hardBin, softBin) = bins.Classify(tests);
        return new CpDieResult(die, site, tests, hardBin, softBin);
    }
}

/// <summary>Aggregated CP outcome for a wafer, lot, or Dataset.</summary>
public sealed record CpSummary(
    int TestedDie,
    int PassedDie,
    IReadOnlyList<BinCount> HardBins,
    IReadOnlyList<BinCount> SoftBins)
{
    public double Yield => TestedDie == 0 ? 0 : Math.Round((double)PassedDie / TestedDie, 6);

    internal static CpSummary Create(TestProgram program, IReadOnlyList<CpDieResult> results)
    {
        var (hardBins, softBins) = BinSummary.Create(
            program, results.Select(result => (result.HardBin, result.SoftBin)));
        return new CpSummary(
            results.Count,
            results.Count(result => result.Passed),
            hardBins,
            softBins);
    }
}
