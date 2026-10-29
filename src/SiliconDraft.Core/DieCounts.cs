namespace SiliconDraft.Core;

/// <summary>Die counts for one wafer, one lot, or a whole Dataset.</summary>
public sealed record DieCounts(
    int GrossDie,
    int NetDie,
    int PartialDieFound,
    int PartialDieRetained)
{
    public static DieCounts Zero { get; } = new(0, 0, 0, 0);

    public static DieCounts Sum(IEnumerable<DieCounts> counts)
    {
        var grossDie = 0;
        var netDie = 0;
        var partialDieFound = 0;
        var partialDieRetained = 0;
        foreach (var count in counts)
        {
            grossDie += count.GrossDie;
            netDie += count.NetDie;
            partialDieFound += count.PartialDieFound;
            partialDieRetained += count.PartialDieRetained;
        }

        return new DieCounts(grossDie, netDie, partialDieFound, partialDieRetained);
    }
}
