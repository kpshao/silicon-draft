using System.Text.Json;
using System.Text.Json.Serialization;

namespace SiliconDraft.Core;

internal static class ConfigLoader
{
    public const int ConfigSchemaVersion = 1;

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
    };

    public static ConfigValidationResult Validate(string configPath)
    {
        string json;
        try
        {
            json = File.ReadAllText(configPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return ConfigValidationResult.Unreadable(
                [new ConfigError("config", $"cannot read '{configPath}': {ex.Message}")]);
        }

        ConfigDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<ConfigDocument>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            var path = string.IsNullOrEmpty(ex.Path) ? "config" : ex.Path;
            var location = ex.LineNumber is null
                ? string.Empty
                : $" (line {ex.LineNumber + 1}, position {ex.BytePositionInLine + 1})";
            return ConfigValidationResult.Invalid(
                [new ConfigError(path, $"invalid JSON{location}: {ex.Message}")]);
        }

        if (document is null)
        {
            return ConfigValidationResult.Invalid(
                [new ConfigError("config", "configuration document is empty")]);
        }

        return ConfigResolver.Resolve(document);
    }
}

internal sealed class ConfigDocument
{
    public int? SchemaVersion { get; init; }

    public ProductDocument? Product { get; init; }

    public WaferDocument? Wafer { get; init; }

    public long? Seed { get; init; }

    public List<LotDocument>? Lots { get; init; }

    public CpDocument? Cp { get; init; }

    public FtDocument? Ft { get; init; }

    public WatDocument? Wat { get; init; }

    public LatentDocument? Latent { get; init; }

    public OutputDocument? Output { get; init; }
}

internal sealed class LatentDocument
{
    public VarianceDocument? Variance { get; init; }

    public List<SpatialLoadingDocument?>? SpatialLoadings { get; init; }

    public List<DiePairDocument?>? DiePairs { get; init; }
}

internal sealed class SpatialLoadingDocument
{
    public string? Step { get; init; }

    public string? Name { get; init; }

    public double? Loading { get; init; }
}

internal sealed class VarianceDocument
{
    public double? Lot { get; init; }

    public double? Wafer { get; init; }

    public double? Spatial { get; init; }

    public double? Die { get; init; }

    public double? Measurement { get; init; }
}

internal sealed class DiePairDocument
{
    public string? CpItem { get; init; }

    public string? FtItem { get; init; }

    public double? TargetR { get; init; }

    public double? Tolerance { get; init; }
}

internal sealed class WatDocument
{
    public List<PcmSiteDocument?>? Sites { get; init; }

    public string? Template { get; init; }

    public string? Organization { get; init; }

    public List<WatTestItemDocument>? TestItems { get; init; }
}

internal sealed class WatTestItemDocument
{
    public string? Name { get; init; }

    public int? TestNumber { get; init; }

    public string? Unit { get; init; }

    public LimitsDocument? SpecLimits { get; init; }

    public LimitsDocument? TestLimits { get; init; }

    public DistributionDocument? Distribution { get; init; }
}

internal sealed class PcmSiteDocument
{
    public string? Id { get; init; }

    public double? XMm { get; init; }

    public double? YMm { get; init; }
}

internal sealed class CpDocument
{
    public int? SiteCount { get; init; }

    public CpBinsDocument? Bins { get; init; }

    public List<CpTestItemDocument>? TestItems { get; init; }
}

internal sealed class CpBinsDocument
{
    public int? PassHardBin { get; init; }

    public int? PassSoftBin { get; init; }

    public int? ParametricFailHardBin { get; init; }

    public int? FunctionalFailHardBin { get; init; }
}

internal sealed class CpTestItemDocument
{
    public string? Name { get; init; }

    public int? TestNumber { get; init; }

    public string? Unit { get; init; }

    public string? Type { get; init; }

    public LimitsDocument? SpecLimits { get; init; }

    public LimitsDocument? TestLimits { get; init; }

    public int? FailSoftBin { get; init; }

    public List<double?>? SiteOffsets { get; init; }

    public DistributionDocument? Distribution { get; init; }

    public double? PassRate { get; init; }
}

internal sealed class FtDocument
{
    public int? AssemblyLoss { get; init; }

    public double? EscapeRate { get; init; }

    public List<FtTestItemDocument>? TestItems { get; init; }
}

internal sealed class FtTestItemDocument
{
    public string? Name { get; init; }

    public int? TestNumber { get; init; }

    public string? Unit { get; init; }

    public string? Type { get; init; }

    public LimitsDocument? SpecLimits { get; init; }

    public LimitsDocument? TestLimits { get; init; }

    public int? FailSoftBin { get; init; }

    public DistributionDocument? Distribution { get; init; }

    public double? PassRate { get; init; }
}

internal sealed class DistributionDocument
{
    public string? Kind { get; init; }

    public double? Mean { get; init; }

    public double? Sigma { get; init; }
}

internal sealed class LimitsDocument
{
    public double? Low { get; init; }

    public double? High { get; init; }
}

internal sealed class ProductDocument
{
    public string? Name { get; init; }
}

internal sealed class WaferDocument
{
    public double? DiameterMm { get; init; }

    public double? EdgeExclusionMm { get; init; }

    public double? NotchAngleDeg { get; init; }

    public DieDocument? Die { get; init; }

    public GridDocument? Grid { get; init; }

    public string? PartialDiePolicy { get; init; }
}

internal sealed class DieDocument
{
    public double? WidthMm { get; init; }

    public double? HeightMm { get; init; }

    public double? ScribeMm { get; init; }

    public PitchDocument? Pitch { get; init; }
}

internal sealed class PitchDocument
{
    public double? WidthMm { get; init; }

    public double? HeightMm { get; init; }
}

internal sealed class GridDocument
{
    public OffsetDocument? OffsetMm { get; init; }

    public int? RotationDeg { get; init; }

    public bool? MirrorX { get; init; }

    public bool? MirrorY { get; init; }

    public string? IndexOrigin { get; init; }
}

internal sealed class OffsetDocument
{
    public double? X { get; init; }

    public double? Y { get; init; }
}

internal sealed class LotDocument
{
    public string? Id { get; init; }

    public int? WaferCount { get; init; }
}

internal sealed class OutputDocument
{
    public string? Directory { get; init; }
}
