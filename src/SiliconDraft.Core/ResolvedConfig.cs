namespace SiliconDraft.Core;

/// <summary>
/// The effective configuration after defaults and presets are applied.
/// Recorded in the ground-truth manifest. Only <see cref="GenerationPipeline.Validate"/>
/// can produce one, so generation never runs from an invalid configuration.
/// </summary>
public sealed record ResolvedConfig
{
    internal ResolvedConfig(
        int schemaVersion,
        ProductConfig product,
        long seed,
        IReadOnlyList<LotConfig> lots,
        OutputConfig output)
    {
        SchemaVersion = schemaVersion;
        Product = product;
        Seed = seed;
        Lots = lots;
        Output = output;
    }

    public int SchemaVersion { get; }

    public ProductConfig Product { get; }

    public long Seed { get; }

    public IReadOnlyList<LotConfig> Lots { get; }

    public OutputConfig Output { get; }
}

public sealed record ProductConfig
{
    internal ProductConfig(string name) => Name = name;

    public string Name { get; }
}

public sealed record LotConfig
{
    internal LotConfig(string id, int waferCount)
    {
        Id = id;
        WaferCount = waferCount;
    }

    public string Id { get; }

    public int WaferCount { get; }
}

public sealed record OutputConfig
{
    internal OutputConfig(string directory) => Directory = directory;

    public string Directory { get; }
}
