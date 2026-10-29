namespace SiliconDraft.Core;

/// <summary>
/// The die positions of one wafer after the Partial Die policy is applied.
/// </summary>
public sealed class DieMap
{
    internal DieMap(
        IReadOnlyList<Die> dies,
        int grossDie,
        int netDie,
        int partialDieFound,
        int partialDieRetained)
    {
        Dies = dies;
        GrossDie = grossDie;
        NetDie = netDie;
        PartialDieFound = partialDieFound;
        PartialDieRetained = partialDieRetained;
    }

    /// <summary>
    /// Valid die plus, when the policy retains them, Partial Die marked as untested.
    /// Ordered by Y then X.
    /// </summary>
    public IReadOnlyList<Die> Dies { get; }

    /// <summary>Number of valid die positions.</summary>
    public int GrossDie { get; }

    /// <summary>
    /// Number of die that pass CP. The CP step does not exist yet, so every valid die passes.
    /// </summary>
    public int NetDie { get; }

    /// <summary>Partial Die positions that intersect the physical wafer, whether retained or not.</summary>
    public int PartialDieFound { get; }

    public int PartialDieRetained { get; }
}

public sealed record Die(
    int X,
    int Y,
    double CenterXmm,
    double CenterYmm,
    DieDisposition Disposition)
{
    public bool Tested => Disposition == DieDisposition.Valid;
}

public enum DieDisposition
{
    Valid,
    Partial,
}
