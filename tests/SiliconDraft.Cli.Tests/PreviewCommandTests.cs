using SiliconDraft.TestSupport;

namespace SiliconDraft.Cli.Tests;

public sealed class PreviewCommandTests
{
    [Fact]
    public void Preview_WithDefaultGeometry_PrintsGeometryAndCountsWithoutWritingFiles()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(TestConfigs.Valid);
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliApplication.Run(["preview", "-c", configPath], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());
        var output = stdout.ToString();
        Assert.Contains("Product: DemoProduct", output);
        Assert.Contains("Lots: 1", output);
        Assert.Contains("Wafers: 1", output);
        Assert.Contains("Diameter: 300 mm", output);
        Assert.Contains("Edge exclusion: 3 mm", output);
        Assert.Contains("Notch angle: 270", output);
        Assert.Contains("Die size: 5 × 5 mm", output);
        Assert.Contains("Die pitch: 5.08 × 5.08 mm", output);
        Assert.Contains("Grid offset: 0, 0 mm", output);
        Assert.Contains("Rotation: 0°", output);
        Assert.Contains("Mirror: none", output);
        Assert.Contains("Die index origin: center", output);
        Assert.Contains("Partial die policy: exclude", output);
        Assert.Contains("Gross die: 2517 per wafer, 2517 total", output);
        Assert.Contains("Net die: 2517 per wafer, 2517 total", output);
        Assert.Contains("Partial die: 336 per wafer, 336 total (retained: 0)", output);
        Assert.False(Directory.Exists(workspace.PathFor("out")));
    }

    [Fact]
    public void Preview_WithCustomGeometry_PrintsConfiguredValues()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(TestConfigs.WithWafer(
            """
            {
              "diameterMm": 100,
              "edgeExclusionMm": 0,
              "die": { "widthMm": 10, "heightMm": 10, "scribeMm": 0 },
              "grid": {
                "rotationDeg": 90,
                "mirrorX": true,
                "mirrorY": true,
                "indexOrigin": "bottomLeft"
              },
              "partialDiePolicy": "retain"
            }
            """));
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliApplication.Run(["preview", "--config", configPath], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());
        var output = stdout.ToString();
        Assert.Contains("Diameter: 100 mm", output);
        Assert.Contains("Edge exclusion: 0 mm", output);
        Assert.Contains("Die pitch: 10 × 10 mm", output);
        Assert.Contains("Rotation: 90°", output);
        Assert.Contains("Mirror: X and Y", output);
        Assert.Contains("Die index origin: bottomLeft", output);
        Assert.Contains("Partial die policy: retain", output);
        Assert.Contains("Gross die: 61 per wafer, 61 total", output);
        Assert.Contains("Net die: 61 per wafer, 61 total", output);
        Assert.Contains("Partial die: 40 per wafer, 40 total (retained: 40)", output);
    }

    [Fact]
    public void Preview_WithSeveralWafers_ReportsPerWaferAndTotalCounts()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(TestConfigs.WithWafer(
            """
            {
              "diameterMm": 100,
              "edgeExclusionMm": 0,
              "die": { "widthMm": 10, "heightMm": 10, "scribeMm": 0 }
            }
            """,
            """[ { "id": "LOT-0001", "waferCount": 2 } ]"""));
        var stdout = new StringWriter();

        var exitCode = CliApplication.Run(["preview", "-c", configPath], stdout, new StringWriter());

        Assert.Equal(0, exitCode);
        var output = stdout.ToString();
        Assert.Contains("Wafers: 2", output);
        Assert.Contains("Gross die: 61 per wafer, 122 total", output);
        Assert.Contains("Net die: 61 per wafer, 122 total", output);
    }

    [Fact]
    public void Preview_WithInvalidGeometry_ReturnsTwoWithoutWritingFiles()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(TestConfigs.WithWafer(
            """{ "grid": { "rotationDeg": 45 } }"""));
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliApplication.Run(["preview", "-c", configPath], stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.Contains("configuration is invalid", stderr.ToString());
        Assert.Contains("wafer.grid.rotationDeg", stderr.ToString());
        Assert.False(Directory.Exists(workspace.PathFor("out")));
    }

    [Fact]
    public void Preview_WithMissingConfigFile_ReturnsOne()
    {
        using var workspace = new TestWorkspace();
        var stderr = new StringWriter();

        var exitCode = CliApplication.Run(
            ["preview", "-c", workspace.PathFor("missing.json")],
            new StringWriter(),
            stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains("cannot read configuration", stderr.ToString());
    }

    [Fact]
    public void Preview_WithoutConfigOption_ReturnsTwoAndPrintsUsage()
    {
        var stderr = new StringWriter();

        var exitCode = CliApplication.Run(["preview"], new StringWriter(), stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("--config", stderr.ToString());
        Assert.Contains("Usage:", stderr.ToString());
    }

    [Fact]
    public void Preview_WithUnknownOption_ReturnsTwoAndPrintsUsage()
    {
        var stderr = new StringWriter();

        var exitCode = CliApplication.Run(["preview", "--nonsense"], new StringWriter(), stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("--nonsense", stderr.ToString());
        Assert.Contains("Usage:", stderr.ToString());
    }
}
