namespace SiliconDraft.Core;

/// <summary>
/// One packaged device tested at FT. It traces to exactly one source Wafer and Die,
/// which may have passed CP (a normal unit) or failed CP (an Escape).
/// </summary>
public sealed record FtUnit(
    string Id,
    string SourceWaferId,
    Die SourceDie,
    bool CpPassed,
    IReadOnlyList<TestResult> Tests,
    int HardBin,
    int SoftBin)
{
    /// <summary>A CP-failing Die that nevertheless became an FT Unit.</summary>
    public bool IsEscape => !CpPassed;

    public bool Passed => FirstFailure is null;

    /// <summary>The first failing FT Test Item; its type and Soft Bin determine the Bins.</summary>
    public TestResult? FirstFailure => Tests.FirstOrDefault(test => !test.Passed);

    internal static FtUnit Create(
        string waferId,
        Die die,
        bool cpPassed,
        IReadOnlyList<TestResult> tests,
        TestBins bins)
    {
        var (hardBin, softBin) = bins.Classify(tests);
        return new FtUnit(
            DieIdentity.UnitId(waferId, die),
            waferId,
            die,
            cpPassed,
            tests,
            hardBin,
            softBin);
    }
}

/// <summary>Aggregated FT outcome for a lot or Dataset.</summary>
public sealed record FtSummary(
    int UnitCount,
    int CpPassedUnits,
    int EscapeUnits,
    int AssemblyLoss,
    int PassedUnits,
    IReadOnlyList<BinCount> HardBins,
    IReadOnlyList<BinCount> SoftBins)
{
    public double Yield => UnitCount == 0 ? 0 : Math.Round((double)PassedUnits / UnitCount, 6);

    /// <summary>
    /// <paramref name="cpPassingDie"/> is the number of CP-passing Die that could have
    /// become FT Units; the difference from the non-Escape units is the Assembly Loss.
    /// </summary>
    internal static FtSummary Create(
        TestProgram program, IReadOnlyList<FtUnit> units, int cpPassingDie)
    {
        var cpPassedUnits = units.Count(unit => !unit.IsEscape);
        var escapeUnits = units.Count - cpPassedUnits;
        var assemblyLoss = cpPassingDie - cpPassedUnits;
        if (assemblyLoss < 0)
        {
            throw new InvalidOperationException(
                $"{cpPassedUnits} non-Escape FT units exceed {cpPassingDie} CP-passing die");
        }

        var (hardBins, softBins) = BinSummary.Create(
            program, units.Select(unit => (unit.HardBin, unit.SoftBin)));
        return new FtSummary(
            units.Count,
            cpPassedUnits,
            escapeUnits,
            assemblyLoss,
            units.Count(unit => unit.Passed),
            hardBins,
            softBins);
    }
}
