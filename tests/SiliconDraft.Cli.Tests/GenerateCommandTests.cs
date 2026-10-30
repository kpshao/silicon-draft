using System.Diagnostics;
using SiliconDraft.TestSupport;

namespace SiliconDraft.Cli.Tests;

public sealed class GenerateCommandTests
{
    [Fact]
    public void Generate_WithValidConfig_WritesManifestAndReturnsZero()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(TestConfigs.Valid);
        var outputDirectory = workspace.PathFor("dataset");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliApplication.Run(
            ["generate", "-c", configPath, "-o", outputDirectory], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());
        Assert.True(File.Exists(Path.Combine(outputDirectory, "_truth", "manifest.json")));
        Assert.True(File.Exists(Path.Combine(outputDirectory, "LOT-0001_CP.stdf")));
        Assert.True(File.Exists(Path.Combine(outputDirectory, "LOT-0001_WAT_long.txt")));
        Assert.Contains("manifest.json", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LOT-0001_CP.stdf", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LOT-0001_WAT_long.txt", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_WithInvalidConfig_ReturnsTwoAndWritesNoData()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(
            """
            {
              "schemaVersion": 1,
              "product": { "name": "DemoProduct" },
              "lots": [ { "id": "LOT-0001", "waferCount": 1 } ],
              "output": { "directory": "out" }
            }
            """);
        var outputDirectory = workspace.PathFor("dataset");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliApplication.Run(
            ["generate", "-c", configPath, "-o", outputDirectory], stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.False(Directory.Exists(outputDirectory));
        Assert.Contains("seed: required", stderr.ToString());
        Assert.Contains("configuration is invalid", stderr.ToString());
        Assert.Equal(string.Empty, stdout.ToString());
    }

    [Fact]
    public void Generate_WithMissingConfigFile_ReturnsOne()
    {
        using var workspace = new TestWorkspace();
        var missingPath = workspace.PathFor("missing.json");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliApplication.Run(
            ["generate", "-c", missingPath, "-o", workspace.PathFor("dataset")], stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains("cannot read configuration", stderr.ToString());
        Assert.Contains("missing.json", stderr.ToString());
        Assert.False(Directory.Exists(workspace.PathFor("dataset")));
    }

    [Fact]
    public void Generate_WithoutConfigOption_ReturnsTwoAndPrintsUsage()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliApplication.Run(["generate"], stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("--config", stderr.ToString());
        Assert.Contains("Usage:", stderr.ToString());
    }

    [Fact]
    public void Generate_WithUnknownOption_ReturnsTwoAndPrintsUsage()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliApplication.Run(["generate", "--nonsense"], stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("--nonsense", stderr.ToString());
        Assert.Contains("Usage:", stderr.ToString());
    }

    [Fact]
    public void Generate_WithoutOutputOption_UsesConfiguredDirectoryRelativeToConfigFile()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(TestConfigs.Valid);
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliApplication.Run(["generate", "-c", configPath], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(workspace.PathFor(Path.Combine("out", "_truth", "manifest.json"))));
    }

    [Fact]
    public void Generate_SameConfigAndSeed_WritesIdenticalManifest()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(TestConfigs.Valid);
        var firstOutput = workspace.PathFor("run-a");
        var secondOutput = workspace.PathFor("run-b");

        Assert.Equal(0, CliApplication.Run(
            ["generate", "-c", configPath, "-o", firstOutput], new StringWriter(), new StringWriter()));
        Assert.Equal(0, CliApplication.Run(
            ["generate", "-c", configPath, "-o", secondOutput], new StringWriter(), new StringWriter()));

        var firstManifest = File.ReadAllBytes(Path.Combine(firstOutput, "_truth", "manifest.json"));
        var secondManifest = File.ReadAllBytes(Path.Combine(secondOutput, "_truth", "manifest.json"));
        Assert.Equal(firstManifest, secondManifest);
    }

    [Fact]
    public void Generate_WhenOutputExists_ReturnsOneUnlessOverwriteIsGiven()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(TestConfigs.Valid);
        var outputDirectory = workspace.PathFor("dataset");
        Assert.Equal(0, CliApplication.Run(
            ["generate", "-c", configPath, "-o", outputDirectory], new StringWriter(), new StringWriter()));
        var stderr = new StringWriter();

        var exitCode = CliApplication.Run(
            ["generate", "-c", configPath, "-o", outputDirectory], new StringWriter(), stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains("already exists", stderr.ToString());

        var overwriteExitCode = CliApplication.Run(
            ["generate", "-c", configPath, "-o", outputDirectory, "--overwrite"],
            new StringWriter(),
            new StringWriter());
        Assert.Equal(0, overwriteExitCode);
    }

    [Fact]
    public void BuiltExecutable_Generate_WritesManifestAndExitsZero()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(TestConfigs.Valid);
        var outputDirectory = workspace.PathFor("dataset");
        var executableName = OperatingSystem.IsWindows() ? "silicondraft.exe" : "silicondraft";
        var executable = Path.Combine(AppContext.BaseDirectory, executableName);

        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("generate");
        startInfo.ArgumentList.Add("--config");
        startInfo.ArgumentList.Add(configPath);
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputDirectory);

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.Equal(0, process.ExitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Contains("Generated dataset", stdout);
        Assert.True(File.Exists(Path.Combine(outputDirectory, "_truth", "manifest.json")));
    }
}
