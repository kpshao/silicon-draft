namespace SiliconDraft.Core;

public enum DistributionKind
{
    Normal,
    Lognormal,
}

/// <summary>
/// Numeric Test Item distribution. For <see cref="DistributionKind.Normal"/> the
/// parameters are in value space; for <see cref="DistributionKind.Lognormal"/> they
/// are the mean and standard deviation of the underlying normal in log space.
/// </summary>
public sealed record Distribution(DistributionKind Kind, double Mean, double Sigma)
{
    internal double Sample(ref SplitMix64 random) => Kind switch
    {
        DistributionKind.Normal => Mean + Sigma * random.NextNormal(),
        DistributionKind.Lognormal => Math.Exp(Mean + Sigma * random.NextNormal()),
        _ => throw new ArgumentOutOfRangeException(nameof(Kind)),
    };
}
