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
}
