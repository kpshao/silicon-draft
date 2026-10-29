namespace SiliconDraft.Core;

/// <summary>Die position counts for one wafer, one lot, or a whole Dataset.</summary>
public sealed record DieCounts(
    int GrossDie,
    int PartialDieFound,
    int PartialDieRetained)
{
    public static DieCounts Zero { get; } = new(0, 0, 0);

    public static DieCounts Sum(IEnumerable<DieCounts> counts)
    {
        var grossDie = 0;
        var partialDieFound = 0;
        var partialDieRetained = 0;
        foreach (var count in counts)
        {
            grossDie += count.GrossDie;
            partialDieFound += count.PartialDieFound;
            partialDieRetained += count.PartialDieRetained;
        }

        return new DieCounts(grossDie, partialDieFound, partialDieRetained);
    }
}
