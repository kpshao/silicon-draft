namespace SiliconDraft.Core;

internal static class ConfigResolver
{
    public static ConfigValidationResult Resolve(ConfigDocument document)
    {
        var errors = new List<ConfigError>();

        if (document.SchemaVersion is null)
        {
            errors.Add(new ConfigError("schemaVersion", "required"));
        }
        else if (document.SchemaVersion != ConfigLoader.ConfigSchemaVersion)
        {
            errors.Add(new ConfigError(
                "schemaVersion",
                $"unsupported schema version {document.SchemaVersion}; supported version is {ConfigLoader.ConfigSchemaVersion}"));
        }

        var productName = document.Product?.Name?.Trim();
        if (document.Product is null)
        {
            errors.Add(new ConfigError("product", "required"));
        }
        else if (string.IsNullOrEmpty(productName))
        {
            errors.Add(new ConfigError("product.name", "required"));
        }

        if (document.Seed is null)
        {
            errors.Add(new ConfigError("seed", "required"));
        }

        if (document.Lots is null || document.Lots.Count == 0)
        {
            errors.Add(new ConfigError("lots", "must contain at least one lot"));
        }
        else
        {
            var seenLotIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < document.Lots.Count; index++)
            {
                var lot = document.Lots[index];
                if (lot is null)
                {
                    errors.Add(new ConfigError($"lots[{index}]", "required"));
                    continue;
                }

                var lotId = lot.Id?.Trim();
                if (string.IsNullOrEmpty(lotId))
                {
                    errors.Add(new ConfigError($"lots[{index}].id", "required"));
                }
                else if (!seenLotIds.Add(lotId))
                {
                    errors.Add(new ConfigError($"lots[{index}].id", $"duplicate lot id '{lotId}'"));
                }

                if (lot.WaferCount is null)
                {
                    errors.Add(new ConfigError($"lots[{index}].waferCount", "required"));
                }
                else if (lot.WaferCount < 1)
                {
                    errors.Add(new ConfigError($"lots[{index}].waferCount", "must be at least 1"));
                }
            }
        }

        var outputDirectory = document.Output?.Directory?.Trim();
        if (document.Output is null)
        {
            errors.Add(new ConfigError("output", "required"));
        }
        else if (string.IsNullOrEmpty(outputDirectory))
        {
            errors.Add(new ConfigError("output.directory", "required"));
        }

        if (errors.Count > 0)
        {
            return ConfigValidationResult.Invalid(errors);
        }

        var resolved = new ResolvedConfig(
            document.SchemaVersion!.Value,
            new ProductConfig(productName!),
            document.Seed!.Value,
            document.Lots!
                .Select(lot => new LotConfig(lot.Id!.Trim(), lot.WaferCount!.Value))
                .ToArray(),
            new OutputConfig(outputDirectory!));

        return ConfigValidationResult.Valid(resolved);
    }
}
