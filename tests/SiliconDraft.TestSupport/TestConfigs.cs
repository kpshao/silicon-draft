namespace SiliconDraft.TestSupport;

public static class TestConfigs
{
    public const string Valid = """
        {
          "schemaVersion": 1,
          "product": { "name": "DemoProduct" },
          "seed": 42,
          "lots": [ { "id": "LOT-0001", "waferCount": 1 } ],
          "output": { "directory": "out" }
        }
        """;

    /// <summary>Builds a valid configuration around an explicit <c>wafer</c> section.</summary>
    public static string WithWafer(string waferJson, string lotsJson = """[ { "id": "LOT-0001", "waferCount": 1 } ]""") => $$"""
        {
          "schemaVersion": 1,
          "product": { "name": "DemoProduct" },
          "seed": 42,
          "wafer": {{waferJson}},
          "lots": {{lotsJson}},
          "output": { "directory": "out" }
        }
        """;

    /// <summary>Builds a valid configuration around an explicit <c>wat</c> section.</summary>
    public static string WithWat(
        string watJson,
        string? waferJson = null,
        string lotsJson = """[ { "id": "LOT-0001", "waferCount": 1 } ]""") => $$"""
        {
          "schemaVersion": 1,
          "product": { "name": "DemoProduct" },
          "seed": 42,
          {{(waferJson is null ? string.Empty : $"\"wafer\": {waferJson},")}}
          "wat": {{watJson}},
          "lots": {{lotsJson}},
          "output": { "directory": "out" }
        }
        """;

    /// <summary>Builds a valid configuration around an explicit <c>cp</c> section.</summary>
    public static string WithCp(
        string cpJson,
        string? waferJson = null,
        string lotsJson = """[ { "id": "LOT-0001", "waferCount": 1 } ]""") => $$"""
        {
          "schemaVersion": 1,
          "product": { "name": "DemoProduct" },
          "seed": 42,
          {{(waferJson is null ? string.Empty : $"\"wafer\": {waferJson},")}}
          "cp": {{cpJson}},
          "lots": {{lotsJson}},
          "output": { "directory": "out" }
        }
        """;
}
