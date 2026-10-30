namespace SiliconDraft.Core;

/// <summary>One Bin's definition and count.</summary>
public sealed record BinCount(int Number, string Name, bool Pass, int Count);

/// <summary>Counts Hard and Soft Bins against the effective Test Program definitions.</summary>
internal static class BinSummary
{
    public static (IReadOnlyList<BinCount> HardBins, IReadOnlyList<BinCount> SoftBins) Create(
        TestProgram program, IEnumerable<(int HardBin, int SoftBin)> results)
    {
        var hardDefinitions = new (int Number, string Name, bool Pass)[]
        {
            (program.Bins.PassHardBin, "Pass", true),
            (program.Bins.ParametricFailHardBin, "Parametric Fail", false),
            (program.Bins.FunctionalFailHardBin, "Functional Fail", false),
        };
        var softDefinitions = new List<(int Number, string Name, bool Pass)>
        {
            (program.Bins.PassSoftBin, "Pass", true),
        };
        softDefinitions.AddRange(
            program.Items.Select(item => (item.FailSoftBin, item.Name, false)));

        var hardCounts = hardDefinitions.ToDictionary(definition => definition.Number, _ => 0);
        var softCounts = softDefinitions.ToDictionary(definition => definition.Number, _ => 0);

        foreach (var (hardBin, softBin) in results)
        {
            hardCounts[hardBin]++;
            softCounts[softBin]++;
        }

        return (
            hardDefinitions
                .Select(definition => new BinCount(
                    definition.Number, definition.Name, definition.Pass, hardCounts[definition.Number]))
                .OrderBy(bin => bin.Number)
                .ToArray(),
            softDefinitions
                .Select(definition => new BinCount(
                    definition.Number, definition.Name, definition.Pass, softCounts[definition.Number]))
                .OrderBy(bin => bin.Number)
                .ToArray());
    }
}
