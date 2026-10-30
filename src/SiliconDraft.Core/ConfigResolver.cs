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
                else if (!IsSafeToken(lotId))
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
        var cp = ResolveCp(document.Cp, errors);
        var wat = ResolveWat(document.Wat, wafer, errors);

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
            cp,
            wat!,
            new OutputConfig(outputDirectory!));

        return ConfigValidationResult.Valid(resolved);
    }

    private static WatConfig? ResolveWat(
        WatDocument? wat, WaferGeometry? wafer, List<ConfigError> errors)
    {
        var template = WatTemplate.Long;
        if (wat?.Template is { } templateText && !TryParseWatTemplate(templateText, out template))
        {
            errors.Add(new ConfigError("wat.template", "must be one of 'long' or 'wide'"));
        }

        var organization = WatFileOrganization.PerLot;
        if (wat?.Organization is { } organizationText &&
            !TryParseWatOrganization(organizationText, out organization))
        {
            errors.Add(new ConfigError(
                "wat.organization", "must be one of 'perLot' or 'perWafer'"));
        }

        IReadOnlyList<PcmSite>? sites;
        if (wat?.Sites is null)
        {
            if (wafer is null)
            {
                return null;
            }

            sites = PcmSite.CreateDefaultLayout(wafer);
        }
        else
        {
            sites = ResolvePcmSites(wat.Sites, wafer?.DiameterMm, errors);
        }

        return new WatConfig(sites, template, organization);
    }

    private static IReadOnlyList<PcmSite> ResolvePcmSites(
        List<PcmSiteDocument?> documents, double? diameterMm, List<ConfigError> errors)
    {
        var sites = new List<PcmSite>(documents.Count);
        if (documents.Count == 0)
        {
            errors.Add(new ConfigError("wat.sites", "must contain at least one site"));
            return sites;
        }

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < documents.Count; index++)
        {
            var path = $"wat.sites[{index}]";
            var document = documents[index];
            if (document is null)
            {
                errors.Add(new ConfigError(path, "required"));
                continue;
            }

            var id = document.Id?.Trim();
            if (string.IsNullOrEmpty(id))
            {
                errors.Add(new ConfigError($"{path}.id", "required"));
                continue;
            }

            if (!seenIds.Add(id))
            {
                errors.Add(new ConfigError($"{path}.id", $"duplicate site id '{id}'"));
                continue;
            }

            if (!IsSafeToken(id))
            {
                errors.Add(new ConfigError(
                    $"{path}.id",
                    "must start with a letter or digit and contain only letters, digits, '.', '_' or '-'"));
                continue;
            }

            if (document.XMm is not double x)
            {
                errors.Add(new ConfigError($"{path}.xMm", "required"));
                continue;
            }

            if (document.YMm is not double y)
            {
                errors.Add(new ConfigError($"{path}.yMm", "required"));
                continue;
            }

            if (!double.IsFinite(x))
            {
                errors.Add(new ConfigError($"{path}.xMm", "must be a finite number"));
                continue;
            }

            if (!double.IsFinite(y))
            {
                errors.Add(new ConfigError($"{path}.yMm", "must be a finite number"));
                continue;
            }

            if (diameterMm is double diameter && Math.Sqrt(x * x + y * y) > diameter / 2)
            {
                errors.Add(new ConfigError(path, "must lie within the wafer"));
                continue;
            }

            sites.Add(new PcmSite(id, x, y));
        }

        return sites;
    }

    private static bool TryParseWatTemplate(string value, out WatTemplate template)
    {
        switch (value)
        {
            case "long":
                template = WatTemplate.Long;
                return true;
            case "wide":
                template = WatTemplate.Wide;
                return true;
            default:
                template = WatTemplate.Long;
                return false;
        }
    }

    private static bool TryParseWatOrganization(string value, out WatFileOrganization organization)
    {
        switch (value)
        {
            case "perLot":
                organization = WatFileOrganization.PerLot;
                return true;
            case "perWafer":
                organization = WatFileOrganization.PerWafer;
                return true;
            default:
                organization = WatFileOrganization.PerLot;
                return false;
        }
    }

    private static CpConfig ResolveCp(CpDocument? cp, List<ConfigError> errors)
    {
        var bins = ResolveCpBins(cp?.Bins, errors);
        var overrides = ResolveCpTestItems(cp?.TestItems, errors);

        ValidateEffectiveSoftBins(bins, overrides, errors);

        return new CpConfig(bins, overrides);
    }

    private static CpBins ResolveCpBins(CpBinsDocument? bins, List<ConfigError> errors)
    {
        var passHardBin = bins?.PassHardBin ?? CpBins.Default.PassHardBin;
        var passSoftBin = bins?.PassSoftBin ?? CpBins.Default.PassSoftBin;
        var parametricFailHardBin = bins?.ParametricFailHardBin ?? CpBins.Default.ParametricFailHardBin;
        var functionalFailHardBin = bins?.FunctionalFailHardBin ?? CpBins.Default.FunctionalFailHardBin;

        ValidateBinNumber(errors, "cp.bins.passHardBin", passHardBin);
        ValidateBinNumber(errors, "cp.bins.passSoftBin", passSoftBin);
        ValidateBinNumber(errors, "cp.bins.parametricFailHardBin", parametricFailHardBin);
        ValidateBinNumber(errors, "cp.bins.functionalFailHardBin", functionalFailHardBin);

        if (parametricFailHardBin == passHardBin)
        {
            errors.Add(new ConfigError(
                "cp.bins.parametricFailHardBin", "must be distinct from cp.bins.passHardBin"));
        }

        if (functionalFailHardBin == passHardBin)
        {
            errors.Add(new ConfigError(
                "cp.bins.functionalFailHardBin", "must be distinct from cp.bins.passHardBin"));
        }

        if (functionalFailHardBin == parametricFailHardBin)
        {
            errors.Add(new ConfigError(
                "cp.bins.functionalFailHardBin",
                "must be distinct from cp.bins.parametricFailHardBin"));
        }

        return new CpBins(passHardBin, passSoftBin, parametricFailHardBin, functionalFailHardBin);
    }

    private static List<CpTestItemOverride> ResolveCpTestItems(
        List<CpTestItemDocument>? documents, List<ConfigError> errors)
    {
        var overrides = new List<CpTestItemOverride>();
        if (documents is null)
        {
            return overrides;
        }

        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < documents.Count; index++)
        {
            var path = $"cp.testItems[{index}]";
            var document = documents[index];
            if (document is null)
            {
                errors.Add(new ConfigError(path, "required"));
                continue;
            }

            var name = document.Name?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                errors.Add(new ConfigError($"{path}.name", "required"));
                continue;
            }

            if (CpCatalog.Find(name) is null)
            {
                errors.Add(new ConfigError($"{path}.name", $"unknown CP test item '{name}'"));
                continue;
            }

            if (!seenNames.Add(name))
            {
                errors.Add(new ConfigError($"{path}.name", $"duplicate CP test item '{name}'"));
                continue;
            }

            var specLimits = ResolveLimits(document.SpecLimits, $"{path}.specLimits", errors);
            var testLimits = ResolveLimits(document.TestLimits, $"{path}.testLimits", errors);

            int? failSoftBin = null;
            if (document.FailSoftBin is int configuredBin)
            {
                if (IsValidBinNumber(configuredBin))
                {
                    failSoftBin = configuredBin;
                }
                else
                {
                    errors.Add(new ConfigError(
                        $"{path}.failSoftBin", "must be between 1 and 65535"));
                }
            }

            overrides.Add(new CpTestItemOverride(name, specLimits, testLimits, failSoftBin));
        }

        return overrides;
    }

    private static Limits? ResolveLimits(
        LimitsDocument? limits, string path, List<ConfigError> errors)
    {
        if (limits is null)
        {
            return null;
        }

        if (limits.Low is not double low)
        {
            errors.Add(new ConfigError($"{path}.low", "required"));
        }
        else if (!double.IsFinite(low))
        {
            errors.Add(new ConfigError($"{path}.low", "must be a finite number"));
        }

        if (limits.High is not double high)
        {
            errors.Add(new ConfigError($"{path}.high", "required"));
        }
        else if (!double.IsFinite(high))
        {
            errors.Add(new ConfigError($"{path}.high", "must be a finite number"));
        }

        if (limits.Low is double lower && limits.High is double upper &&
            double.IsFinite(lower) && double.IsFinite(upper) && lower >= upper)
        {
            errors.Add(new ConfigError(path, "low must be less than high"));
        }

        return limits.Low is double l && limits.High is double h ? new Limits(l, h) : null;
    }

    private static void ValidateEffectiveSoftBins(
        CpBins bins, List<CpTestItemOverride> overrides, List<ConfigError> errors)
    {
        var overridesByName = new Dictionary<string, (CpTestItemOverride Item, int Index)>(
            StringComparer.Ordinal);
        for (var index = 0; index < overrides.Count; index++)
        {
            overridesByName[overrides[index].Name] = (overrides[index], index);
        }

        var assigned = new Dictionary<int, (string ItemName, string? OverridePath)>();
        foreach (var template in CpCatalog.DefaultItems)
        {
            var overrideInfo = overridesByName.GetValueOrDefault(template.Name);
            var effectiveBin = overrideInfo.Item?.FailSoftBin ?? template.FailSoftBin;
            var overridePath = overrideInfo.Item is null
                ? null
                : $"cp.testItems[{overrideInfo.Index}].failSoftBin";
            var reportedOnOverride = overrideInfo.Item?.FailSoftBin is not null;

            if (effectiveBin == bins.PassSoftBin)
            {
                errors.Add(new ConfigError(
                    reportedOnOverride ? overridePath! : "cp.bins.passSoftBin",
                    reportedOnOverride
                        ? $"conflicts with cp.bins.passSoftBin ({bins.PassSoftBin})"
                        : $"conflicts with the default fail soft bin of '{template.Name}' ({effectiveBin})"));
                continue;
            }

            if (assigned.TryGetValue(effectiveBin, out var other))
            {
                errors.Add(new ConfigError(
                    reportedOnOverride
                        ? overridePath!
                        : other.OverridePath ?? $"cp.testItems[{overrideInfo.Index}].failSoftBin",
                    $"duplicate soft bin {effectiveBin}; already used by '{other.ItemName}'"));
                continue;
            }

            assigned[effectiveBin] = (template.Name, reportedOnOverride ? overridePath : null);
        }
    }

    private static void ValidateBinNumber(List<ConfigError> errors, string path, int value)
    {
        if (!IsValidBinNumber(value))
        {
            errors.Add(new ConfigError(path, "must be between 1 and 65535"));
        }
    }

    private static bool IsValidBinNumber(int value) => value is >= 1 and <= 65535;

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
            // Each axis may be overridden independently; the other keeps the default.
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
    /// Lot ids become file-name stems and site ids label output rows, so both must not be
    /// able to leave the output directory or need quoting in a delimited file.
    /// </summary>
    private static bool IsSafeToken(string value)
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
