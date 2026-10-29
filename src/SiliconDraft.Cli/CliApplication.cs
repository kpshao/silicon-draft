using System.Globalization;
using SiliconDraft.Core;

namespace SiliconDraft.Cli;

/// <summary>
/// Thin adapter over <see cref="GenerationPipeline"/>: argument parsing, exit codes,
/// and console output only. All generation behaviour lives in the Core facade.
/// </summary>
public static class CliApplication
{
    public const int ExitSuccess = 0;
    public const int ExitRuntimeError = 1;
    public const int ExitInvalidInput = 2;
    public const int ExitVerificationFailure = 3;

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
        {
            PrintUsage(stderr);
            return ExitInvalidInput;
        }

        if (IsHelp(args[0]))
        {
            PrintUsage(stdout);
            return ExitSuccess;
        }

        return args[0] switch
        {
            "generate" => RunGenerate(args[1..], stdout, stderr),
            "preview" => RunPreview(args[1..], stdout, stderr),
            _ => UnknownCommand(args[0], stderr),
        };
    }

    private static int UnknownCommand(string command, TextWriter stderr)
    {
        stderr.WriteLine($"error: unknown command '{command}'");
        PrintUsage(stderr);
        return ExitInvalidInput;
    }

    private static int RunGenerate(string[] args, TextWriter stdout, TextWriter stderr)
    {
        string? configPath = null;
        string? outputDirectory = null;
        var overwrite = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "-c" or "--config":
                    if (!TryReadValue(args, ref index, "--config", stderr, out configPath))
                    {
                        return ExitInvalidInput;
                    }

                    break;
                case "-o" or "--output":
                    if (!TryReadValue(args, ref index, "--output", stderr, out outputDirectory))
                    {
                        return ExitInvalidInput;
                    }

                    break;
                case "--overwrite":
                    overwrite = true;
                    break;
                case "-h" or "--help":
                    PrintUsage(stdout);
                    return ExitSuccess;
                default:
                    stderr.WriteLine($"error: unknown option '{args[index]}'");
                    PrintUsage(stderr);
                    return ExitInvalidInput;
            }
        }

        if (configPath is null)
        {
            stderr.WriteLine("error: missing required option --config");
            PrintUsage(stderr);
            return ExitInvalidInput;
        }

        try
        {
            var pipeline = new GenerationPipeline();
            if (!TryLoadConfig(pipeline, configPath, stderr, out var config, out var exitCode))
            {
                return exitCode;
            }

            var dataset = pipeline.Generate(config!);
            var destination = Path.GetFullPath(
                outputDirectory ??
                ConfigPaths.ResolveOutputDirectory(config!.Output.Directory, configPath));
            var export = pipeline.Export(dataset, destination, overwrite);
            if (!export.Success)
            {
                stderr.WriteLine($"error: {export.Error}");
                return ExitRuntimeError;
            }

            stdout.WriteLine(
                $"Generated dataset '{dataset.ProductName}' ({Quantity(dataset.LotCount, "lot")}, {Quantity(dataset.WaferCount, "wafer")})");
            stdout.WriteLine($"Manifest: {export.ManifestPath}");
            foreach (var waferMapPath in export.WaferMapPaths)
            {
                stdout.WriteLine($"Wafer map: {waferMapPath}");
            }

            return ExitSuccess;
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return ExitRuntimeError;
        }
    }

    private static int RunPreview(string[] args, TextWriter stdout, TextWriter stderr)
    {
        string? configPath = null;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "-c" or "--config":
                    if (!TryReadValue(args, ref index, "--config", stderr, out configPath))
                    {
                        return ExitInvalidInput;
                    }

                    break;
                case "-h" or "--help":
                    PrintUsage(stdout);
                    return ExitSuccess;
                default:
                    stderr.WriteLine($"error: unknown option '{args[index]}'");
                    PrintUsage(stderr);
                    return ExitInvalidInput;
            }
        }

        if (configPath is null)
        {
            stderr.WriteLine("error: missing required option --config");
            PrintUsage(stderr);
            return ExitInvalidInput;
        }

        try
        {
            var pipeline = new GenerationPipeline();
            if (!TryLoadConfig(pipeline, configPath, stderr, out var config, out var exitCode))
            {
                return exitCode;
            }

            PrintPreview(pipeline.Generate(config!), stdout);
            return ExitSuccess;
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return ExitRuntimeError;
        }
    }

    private static bool TryLoadConfig(
        GenerationPipeline pipeline,
        string configPath,
        TextWriter stderr,
        out ResolvedConfig? config,
        out int exitCode)
    {
        var validation = pipeline.Validate(configPath);
        if (validation.IsValid)
        {
            config = validation.Config;
            exitCode = ExitSuccess;
            return true;
        }

        var unreadable = validation.FailureKind == ConfigFailureKind.UnreadableFile;
        stderr.WriteLine(unreadable
            ? $"error: cannot read configuration: {configPath}"
            : $"error: configuration is invalid: {configPath}");
        foreach (var error in validation.Errors)
        {
            stderr.WriteLine($"  {error}");
        }

        config = null;
        exitCode = unreadable ? ExitRuntimeError : ExitInvalidInput;
        return false;
    }

    private static void PrintPreview(Dataset dataset, TextWriter writer)
    {
        var geometry = dataset.Config.Wafer;
        var map = dataset.Lots[0].Wafers[0].DieMap;

        writer.WriteLine($"Product: {dataset.ProductName}");
        writer.WriteLine($"Lots: {dataset.LotCount}");
        writer.WriteLine($"Wafers: {dataset.WaferCount}");
        writer.WriteLine();
        writer.WriteLine("Wafer geometry (per wafer):");
        writer.WriteLine($"  Diameter: {Number(geometry.DiameterMm)} mm");
        writer.WriteLine($"  Edge exclusion: {Number(geometry.EdgeExclusionMm)} mm");
        writer.WriteLine($"  Notch angle: {Number(geometry.NotchAngleDeg)}°");
        writer.WriteLine($"  Die size: {Number(geometry.DieWidthMm)} × {Number(geometry.DieHeightMm)} mm");
        writer.WriteLine($"  Scribe lane: {Number(geometry.ScribeMm)} mm");
        writer.WriteLine(
            $"  Die pitch: {Number(geometry.PitchWidthMm)} × {Number(geometry.PitchHeightMm)} mm");
        writer.WriteLine(
            $"  Grid offset: {Number(geometry.GridOffsetXMm)}, {Number(geometry.GridOffsetYMm)} mm");
        writer.WriteLine($"  Rotation: {geometry.RotationDeg}°");
        writer.WriteLine($"  Mirror: {MirrorLabel(geometry)}");
        writer.WriteLine($"  Die index origin: {IndexOriginLabel(geometry.IndexOrigin)}");
        writer.WriteLine($"  Partial die policy: {PartialDiePolicyLabel(geometry.PartialDiePolicy)}");
        writer.WriteLine();
        writer.WriteLine("Die map:");
        writer.WriteLine(
            $"  Gross die: {map.GrossDie} per wafer, {map.GrossDie * dataset.WaferCount} total");
        writer.WriteLine(
            $"  Net die: {map.NetDie} per wafer, {map.NetDie * dataset.WaferCount} total");
        writer.WriteLine(
            $"  Partial die: {map.PartialDieFound} per wafer, {map.PartialDieFound * dataset.WaferCount} total (retained: {map.PartialDieRetained})");
    }

    private static string MirrorLabel(WaferGeometry geometry) => (geometry.MirrorX, geometry.MirrorY) switch
    {
        (true, true) => "X and Y",
        (true, false) => "X",
        (false, true) => "Y",
        _ => "none",
    };

    private static string IndexOriginLabel(DieIndexOrigin indexOrigin) => indexOrigin switch
    {
        DieIndexOrigin.Center => "center",
        DieIndexOrigin.BottomLeft => "bottomLeft",
        DieIndexOrigin.TopLeft => "topLeft",
        DieIndexOrigin.TopRight => "topRight",
        DieIndexOrigin.BottomRight => "bottomRight",
        _ => throw new ArgumentOutOfRangeException(nameof(indexOrigin)),
    };

    private static string PartialDiePolicyLabel(PartialDiePolicy policy) => policy switch
    {
        PartialDiePolicy.Exclude => "exclude",
        PartialDiePolicy.Retain => "retain",
        _ => throw new ArgumentOutOfRangeException(nameof(policy)),
    };

    private static string Number(double value) =>
        value.ToString("0.####", CultureInfo.InvariantCulture);

    private static bool TryReadValue(
        string[] args, ref int index, string option, TextWriter stderr, out string? value)
    {
        if (index + 1 >= args.Length)
        {
            stderr.WriteLine($"error: option '{option}' requires a value");
            value = null;
            return false;
        }

        value = args[++index];
        return true;
    }

    private static string Quantity(int count, string noun) =>
        count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    private static bool IsHelp(string argument) => argument is "-h" or "--help";

    private static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("SiliconDraft generates synthetic CP/FT/WAT datasets with known ground truth.");
        writer.WriteLine();
        writer.WriteLine("Usage:");
        writer.WriteLine("  silicondraft generate -c <config> [-o <directory>] [--overwrite]");
        writer.WriteLine("  silicondraft preview -c <config>");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  -c, --config <path>     Versioned JSON configuration file (required)");
        writer.WriteLine("  -o, --output <dir>      Override the configured output directory (generate only)");
        writer.WriteLine("      --overwrite         Replace existing output files (generate only)");
        writer.WriteLine("  -h, --help              Show this help");
        writer.WriteLine();
        writer.WriteLine("Relative output directories in the configuration are resolved against the configuration file.");
        writer.WriteLine();
        writer.WriteLine("Exit codes:");
        writer.WriteLine("  0  success");
        writer.WriteLine("  1  runtime error");
        writer.WriteLine("  2  invalid configuration or arguments");
        writer.WriteLine("  3  verification failure");
    }
}
