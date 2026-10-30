namespace SiliconDraft.Core;

/// <summary>
/// The stable identity shared by a Die's CP record and the FT Unit it becomes, so CP-FT
/// joins survive export and cannot drift between the two steps.
/// </summary>
internal static class DieIdentity
{
    public static string UnitId(string waferId, Die die) => $"{waferId}:{die.X},{die.Y}";
}
