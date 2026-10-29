namespace SiliconDraft.Core;

/// <summary>
/// The die positions of one wafer after the Partial Die policy is applied.
/// </summary>
public sealed class DieMap
{
    internal DieMap(IReadOnlyList<Die> dies, DieCounts counts)
    {
        Dies = dies;
        Counts = counts;
    }

    /// <summary>
    /// Valid die plus, when the policy retains them, Partial Die marked as untested.
    /// Ordered by Y then X.
    /// </summary>
    public IReadOnlyList<Die> Dies { get; }

    public DieCounts Counts { get; }

    /// <summary>Number of valid die positions.</summary>
    public int GrossDie => Counts.GrossDie;

    /// <summary>Partial Die positions that intersect the physical wafer, whether retained or not.</summary>
    public int PartialDieFound => Counts.PartialDieFound;

    public int PartialDieRetained => Counts.PartialDieRetained;
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
