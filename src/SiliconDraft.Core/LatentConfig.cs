namespace SiliconDraft.Core;

/// <summary>The test step a Test Item belongs to.</summary>
public enum TestStep
{
    Cp,
    Ft,
}

internal static class TestStepKey
{
    /// <summary>The uppercase step name used in deterministic seed paths.</summary>
    public static string For(TestStep step) => step == TestStep.Cp ? "CP" : "FT";
}

/// <summary>
/// The five layers a Test Item's model variance decomposes into. Every component is a
/// fraction of the item's total model variance and the five fractions sum to one.
/// </summary>
public sealed record VarianceComponents(
    double Lot,
    double Wafer,
    double Spatial,
    double Die,
    double Measurement)
{
    /// <summary>
    /// Default fractions: lot effects are small, wafer and spatial effects moderate, and
    /// most variation is die-to-die, with a fixed measurement-noise floor.
    /// </summary>
    public static VarianceComponents Default { get; } = new(0.03, 0.05, 0.05, 0.72, 0.15);
}

/// <summary>
/// A configured die-level CP-FT correlation pair: the CP Test Item, the FT Test Item,
/// the target Pearson correlation on the measurement scale, and the allowed deviation.
/// </summary>
public sealed record DieCorrelationPair(
    string CpItem,
    string FtItem,
    double TargetR,
    double Tolerance)
{
    public const double DefaultTolerance = 0.05;
}

/// <summary>
/// A Test Item's sensitivity to the shared Spatial Field. The default is 1; a higher
/// value gives the item a larger spatial variance share, and every other layer is
/// rescaled so the item's total model variance stays the same.
/// </summary>
public sealed record SpatialLoading(TestStep Step, string Name, double Loading);

/// <summary>
/// The effective latent-factor settings: how Test Item variance is split across the five
/// layers, per-item Spatial Field loadings, and which CP-FT pairs are calibrated to a
/// target die-level correlation.
/// </summary>
public sealed record LatentConfig(
    VarianceComponents Variance,
    IReadOnlyList<DieCorrelationPair> DiePairs,
    IReadOnlyList<SpatialLoading> SpatialLoadings)
{
    /// <summary>
    /// Physically motivated defaults: drive current, input capacitance, and leakage
    /// relationships across CP and FT, plus the inverse threshold-voltage/speed relation.
    /// </summary>
    public static IReadOnlyList<DieCorrelationPair> DefaultDiePairs { get; } =
    [
        new("Idsat", "Ioh", 0.70, DieCorrelationPair.DefaultTolerance),
        new("Vt", "Fmax", -0.25, DieCorrelationPair.DefaultTolerance),
        new("Cin", "Cin", 0.80, DieCorrelationPair.DefaultTolerance),
        new("Leakage", "Iil", 0.65, DieCorrelationPair.DefaultTolerance),
        new("Iddq", "Icc", 0.60, DieCorrelationPair.DefaultTolerance),
    ];
}
