namespace SiliconDraft.Core;

/// <summary>
/// The built-in minimal WAT catalog. Every entry declares its business meaning via its
/// name and unit, physically plausible distribution defaults, and spec and test limits.
/// The catalog is versioned because exported data records it.
/// </summary>
internal static class WatCatalog
{
    public const string Version = "wat-catalog-v1";

    public static IReadOnlyList<WatTestItem> DefaultItems { get; } =
    [
        Item(3001, "Vtn", "V", new Limits(0.39, 0.51), new Limits(0.40, 0.50),
            DistributionKind.Normal, mean: 0.45, sigma: 0.02),
        Item(3002, "Vtp", "V", new Limits(-0.51, -0.39), new Limits(-0.50, -0.40),
            DistributionKind.Normal, mean: -0.45, sigma: 0.02),
        Item(3003, "IdsatN", "mA", new Limits(390, 510), new Limits(400, 500),
            DistributionKind.Normal, mean: 450, sigma: 20),
        Item(3004, "IdsatP", "mA", new Limits(190, 250), new Limits(200, 240),
            DistributionKind.Normal, mean: 220, sigma: 12),
        Item(3005, "Tox", "nm", new Limits(1.9, 2.1), new Limits(1.92, 2.08),
            DistributionKind.Normal, mean: 2.0, sigma: 0.03),
        Item(3006, "Rs", "Ohm/sq", new Limits(100, 140), new Limits(105, 135),
            DistributionKind.Normal, mean: 120, sigma: 5),
        Item(3007, "Rc", "Ohm", new Limits(0, 8), new Limits(0, 6),
            DistributionKind.Lognormal, mean: 1.204, sigma: 0.15),
        Item(3008, "Vbd", "V", new Limits(10, 14), new Limits(10.5, 13.5),
            DistributionKind.Normal, mean: 12, sigma: 0.5),
    ];

    public static WatProgram CreateProgram() => new(Version, DefaultItems);

    private static WatTestItem Item(
        int testNumber,
        string name,
        string unit,
        Limits specLimits,
        Limits testLimits,
        DistributionKind kind,
        double mean,
        double sigma) =>
        new(testNumber, name, unit, specLimits, testLimits, new Distribution(kind, mean, sigma));
}
