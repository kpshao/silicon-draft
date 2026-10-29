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

    public long? Seed { get; init; }

    public List<LotDocument>? Lots { get; init; }

    public OutputDocument? Output { get; init; }
}

internal sealed class ProductDocument
{
    public string? Name { get; init; }
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
