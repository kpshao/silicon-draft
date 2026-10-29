namespace SiliconDraft.Core;

/// <summary>The CP outcome of one valid Die: every Test Item result and the assigned Bins.</summary>
public sealed record CpDieResult(
    Die Die,
    IReadOnlyList<TestResult> Tests,
    int HardBin,
    int SoftBin)
{
    public bool Passed => FirstFailure is null;

    /// <summary>The first failing Test Item; its type and Soft Bin determine the Bins.</summary>
    public TestResult? FirstFailure => Tests.FirstOrDefault(test => !test.Passed);

    internal static CpDieResult Create(Die die, IReadOnlyList<TestResult> tests, CpBins bins)
    {
        var failure = tests.FirstOrDefault(test => !test.Passed);
        var hardBin = failure is null
            ? bins.PassHardBin
            : failure.Item is FunctionalTestItem
                ? bins.FunctionalFailHardBin
                : bins.ParametricFailHardBin;
        var softBin = failure?.Item.FailSoftBin ?? bins.PassSoftBin;
        return new CpDieResult(die, tests, hardBin, softBin);
    }
}

/// <summary>One Test Item result for one Die.</summary>
public sealed record TestResult(TestItem Item, double Value, bool Passed);

/// <summary>One Bin's definition and count.</summary>
public sealed record CpBinCount(int Number, string Name, bool Pass, int Count);

/// <summary>Aggregated CP outcome for a wafer, lot, or Dataset.</summary>
public sealed record CpSummary(
    int TestedDie,
    int PassedDie,
    IReadOnlyList<CpBinCount> HardBins,
    IReadOnlyList<CpBinCount> SoftBins)
{
    public double Yield => TestedDie == 0 ? 0 : Math.Round((double)PassedDie / TestedDie, 6);

    internal static CpSummary Create(CpProgram program, IReadOnlyList<CpDieResult> results)
    {
        var hardDefinitions = new (int Number, string Name, bool Pass)[]
        {
            (program.Bins.PassHardBin, "Pass", true),
            (program.Bins.ParametricFailHardBin, "Parametric Fail", false),
            (program.Bins.FunctionalFailHardBin, "Functional Fail", false),
        };
        var softDefinitions = new List<(int Number, string Name, bool Pass)>
        {
            (program.Bins.PassSoftBin, "Pass", true),
        };
        softDefinitions.AddRange(
            program.Items.Select(item => (item.FailSoftBin, item.Name, false)));

        var hardCounts = hardDefinitions.ToDictionary(definition => definition.Number, _ => 0);
        var softCounts = softDefinitions.ToDictionary(definition => definition.Number, _ => 0);

        foreach (var result in results)
        {
            hardCounts[result.HardBin]++;
            softCounts[result.SoftBin]++;
        }

        return new CpSummary(
            results.Count,
            results.Count(result => result.Passed),
            hardDefinitions
                .Select(definition => new CpBinCount(
                    definition.Number, definition.Name, definition.Pass, hardCounts[definition.Number]))
                .OrderBy(bin => bin.Number)
                .ToArray(),
            softDefinitions
                .Select(definition => new CpBinCount(
                    definition.Number, definition.Name, definition.Pass, softCounts[definition.Number]))
                .OrderBy(bin => bin.Number)
                .ToArray());
    }
}
