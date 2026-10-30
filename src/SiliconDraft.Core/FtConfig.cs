namespace SiliconDraft.Core;

/// <summary>
/// Effective FT settings after defaults are applied: the Assembly Loss applied per Lot
/// and the Escape rate admitted from CP-failing Die. Recorded in the Resolved Config.
/// </summary>
public sealed record FtConfig
{
    internal FtConfig(int assemblyLoss, double escapeRate)
    {
        AssemblyLoss = assemblyLoss;
        EscapeRate = escapeRate;
    }

    /// <summary>CP-passing Die per Lot that never become FT Units.</summary>
    public int AssemblyLoss { get; }

    /// <summary>Probability that a CP-failing Die nevertheless becomes an FT Unit.</summary>
    public double EscapeRate { get; }

    internal static FtConfig Default { get; } = new(0, 0);
}
