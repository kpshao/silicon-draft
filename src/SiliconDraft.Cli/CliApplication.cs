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

        if (args[0] != "generate")
        {
            stderr.WriteLine($"error: unknown command '{args[0]}'");
            PrintUsage(stderr);
            return ExitInvalidInput;
        }

        return RunGenerate(args[1..], stdout, stderr);
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
            var validation = pipeline.Validate(configPath);
            if (!validation.IsValid)
            {
                var unreadable = validation.FailureKind == ConfigFailureKind.UnreadableFile;
                stderr.WriteLine(unreadable
                    ? $"error: cannot read configuration: {configPath}"
                    : $"error: configuration is invalid: {configPath}");
                foreach (var error in validation.Errors)
                {
                    stderr.WriteLine($"  {error}");
                }

                return unreadable ? ExitRuntimeError : ExitInvalidInput;
            }

            var dataset = pipeline.Generate(validation.Config!);
            var destination = Path.GetFullPath(
                outputDirectory ??
                ConfigPaths.ResolveOutputDirectory(validation.Config!.Output.Directory, configPath));
            var export = pipeline.Export(dataset, destination, overwrite);
            if (!export.Success)
            {
                stderr.WriteLine($"error: {export.Error}");
                return ExitRuntimeError;
            }

            stdout.WriteLine(
                $"Generated dataset '{dataset.ProductName}' ({Quantity(dataset.LotCount, "lot")}, {Quantity(dataset.WaferCount, "wafer")})");
            stdout.WriteLine($"Manifest: {export.ManifestPath}");
            return ExitSuccess;
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return ExitRuntimeError;
        }
    }

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
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  -c, --config <path>     Versioned JSON configuration file (required)");
        writer.WriteLine("  -o, --output <dir>      Override the configured output directory");
        writer.WriteLine("      --overwrite         Replace existing output files");
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
