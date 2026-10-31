using System.Text;

namespace SiliconDraft.Core;

internal static class ConfigResolver
{
    private const int DefaultCpSiteCount = 8;
    private const int MaxExportedTextBytes = 255;
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
        var ft = ResolveFt(document.Ft, cp.Bins, errors);
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
            ft,
            wat!,
            new OutputConfig(outputDirectory!));

        return ConfigValidationResult.Valid(resolved);
    }

    private static FtConfig ResolveFt(
        FtDocument? ft, TestBins cpBins, List<ConfigError> errors)
    {
        var assemblyLoss = ft?.AssemblyLoss ?? FtConfig.Default.AssemblyLoss;
        if (assemblyLoss < 0)
        {
            errors.Add(new ConfigError("ft.assemblyLoss", "must be 0 or greater"));
        }

        var escapeRate = ft?.EscapeRate ?? FtConfig.Default.EscapeRate;
        if (!double.IsFinite(escapeRate) || escapeRate is < 0 or > 1)
        {
            errors.Add(new ConfigError("ft.escapeRate", "must be between 0 and 1"));
        }

        var (overrides, customItems) = ResolveFtTestItems(ft?.TestItems, errors);
        ValidateEffectiveFtSoftBins(overrides, customItems, cpBins, errors);

        return new FtConfig(
            assemblyLoss,
            escapeRate,
            overrides.Select(entry => entry.Item).ToArray(),
            customItems.Select(entry => entry.Item).ToArray());
    }

    private static (List<FtOverrideEntry> Overrides, List<CustomItemEntry> CustomItems)
        ResolveFtTestItems(List<FtTestItemDocument>? documents, List<ConfigError> errors)
    {
        var overrides = new List<FtOverrideEntry>();
        var customItems = new List<CustomItemEntry>();
        if (documents is null)
        {
            return (overrides, customItems);
        }

        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < documents.Count; index++)
        {
            var path = $"ft.testItems[{index}]";
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

            if (!seenNames.Add(name))
            {
                errors.Add(new ConfigError($"{path}.name", $"duplicate FT test item '{name}'"));
                continue;
            }

            ValidateExportedText(name, $"{path}.name", errors);

            var catalogItem = FtCatalog.Find(name);
            if (catalogItem is not null)
            {
                if (HasCustomDefinition(
                        document.TestNumber, document.Unit, document.Type,
                        document.Distribution, document.PassRate))
                {
                    errors.Add(new ConfigError(
                        path,
                        "a definition can only extend the catalog with a name the catalog does not define"));
                    continue;
                }

                var specLimits = ResolveLimits(document.SpecLimits, $"{path}.specLimits", errors);
                var testLimits = ResolveLimits(document.TestLimits, $"{path}.testLimits", errors);
                if (catalogItem is FunctionalTestItem &&
                    testLimits is { } functionalTestLimits &&
                    !IncludesPassValue(functionalTestLimits))
                {
                    errors.Add(new ConfigError(
                        $"{path}.testLimits",
                        "functional test limits must include the pass value 1"));
                }

                var failSoftBin = ResolveFailSoftBin(document.FailSoftBin, $"{path}.failSoftBin", errors);
                overrides.Add(new FtOverrideEntry(
                    new FtTestItemOverride(name, specLimits, testLimits, failSoftBin),
                    path));
                continue;
            }

            var customItem = ResolveCustomTestItem(
                name,
                path,
                document.TestNumber,
                document.Unit,
                document.Type,
                document.SpecLimits,
                document.TestLimits,
                document.FailSoftBin,
                document.Distribution,
                document.PassRate,
                siteOffsetsDocument: null,
                siteCount: 1,
                errors);
            if (customItem is not null)
            {
                customItems.Add(new CustomItemEntry(customItem, path));
            }
        }

        ValidateCustomTestNumbers(
            FtCatalog.DefaultItems.Select(item => item.TestNumber),
            customItems,
            errors);
        return (overrides, customItems);
    }

    private static void ValidateEffectiveFtSoftBins(
        IReadOnlyList<FtOverrideEntry> overrides,
        IReadOnlyList<CustomItemEntry> customItems,
        TestBins cpBins,
        List<ConfigError> errors)
    {
        var overridesByName = new Dictionary<string, FtOverrideEntry>(StringComparer.Ordinal);
        foreach (var entry in overrides)
        {
            overridesByName[entry.Item.Name] = entry;
        }

        var assigned = new Dictionary<int, (string ItemName, string? Path)>();
        foreach (var template in FtCatalog.DefaultItems)
        {
            var overrideEntry = overridesByName.GetValueOrDefault(template.Name);
            var effectiveBin = overrideEntry.Item?.FailSoftBin ?? template.FailSoftBin;
            var overridePath = overrideEntry.Item is null
                ? null
                : $"{overrideEntry.Path}.failSoftBin";
            var reportedOnOverride = overrideEntry.Item?.FailSoftBin is not null;
            ValidateFtFailSoftBin(
                effectiveBin,
                template.Name,
                reportedOnOverride ? overridePath! : "cp.bins.passSoftBin",
                reportedOnOverride,
                cpBins,
                assigned,
                errors);
        }

        foreach (var entry in customItems)
        {
            ValidateFtFailSoftBin(
                entry.Item.FailSoftBin,
                entry.Item.Name,
                $"{entry.Path}.failSoftBin",
                true,
                cpBins,
                assigned,
                errors);
        }
    }

    private static void ValidateFtFailSoftBin(
        int effectiveBin,
        string itemName,
        string path,
        bool reportedOnOverride,
        TestBins cpBins,
        Dictionary<int, (string ItemName, string? Path)> assigned,
        List<ConfigError> errors)
    {
        if (effectiveBin == cpBins.PassSoftBin)
        {
            errors.Add(new ConfigError(
                path,
                reportedOnOverride
                    ? $"conflicts with cp.bins.passSoftBin ({cpBins.PassSoftBin})"
                    : $"conflicts with the default FT fail soft bin of '{itemName}' ({effectiveBin})"));
            return;
        }

        if (assigned.TryGetValue(effectiveBin, out var other))
        {
            errors.Add(new ConfigError(
                other.Path ?? path,
                $"duplicate soft bin {effectiveBin}; already used by '{other.ItemName}'"));
            return;
        }

        assigned[effectiveBin] = (itemName, reportedOnOverride ? path : null);
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

        var (overrides, customItems) = ResolveWatTestItems(wat?.TestItems, errors);
        return new WatConfig(
            sites, template, organization, overrides, customItems);
    }

    private static (List<WatTestItemOverride> Overrides, List<WatTestItem> CustomItems)
        ResolveWatTestItems(List<WatTestItemDocument>? documents, List<ConfigError> errors)
    {
        var overrides = new List<WatTestItemOverride>();
        var customItems = new List<WatTestItem>();
        if (documents is null)
        {
            return (overrides, customItems);
        }

        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        var customTestNumbers = new List<(string Path, int TestNumber)>();
        for (var index = 0; index < documents.Count; index++)
        {
            var path = $"wat.testItems[{index}]";
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

            if (!seenNames.Add(name))
            {
                errors.Add(new ConfigError($"{path}.name", $"duplicate WAT test item '{name}'"));
                continue;
            }

            ValidateExportedText(name, $"{path}.name", errors);

            if (WatCatalog.Find(name) is not null)
            {
                if (document.TestNumber is not null || document.Unit is not null)
                {
                    errors.Add(new ConfigError(
                        path,
                        "a definition can only extend the catalog with a name the catalog does not define"));
                    continue;
                }

                var specLimits = ResolveLimits(document.SpecLimits, $"{path}.specLimits", errors);
                var testLimits = ResolveLimits(document.TestLimits, $"{path}.testLimits", errors);
                var distribution = document.Distribution is null
                    ? null
                    : ResolveDistribution(document.Distribution, $"{path}.distribution", errors);
                overrides.Add(new WatTestItemOverride(name, specLimits, testLimits, distribution));
                continue;
            }

            var customItem = ResolveCustomWatTestItem(
                name, path, document, errors);
            if (customItem is not null)
            {
                customItems.Add(customItem);
                customTestNumbers.Add((path, customItem.TestNumber));
            }
        }

        var seenTestNumbers = WatCatalog.DefaultItems
            .Select(item => item.TestNumber)
            .ToHashSet();
        foreach (var (path, testNumber) in customTestNumbers)
        {
            if (!seenTestNumbers.Add(testNumber))
            {
                errors.Add(new ConfigError(
                    $"{path}.testNumber",
                    $"duplicate test number {testNumber}; already used by another test item"));
            }
        }

        return (overrides, customItems);
    }

    private static WatTestItem? ResolveCustomWatTestItem(
        string name,
        string path,
        WatTestItemDocument document,
        List<ConfigError> errors)
    {
        var initialErrorCount = errors.Count;

        var testNumber = 0;
        if (document.TestNumber is not int configuredTestNumber)
        {
            errors.Add(new ConfigError($"{path}.testNumber", "required"));
        }
        else if (configuredTestNumber is < 1 or > 65535)
        {
            errors.Add(new ConfigError($"{path}.testNumber", "must be between 1 and 65535"));
        }
        else
        {
            testNumber = configuredTestNumber;
        }

        var unit = document.Unit?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(unit))
        {
            errors.Add(new ConfigError($"{path}.unit", "required"));
        }
        else
        {
            ValidateExportedText(unit, $"{path}.unit", errors);
        }

        var specLimits = ResolveLimits(document.SpecLimits, $"{path}.specLimits", errors);
        var testLimits = ResolveLimits(document.TestLimits, $"{path}.testLimits", errors);
        if (document.TestLimits is null)
        {
            errors.Add(new ConfigError($"{path}.testLimits", "required"));
        }

        Distribution? distribution = null;
        if (document.Distribution is null)
        {
            errors.Add(new ConfigError($"{path}.distribution", "required"));
        }
        else
        {
            distribution = ResolveDistribution(
                document.Distribution, $"{path}.distribution", errors);
        }

        if (errors.Count > initialErrorCount)
        {
            return null;
        }

        return new WatTestItem(
            testNumber,
            name,
            unit,
            specLimits ?? testLimits!,
            testLimits!,
            distribution!);
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
        var siteCount = cp?.SiteCount ?? DefaultCpSiteCount;
        if (siteCount is < CpConfig.MinSiteCount or > CpConfig.MaxSiteCount)
        {
            errors.Add(new ConfigError(
                "cp.siteCount",
                $"must be between {CpConfig.MinSiteCount} and {CpConfig.MaxSiteCount}"));
        }

        var bins = ResolveCpBins(cp?.Bins, errors);
        var (overrides, customItems) = ResolveCpTestItems(cp?.TestItems, siteCount, errors);

        ValidateEffectiveSoftBins(bins, overrides, customItems, errors);

        return new CpConfig(
            bins,
            overrides.Select(entry => entry.Item).ToArray(),
            customItems.Select(entry => entry.Item).ToArray(),
            siteCount);
    }

    private static TestBins ResolveCpBins(CpBinsDocument? bins, List<ConfigError> errors)
    {
        var passHardBin = bins?.PassHardBin ?? TestBins.Default.PassHardBin;
        var passSoftBin = bins?.PassSoftBin ?? TestBins.Default.PassSoftBin;
        var parametricFailHardBin = bins?.ParametricFailHardBin ?? TestBins.Default.ParametricFailHardBin;
        var functionalFailHardBin = bins?.FunctionalFailHardBin ?? TestBins.Default.FunctionalFailHardBin;

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

        return new TestBins(passHardBin, passSoftBin, parametricFailHardBin, functionalFailHardBin);
    }

    private static (List<CpOverrideEntry> Overrides, List<CustomItemEntry> CustomItems)
        ResolveCpTestItems(List<CpTestItemDocument>? documents, int siteCount, List<ConfigError> errors)
    {
        var overrides = new List<CpOverrideEntry>();
        var customItems = new List<CustomItemEntry>();
        if (documents is null)
        {
            return (overrides, customItems);
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

            if (!seenNames.Add(name))
            {
                errors.Add(new ConfigError($"{path}.name", $"duplicate CP test item '{name}'"));
                continue;
            }

            ValidateExportedText(name, $"{path}.name", errors);

            var catalogItem = CpCatalog.Find(name);
            if (catalogItem is not null)
            {
                if (HasCustomDefinition(
                        document.TestNumber, document.Unit, document.Type,
                        document.Distribution, document.PassRate))
                {
                    errors.Add(new ConfigError(
                        path,
                        "a definition can only extend the catalog with a name the catalog does not define"));
                    continue;
                }

                var specLimits = ResolveLimits(document.SpecLimits, $"{path}.specLimits", errors);
                var testLimits = ResolveLimits(document.TestLimits, $"{path}.testLimits", errors);
                if (catalogItem is FunctionalCatalogItem &&
                    testLimits is { } functionalTestLimits &&
                    !IncludesPassValue(functionalTestLimits))
                {
                    errors.Add(new ConfigError(
                        $"{path}.testLimits",
                        "functional test limits must include the pass value 1"));
                }

                var siteOffsets = ResolveSiteOffsets(
                    document.SiteOffsets,
                    $"{path}.siteOffsets",
                    siteCount,
                    isNumeric: catalogItem is NumericCatalogItem,
                    errors);
                var failSoftBin = ResolveFailSoftBin(
                    document.FailSoftBin, $"{path}.failSoftBin", errors);
                overrides.Add(new CpOverrideEntry(
                    new CpTestItemOverride(name, specLimits, testLimits, failSoftBin, siteOffsets),
                    path));
                continue;
            }

            var customItem = ResolveCustomTestItem(
                name,
                path,
                document.TestNumber,
                document.Unit,
                document.Type,
                document.SpecLimits,
                document.TestLimits,
                document.FailSoftBin,
                document.Distribution,
                document.PassRate,
                document.SiteOffsets,
                siteCount,
                errors);
            if (customItem is not null)
            {
                customItems.Add(new CustomItemEntry(customItem, path));
            }
        }

        ValidateCustomTestNumbers(
            CpCatalog.DefaultItems.Select(item => item.Definition.TestNumber),
            customItems,
            errors);
        return (overrides, customItems);
    }

    /// <summary>
    /// Resolves a Test Item the configuration defines beyond the catalog. Numeric items
    /// require a unit and a distribution; Functional items require a pass rate and use
    /// the P/F unit. Spec Limits default to the Test Limits when omitted.
    /// </summary>
    private static CustomTestItem? ResolveCustomTestItem(
        string name,
        string path,
        int? testNumber,
        string? unit,
        string? type,
        LimitsDocument? specLimitsDocument,
        LimitsDocument? testLimitsDocument,
        int? failSoftBin,
        DistributionDocument? distributionDocument,
        double? passRate,
        List<double?>? siteOffsetsDocument,
        int siteCount,
        List<ConfigError> errors)
    {
        var initialErrorCount = errors.Count;

        var resolvedTestNumber = 0;
        if (testNumber is not int configuredTestNumber)
        {
            errors.Add(new ConfigError($"{path}.testNumber", "required"));
        }
        else if (configuredTestNumber is < 1 or > 65535)
        {
            errors.Add(new ConfigError($"{path}.testNumber", "must be between 1 and 65535"));
        }
        else
        {
            resolvedTestNumber = configuredTestNumber;
        }

        if (type is null)
        {
            errors.Add(new ConfigError($"{path}.type", "required"));
            return null;
        }

        if (!TryParseTestItemKind(type, out var kind))
        {
            errors.Add(new ConfigError($"{path}.type", "must be one of 'numeric' or 'functional'"));
            return null;
        }

        var specLimits = ResolveLimits(specLimitsDocument, $"{path}.specLimits", errors);
        var testLimits = ResolveLimits(testLimitsDocument, $"{path}.testLimits", errors);
        var resolvedFailSoftBin = ResolveFailSoftBin(
            failSoftBin, $"{path}.failSoftBin", errors, required: true);

        string resolvedUnit;
        Distribution? distribution = null;
        double? resolvedPassRate = null;
        IReadOnlyList<double> siteOffsets = [];
        if (kind == TestItemKind.Numeric)
        {
            if (testLimitsDocument is null)
            {
                errors.Add(new ConfigError($"{path}.testLimits", "required"));
            }

            resolvedUnit = unit?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(resolvedUnit))
            {
                errors.Add(new ConfigError($"{path}.unit", "required"));
            }
            else
            {
                ValidateExportedText(resolvedUnit, $"{path}.unit", errors);
            }

            if (passRate is not null)
            {
                errors.Add(new ConfigError(
                    $"{path}.passRate", "can only be set for functional test items"));
            }

            if (distributionDocument is null)
            {
                errors.Add(new ConfigError($"{path}.distribution", "required"));
            }
            else
            {
                distribution = ResolveDistribution(
                    distributionDocument, $"{path}.distribution", errors);
            }

            siteOffsets = ResolveSiteOffsets(
                    siteOffsetsDocument, $"{path}.siteOffsets", siteCount, isNumeric: true, errors)
                ?? [];
        }
        else
        {
            if (unit is not null)
            {
                errors.Add(new ConfigError(
                    $"{path}.unit", "functional test items use the P/F unit"));
            }

            if (distributionDocument is not null)
            {
                errors.Add(new ConfigError(
                    $"{path}.distribution", "can only be set for numeric test items"));
            }

            if (siteOffsetsDocument is not null)
            {
                errors.Add(new ConfigError(
                    $"{path}.siteOffsets", "site offsets are only supported for numeric test items"));
            }

            if (passRate is not double configuredPassRate)
            {
                errors.Add(new ConfigError($"{path}.passRate", "required"));
            }
            else if (!double.IsFinite(configuredPassRate) || configuredPassRate is <= 0 or >= 1)
            {
                errors.Add(new ConfigError($"{path}.passRate", "must be between 0 and 1"));
            }
            else
            {
                resolvedPassRate = configuredPassRate;
            }

            // Functional results are sampled as 0 or 1, so limits that exclude the pass
            // value would fail every unit; require test limits to include 1.
            if (testLimits is { } functionalTestLimits && !IncludesPassValue(functionalTestLimits))
            {
                errors.Add(new ConfigError(
                    $"{path}.testLimits",
                    "functional test limits must include the pass value 1"));
            }

            if (specLimits is { } functionalSpecLimits && !IncludesPassValue(functionalSpecLimits))
            {
                errors.Add(new ConfigError(
                    $"{path}.specLimits",
                    "functional spec limits must include the pass value 1"));
            }

            resolvedUnit = TestItemFactory.PassFailUnit;
            specLimits ??= testLimits ?? TestItemFactory.PassFailSpec;
            testLimits ??= TestItemFactory.PassFailTest;
        }

        if (errors.Count > initialErrorCount)
        {
            return null;
        }

        return new CustomTestItem(
            resolvedTestNumber,
            name,
            resolvedUnit,
            specLimits ?? testLimits!,
            testLimits!,
            resolvedFailSoftBin!.Value,
            kind,
            distribution,
            resolvedPassRate,
            siteOffsets);
    }

    /// <summary>
    /// Site offsets are additive measurement shifts per Tester Site. Only Numeric Test
    /// Items have a measurement value to shift, so Functional items reject them.
    /// </summary>
    private static IReadOnlyList<double>? ResolveSiteOffsets(
        List<double?>? documents,
        string path,
        int siteCount,
        bool isNumeric,
        List<ConfigError> errors)
    {
        if (documents is null)
        {
            return null;
        }

        if (!isNumeric)
        {
            errors.Add(new ConfigError(
                path, "site offsets are only supported for numeric test items"));
            return null;
        }

        if (documents.Count != siteCount)
        {
            errors.Add(new ConfigError(
                path, $"must contain exactly {siteCount} offsets, one per Tester Site"));
            return null;
        }

        var offsets = new double[documents.Count];
        var valid = true;
        for (var index = 0; index < documents.Count; index++)
        {
            if (documents[index] is not double offset || !double.IsFinite(offset))
            {
                errors.Add(new ConfigError($"{path}[{index}]", "must be a finite number"));
                valid = false;
                continue;
            }

            offsets[index] = offset;
        }

        return valid ? offsets : null;
    }

    private static int? ResolveFailSoftBin(
        int? configuredBin, string path, List<ConfigError> errors, bool required = false)
    {
        if (configuredBin is not int bin)
        {
            if (required)
            {
                errors.Add(new ConfigError(path, "required"));
            }

            return null;
        }

        if (!IsValidBinNumber(bin))
        {
            errors.Add(new ConfigError(path, "must be between 1 and 65535"));
            return null;
        }

        return bin;
    }

    private static Distribution? ResolveDistribution(
        DistributionDocument document, string path, List<ConfigError> errors)
    {
        var initialErrorCount = errors.Count;

        var kind = DistributionKind.Normal;
        if (document.Kind is null)
        {
            errors.Add(new ConfigError($"{path}.kind", "required"));
        }
        else if (!TryParseDistributionKind(document.Kind, out kind))
        {
            errors.Add(new ConfigError(
                $"{path}.kind", "must be one of 'normal' or 'lognormal'"));
        }

        if (document.Mean is not double mean)
        {
            errors.Add(new ConfigError($"{path}.mean", "required"));
        }
        else if (!double.IsFinite(mean))
        {
            errors.Add(new ConfigError($"{path}.mean", "must be a finite number"));
        }

        if (document.Sigma is not double sigma)
        {
            errors.Add(new ConfigError($"{path}.sigma", "required"));
        }
        else if (!double.IsFinite(sigma) || sigma <= 0)
        {
            errors.Add(new ConfigError($"{path}.sigma", "must be greater than 0"));
        }

        return errors.Count > initialErrorCount
            ? null
            : new Distribution(kind, document.Mean!.Value, document.Sigma!.Value);
    }

    private static bool TryParseTestItemKind(string value, out TestItemKind kind)
    {
        switch (value)
        {
            case "numeric":
                kind = TestItemKind.Numeric;
                return true;
            case "functional":
                kind = TestItemKind.Functional;
                return true;
            default:
                kind = TestItemKind.Numeric;
                return false;
        }
    }

    private static bool TryParseDistributionKind(string value, out DistributionKind kind)
    {
        switch (value)
        {
            case "normal":
                kind = DistributionKind.Normal;
                return true;
            case "lognormal":
                kind = DistributionKind.Lognormal;
                return true;
            default:
                kind = DistributionKind.Normal;
                return false;
        }
    }

    /// <summary>
    /// Test Item names and units are written to STDF CN fields and tab-separated WAT
    /// rows, so they must fit the STDF text limit and must not break the TSV layout.
    /// </summary>
    private static void ValidateExportedText(string value, string path, List<ConfigError> errors)
    {
        if (Encoding.UTF8.GetByteCount(value) > MaxExportedTextBytes)
        {
            errors.Add(new ConfigError(
                path, $"must be at most {MaxExportedTextBytes} UTF-8 bytes"));
        }

        if (value.Contains('\t') || value.Contains('\n') || value.Contains('\r'))
        {
            errors.Add(new ConfigError(path, "must not contain tab or newline characters"));
        }
    }

    private static bool IncludesPassValue(Limits limits) => limits.Low <= 1 && limits.High >= 1;

    private static bool HasCustomDefinition(
        int? testNumber,
        string? unit,
        string? type,
        DistributionDocument? distribution,
        double? passRate) =>
        testNumber is not null ||
        unit is not null ||
        type is not null ||
        distribution is not null ||
        passRate is not null;

    private static void ValidateCustomTestNumbers(
        IEnumerable<int> catalogTestNumbers,
        IReadOnlyList<CustomItemEntry> customItems,
        List<ConfigError> errors)
    {
        var seen = new HashSet<int>(catalogTestNumbers);
        foreach (var entry in customItems)
        {
            if (!seen.Add(entry.Item.TestNumber))
            {
                errors.Add(new ConfigError(
                    $"{entry.Path}.testNumber",
                    $"duplicate test number {entry.Item.TestNumber}; already used by another test item"));
            }
        }
    }

    /// <summary>A custom Test Item together with the configuration path it came from.</summary>
    private readonly record struct CustomItemEntry(CustomTestItem Item, string Path);

    /// <summary>A CP override together with the configuration path it came from.</summary>
    private readonly record struct CpOverrideEntry(CpTestItemOverride Item, string Path);

    /// <summary>An FT override together with the configuration path it came from.</summary>
    private readonly record struct FtOverrideEntry(FtTestItemOverride Item, string Path);

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
        TestBins bins,
        List<CpOverrideEntry> overrides,
        List<CustomItemEntry> customItems,
        List<ConfigError> errors)
    {
        var overridesByName = new Dictionary<string, CpOverrideEntry>(StringComparer.Ordinal);
        foreach (var entry in overrides)
        {
            overridesByName[entry.Item.Name] = entry;
        }

        var assigned = new Dictionary<int, (string ItemName, string? OverridePath)>();
        foreach (var template in CpCatalog.DefaultItems)
        {
            var overrideEntry = overridesByName.GetValueOrDefault(template.Name);
            var effectiveBin = overrideEntry.Item?.FailSoftBin ?? template.FailSoftBin;
            var overridePath = overrideEntry.Item is null
                ? null
                : $"{overrideEntry.Path}.failSoftBin";
            var reportedOnOverride = overrideEntry.Item?.FailSoftBin is not null;

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
                        : other.OverridePath ?? "cp.testItems",
                    $"duplicate soft bin {effectiveBin}; already used by '{other.ItemName}'"));
                continue;
            }

            assigned[effectiveBin] = (template.Name, reportedOnOverride ? overridePath : null);
        }

        foreach (var entry in customItems)
        {
            var effectiveBin = entry.Item.FailSoftBin;
            if (effectiveBin == bins.PassSoftBin)
            {
                errors.Add(new ConfigError(
                    $"{entry.Path}.failSoftBin",
                    $"conflicts with cp.bins.passSoftBin ({bins.PassSoftBin})"));
                continue;
            }

            if (assigned.TryGetValue(effectiveBin, out var other))
            {
                errors.Add(new ConfigError(
                    $"{entry.Path}.failSoftBin",
                    $"duplicate soft bin {effectiveBin}; already used by '{other.ItemName}'"));
                continue;
            }

            assigned[effectiveBin] = (entry.Item.Name, null);
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
