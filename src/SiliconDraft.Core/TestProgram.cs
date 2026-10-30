namespace SiliconDraft.Core;

/// <summary>
/// The effective Test Program for one test step: the ordered Test Items, the Bin
/// numbering, and the Tester Sites the step runs in parallel.
/// </summary>
public sealed record TestProgram(
    string CatalogVersion,
    IReadOnlyList<TestItem> Items,
    TestBins Bins,
    int SiteCount);

/// <summary>
/// The Hard and Soft Bin numbering a Test Program applies. The first failing Test Item
/// decides the outcome: a Numeric failure maps to the Parametric Fail Hard Bin, a
/// Functional failure to the Functional Fail Hard Bin, and the item's failure Soft Bin
/// identifies what failed.
/// </summary>
public sealed record TestBins(
    int PassHardBin,
    int PassSoftBin,
    int ParametricFailHardBin,
    int FunctionalFailHardBin)
{
    internal static TestBins Default { get; } = new(1, 1, 2, 3);

    internal (int HardBin, int SoftBin) Classify(IReadOnlyList<TestResult> tests)
    {
        var failure = tests.FirstOrDefault(test => !test.Passed);
        var hardBin = failure is null
            ? PassHardBin
            : failure.Item is FunctionalTestItem
                ? FunctionalFailHardBin
                : ParametricFailHardBin;
        var softBin = failure?.Item.FailSoftBin ?? PassSoftBin;
        return (hardBin, softBin);
    }
}

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
        Distribution distribution,
        IReadOnlyList<double> siteOffsets)
        : base(testNumber, name, unit, specLimits, testLimits, failSoftBin)
    {
        Distribution = distribution;
        SiteOffsets = siteOffsets;
    }

    public Distribution Distribution { get; }

    /// <summary>
    /// Additive measurement offset per Tester Site, in this item's unit. Empty for FT
    /// items, which run on one site without offsets.
    /// </summary>
    public IReadOnlyList<double> SiteOffsets { get; }
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

/// <summary>One Test Item result for one Die or FT Unit.</summary>
public sealed record TestResult(TestItem Item, double Value, bool Passed);

