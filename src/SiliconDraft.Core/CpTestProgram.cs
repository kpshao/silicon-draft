namespace SiliconDraft.Core;

/// <summary>
/// The effective CP Test Program: the ordered Test Items and the Bin numbering
/// applied to every valid Die at CP.
/// </summary>
public sealed record CpProgram(
    string CatalogVersion,
    IReadOnlyList<TestItem> Items,
    CpBins Bins);

/// <summary>
/// A named measurement with a business meaning, a unit, spec and test limits,
/// and the Soft Bin reported when it is the first failing item.
/// </summary>
public abstract record TestItem
{
    protected TestItem(
        int testNumber,
        string name,
        string unit,
        Limits specLimits,
        Limits testLimits,
        int failSoftBin)
    {
        TestNumber = testNumber;
        Name = name;
        Unit = unit;
        SpecLimits = specLimits;
        TestLimits = testLimits;
        FailSoftBin = failSoftBin;
    }

    public int TestNumber { get; }

    public string Name { get; }

    public string Unit { get; }

    public Limits SpecLimits { get; }

    /// <summary>The limits used to decide pass/fail; exported as PTR limits.</summary>
    public Limits TestLimits { get; }

    public int FailSoftBin { get; }
}

/// <summary>A Test Item that yields a measured value.</summary>
public sealed record NumericTestItem : TestItem
{
    internal NumericTestItem(
        int testNumber,
        string name,
        string unit,
        Limits specLimits,
        Limits testLimits,
        int failSoftBin,
        Distribution distribution)
        : base(testNumber, name, unit, specLimits, testLimits, failSoftBin)
    {
        Distribution = distribution;
    }

    public Distribution Distribution { get; }
}

/// <summary>A Test Item that yields pass/fail against a pattern or condition.</summary>
public sealed record FunctionalTestItem : TestItem
{
    internal FunctionalTestItem(
        int testNumber,
        string name,
        string unit,
        Limits specLimits,
        Limits testLimits,
        int failSoftBin,
        double passRate)
        : base(testNumber, name, unit, specLimits, testLimits, failSoftBin)
    {
        PassRate = passRate;
    }

    /// <summary>Probability that a valid Die yields a passing result.</summary>
    public double PassRate { get; }
}

