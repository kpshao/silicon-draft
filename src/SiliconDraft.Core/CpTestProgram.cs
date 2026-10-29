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
        CpLimits specLimits,
        CpLimits testLimits,
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

    public CpLimits SpecLimits { get; }

    /// <summary>The limits used to decide pass/fail; exported as PTR limits.</summary>
    public CpLimits TestLimits { get; }

    public int FailSoftBin { get; }
}

/// <summary>A Test Item that yields a measured value.</summary>
public sealed record NumericTestItem : TestItem
{
    internal NumericTestItem(
        int testNumber,
        string name,
        string unit,
        CpLimits specLimits,
        CpLimits testLimits,
        int failSoftBin,
        CpDistribution distribution)
        : base(testNumber, name, unit, specLimits, testLimits, failSoftBin)
    {
        Distribution = distribution;
    }

    public CpDistribution Distribution { get; }
}

/// <summary>A Test Item that yields pass/fail against a pattern or condition.</summary>
public sealed record FunctionalTestItem : TestItem
{
    internal FunctionalTestItem(
        int testNumber,
        string name,
        string unit,
        CpLimits specLimits,
        CpLimits testLimits,
        int failSoftBin,
        double passRate)
        : base(testNumber, name, unit, specLimits, testLimits, failSoftBin)
    {
        PassRate = passRate;
    }

    /// <summary>Probability that a valid Die yields a passing result.</summary>
    public double PassRate { get; }
}

public enum CpDistributionKind
{
    Normal,
    Lognormal,
}

/// <summary>
/// Numeric Test Item distribution. For <see cref="CpDistributionKind.Normal"/> the
/// parameters are in value space; for <see cref="CpDistributionKind.Lognormal"/> they
/// are the mean and standard deviation of the underlying normal in log space.
/// </summary>
public sealed record CpDistribution(CpDistributionKind Kind, double Mean, double Sigma);
