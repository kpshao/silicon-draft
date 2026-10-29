namespace SiliconDraft.Core;

internal static class ConfigResolver
{
    private const double DefaultDiameterMm = 300;
    private const double DefaultEdgeExclusionMm = 3;
    private const double DefaultNotchAngleDeg = 270;
    private const double DefaultDieWidthMm = 5;
    private const double DefaultDieHeightMm = 5;
    private const double DefaultScribeMm = 0.08;

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
            // Lot ids become file-name stems; treat case-only variants as duplicates so
            // case-insensitive file systems cannot silently overwrite a lot's wafer map.
            var seenLotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                else if (!IsSafeFileStem(lotId))
                {
                    errors.Add(new ConfigError(
                        $"lots[{index}].id",
                        "must start with a letter or digit and contain only letters, digits, '.', '_' or '-'"));
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

        var wafer = ResolveWafer(document.Wafer, errors);

        if (errors.Count > 0)
        {
            return ConfigValidationResult.Invalid(errors);
        }

        var resolved = new ResolvedConfig(
            document.SchemaVersion!.Value,
            new ProductConfig(productName!),
            wafer!,
            document.Seed!.Value,
            document.Lots!
                .Select(lot => new LotConfig(lot.Id!.Trim(), lot.WaferCount!.Value))
                .ToArray(),
            new OutputConfig(outputDirectory!));

        return ConfigValidationResult.Valid(resolved);
    }

    private static WaferGeometry? ResolveWafer(WaferDocument? wafer, List<ConfigError> errors)
    {
        var initialErrorCount = errors.Count;

        var diameter = wafer?.DiameterMm ?? DefaultDiameterMm;
        ValidatePositive(errors, "wafer.diameterMm", diameter);

        var edgeExclusion = wafer?.EdgeExclusionMm ?? DefaultEdgeExclusionMm;
        ValidateNonNegative(errors, "wafer.edgeExclusionMm", edgeExclusion);
        if (IsFinitePositive(diameter) && double.IsFinite(edgeExclusion) && edgeExclusion >= diameter / 2)
        {
            errors.Add(new ConfigError(
                "wafer.edgeExclusionMm", "must be less than half the wafer diameter"));
        }

        var notchAngle = wafer?.NotchAngleDeg ?? DefaultNotchAngleDeg;
        if (!double.IsFinite(notchAngle) || notchAngle < 0)
        {
            errors.Add(new ConfigError("wafer.notchAngleDeg", "must be 0 or greater"));
        }
        else if (notchAngle >= 360)
        {
            errors.Add(new ConfigError("wafer.notchAngleDeg", "must be less than 360"));
        }

        var dieWidth = wafer?.Die?.WidthMm ?? DefaultDieWidthMm;
        var dieHeight = wafer?.Die?.HeightMm ?? DefaultDieHeightMm;
        var scribe = wafer?.Die?.ScribeMm ?? DefaultScribeMm;
        ValidatePositive(errors, "wafer.die.widthMm", dieWidth);
        ValidatePositive(errors, "wafer.die.heightMm", dieHeight);
        ValidateNonNegative(errors, "wafer.die.scribeMm", scribe);

        double pitchWidth;
        double pitchHeight;
        if (wafer?.Die?.Pitch is { } pitch)
        {
            if (pitch.WidthMm is null)
            {
                errors.Add(new ConfigError("wafer.die.pitch.widthMm", "required"));
            }

            if (pitch.HeightMm is null)
            {
                errors.Add(new ConfigError("wafer.die.pitch.heightMm", "required"));
            }

            pitchWidth = pitch.WidthMm ?? dieWidth + scribe;
            pitchHeight = pitch.HeightMm ?? dieHeight + scribe;
            if (pitch.WidthMm is double configuredWidth)
            {
                ValidatePositive(errors, "wafer.die.pitch.widthMm", configuredWidth);
                if (IsFinitePositive(configuredWidth) &&
                    IsFinitePositive(dieWidth) &&
                    configuredWidth < dieWidth)
                {
                    errors.Add(new ConfigError(
                        "wafer.die.pitch.widthMm", "must be at least the die width"));
                }
            }

            if (pitch.HeightMm is double configuredHeight)
            {
                ValidatePositive(errors, "wafer.die.pitch.heightMm", configuredHeight);
                if (IsFinitePositive(configuredHeight) &&
                    IsFinitePositive(dieHeight) &&
                    configuredHeight < dieHeight)
                {
                    errors.Add(new ConfigError(
                        "wafer.die.pitch.heightMm", "must be at least the die height"));
                }
            }
        }
        else
        {
            pitchWidth = dieWidth + scribe;
            pitchHeight = dieHeight + scribe;
        }

        var offsetX = wafer?.Grid?.OffsetMm?.X ?? 0;
        var offsetY = wafer?.Grid?.OffsetMm?.Y ?? 0;
        ValidateFinite(errors, "wafer.grid.offsetMm.x", offsetX);
        ValidateFinite(errors, "wafer.grid.offsetMm.y", offsetY);

        var rotationDeg = wafer?.Grid?.RotationDeg ?? 0;
        if (rotationDeg is not (0 or 90 or 180 or 270))
        {
            errors.Add(new ConfigError(
                "wafer.grid.rotationDeg", "must be one of 0, 90, 180 or 270"));
        }

        var indexOrigin = DieIndexOrigin.Center;
        if (wafer?.Grid?.IndexOrigin is { } indexOriginText &&
            !TryParseIndexOrigin(indexOriginText, out indexOrigin))
        {
            errors.Add(new ConfigError(
                "wafer.grid.indexOrigin",
                "must be one of 'center', 'bottomLeft', 'topLeft', 'topRight' or 'bottomRight'"));
        }

        var partialDiePolicy = PartialDiePolicy.Exclude;
        if (wafer?.PartialDiePolicy is { } policyText &&
            !TryParsePartialDiePolicy(policyText, out partialDiePolicy))
        {
            errors.Add(new ConfigError(
                "wafer.partialDiePolicy", "must be one of 'exclude' or 'retain'"));
        }

        if (errors.Count > initialErrorCount)
        {
            return null;
        }

        var geometry = new WaferGeometry(
            diameter,
            edgeExclusion,
            notchAngle,
            dieWidth,
            dieHeight,
            scribe,
            pitchWidth,
            pitchHeight,
            offsetX,
            offsetY,
            rotationDeg,
            wafer?.Grid?.MirrorX ?? false,
            wafer?.Grid?.MirrorY ?? false,
            indexOrigin,
            partialDiePolicy);

        var dieMap = DieMapGenerator.TryGenerate(geometry);
        if (dieMap.Map is null)
        {
            errors.Add(new ConfigError("wafer", dieMap.Error!));
            return null;
        }

        return geometry;
    }

    private static bool TryParseIndexOrigin(string value, out DieIndexOrigin origin)
    {
        switch (value)
        {
            case "center":
                origin = DieIndexOrigin.Center;
                return true;
            case "bottomLeft":
                origin = DieIndexOrigin.BottomLeft;
                return true;
            case "topLeft":
                origin = DieIndexOrigin.TopLeft;
                return true;
            case "topRight":
                origin = DieIndexOrigin.TopRight;
                return true;
            case "bottomRight":
                origin = DieIndexOrigin.BottomRight;
                return true;
            default:
                origin = DieIndexOrigin.Center;
                return false;
        }
    }

    private static bool TryParsePartialDiePolicy(string value, out PartialDiePolicy policy)
    {
        switch (value)
        {
            case "exclude":
                policy = PartialDiePolicy.Exclude;
                return true;
            case "retain":
                policy = PartialDiePolicy.Retain;
                return true;
            default:
                policy = PartialDiePolicy.Exclude;
                return false;
        }
    }

    private static bool IsFinitePositive(double value) => double.IsFinite(value) && value > 0;

    /// <summary>
    /// Lot ids become file-name stems, so they must not be able to leave the output
    /// directory or need quoting in a comma-separated file.
    /// </summary>
    private static bool IsSafeFileStem(string value)
    {
        if (value.Length == 0 || !char.IsAsciiLetterOrDigit(value[0]))
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '.' && character != '_' && character != '-')
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidatePositive(List<ConfigError> errors, string path, double value)
    {
        if (!IsFinitePositive(value))
        {
            errors.Add(new ConfigError(path, "must be greater than 0"));
        }
    }

    private static void ValidateNonNegative(List<ConfigError> errors, string path, double value)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            errors.Add(new ConfigError(path, "must be 0 or greater"));
        }
    }

    private static void ValidateFinite(List<ConfigError> errors, string path, double value)
    {
        if (!double.IsFinite(value))
        {
            errors.Add(new ConfigError(path, "must be a finite number"));
        }
    }
}
