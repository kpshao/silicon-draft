using SiliconDraft.TestSupport;

namespace SiliconDraft.Core.Tests;

public sealed class WaferGeometryConfigTests
{
    [Fact]
    public void Validate_WithoutWaferSection_AppliesDefaultGeometry()
    {
        using var workspace = new TestWorkspace();
        var result = new GenerationPipeline().Validate(workspace.WriteConfig(TestConfigs.Valid));

        Assert.True(result.IsValid);
        var geometry = result.Config!.Wafer;
        Assert.Equal(300, geometry.DiameterMm);
        Assert.Equal(3, geometry.EdgeExclusionMm);
        Assert.Equal(270, geometry.NotchAngleDeg);
        Assert.Equal(5, geometry.DieWidthMm);
        Assert.Equal(5, geometry.DieHeightMm);
        Assert.Equal(0.08, geometry.ScribeMm);
        Assert.Equal(5.08, geometry.PitchWidthMm, 6);
        Assert.Equal(5.08, geometry.PitchHeightMm, 6);
        Assert.Equal(0, geometry.GridOffsetXMm);
        Assert.Equal(0, geometry.GridOffsetYMm);
        Assert.Equal(0, geometry.RotationDeg);
        Assert.False(geometry.MirrorX);
        Assert.False(geometry.MirrorY);
        Assert.Equal(DieIndexOrigin.Center, geometry.IndexOrigin);
        Assert.Equal(PartialDiePolicy.Exclude, geometry.PartialDiePolicy);
    }

    [Fact]
    public void Validate_WithWaferSection_ResolvesConfiguredGeometry()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(TestConfigs.WithWafer(
            """
            {
              "diameterMm": 200,
              "edgeExclusionMm": 2.5,
              "notchAngleDeg": 90,
              "die": {
                "widthMm": 10,
                "heightMm": 8,
                "scribeMm": 0.1,
                "pitch": { "widthMm": 10.2, "heightMm": 8.3 }
              },
              "grid": {
                "offsetMm": { "x": 1.5, "y": -2.5 },
                "rotationDeg": 90,
                "mirrorX": true,
                "mirrorY": false,
                "indexOrigin": "bottomLeft"
              },
              "partialDiePolicy": "retain"
            }
            """));

        var result = new GenerationPipeline().Validate(configPath);

        Assert.True(result.IsValid);
        var geometry = result.Config!.Wafer;
        Assert.Equal(200, geometry.DiameterMm);
        Assert.Equal(2.5, geometry.EdgeExclusionMm);
        Assert.Equal(90, geometry.NotchAngleDeg);
        Assert.Equal(10, geometry.DieWidthMm);
        Assert.Equal(8, geometry.DieHeightMm);
        Assert.Equal(0.1, geometry.ScribeMm);
        Assert.Equal(10.2, geometry.PitchWidthMm, 6);
        Assert.Equal(8.3, geometry.PitchHeightMm, 6);
        Assert.Equal(1.5, geometry.GridOffsetXMm);
        Assert.Equal(-2.5, geometry.GridOffsetYMm);
        Assert.Equal(90, geometry.RotationDeg);
        Assert.True(geometry.MirrorX);
        Assert.False(geometry.MirrorY);
        Assert.Equal(DieIndexOrigin.BottomLeft, geometry.IndexOrigin);
        Assert.Equal(PartialDiePolicy.Retain, geometry.PartialDiePolicy);
    }

    [Fact]
    public void Validate_WithPartialWaferSection_MixesConfiguredValuesWithDefaults()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(TestConfigs.WithWafer(
            """
            {
              "diameterMm": 200,
              "die": { "widthMm": 8 }
            }
            """));

        var result = new GenerationPipeline().Validate(configPath);

        Assert.True(result.IsValid);
        var geometry = result.Config!.Wafer;
        Assert.Equal(200, geometry.DiameterMm);
        Assert.Equal(3, geometry.EdgeExclusionMm);
        Assert.Equal(8, geometry.DieWidthMm);
        Assert.Equal(5, geometry.DieHeightMm);
        Assert.Equal(8.08, geometry.PitchWidthMm, 6);
        Assert.Equal(5.08, geometry.PitchHeightMm, 6);
    }

    [Theory]
    [InlineData("""{ "diameterMm": 0 }""", "wafer.diameterMm", "greater than 0")]
    [InlineData("""{ "diameterMm": -300 }""", "wafer.diameterMm", "greater than 0")]
    [InlineData("""{ "edgeExclusionMm": -1 }""", "wafer.edgeExclusionMm", "0 or greater")]
    [InlineData("""{ "diameterMm": 300, "edgeExclusionMm": 150 }""", "wafer.edgeExclusionMm", "less than")]
    [InlineData("""{ "notchAngleDeg": 360 }""", "wafer.notchAngleDeg", "less than 360")]
    [InlineData("""{ "notchAngleDeg": -90 }""", "wafer.notchAngleDeg", "0 or greater")]
    [InlineData("""{ "die": { "widthMm": 0 } }""", "wafer.die.widthMm", "greater than 0")]
    [InlineData("""{ "die": { "heightMm": -5 } }""", "wafer.die.heightMm", "greater than 0")]
    [InlineData("""{ "die": { "scribeMm": -0.01 } }""", "wafer.die.scribeMm", "0 or greater")]
    [InlineData("""{ "die": { "widthMm": 5, "pitch": { "widthMm": 4, "heightMm": 5 } } }""", "wafer.die.pitch.widthMm", "at least")]
    [InlineData("""{ "die": { "pitch": { "widthMm": 5 } } }""", "wafer.die.pitch.heightMm", "required")]
    [InlineData("""{ "die": { "pitch": { "heightMm": 5 } } }""", "wafer.die.pitch.widthMm", "required")]
    [InlineData("""{ "grid": { "rotationDeg": 45 } }""", "wafer.grid.rotationDeg", "0, 90, 180")]
    [InlineData("""{ "grid": { "indexOrigin": "middle" } }""", "wafer.grid.indexOrigin", "bottomLeft")]
    [InlineData("""{ "partialDiePolicy": "keep" }""", "wafer.partialDiePolicy", "retain")]
    public void Validate_WithInvalidGeometry_ReportsActionableError(
        string waferJson, string expectedPath, string expectedMessagePart)
    {
        using var workspace = new TestWorkspace();
        var result = new GenerationPipeline().Validate(
            workspace.WriteConfig(TestConfigs.WithWafer(waferJson)));

        Assert.False(result.IsValid);
        Assert.Null(result.Config);
        Assert.Equal(ConfigFailureKind.InvalidConfiguration, result.FailureKind);
        var error = Assert.Single(result.Errors, error => error.Path == expectedPath);
        Assert.Contains(expectedMessagePart, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WithGeometryThatProducesNoValidDie_IsRejected()
    {
        using var workspace = new TestWorkspace();
        var result = new GenerationPipeline().Validate(workspace.WriteConfig(TestConfigs.WithWafer(
            """
            {
              "diameterMm": 100,
              "edgeExclusionMm": 45,
              "die": { "widthMm": 10, "heightMm": 10, "scribeMm": 0 }
            }
            """)));

        Assert.False(result.IsValid);
        Assert.Equal(ConfigFailureKind.InvalidConfiguration, result.FailureKind);
        var error = Assert.Single(result.Errors, error => error.Path == "wafer");
        Assert.Contains("no valid die", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WithOffsetFarOutsideTheWafer_IsRejectedAsHavingNoValidDie()
    {
        using var workspace = new TestWorkspace();
        var result = new GenerationPipeline().Validate(workspace.WriteConfig(TestConfigs.WithWafer(
            """{ "grid": { "offsetMm": { "x": 1e300, "y": 0 } } }""")));

        Assert.False(result.IsValid);
        Assert.Equal(ConfigFailureKind.InvalidConfiguration, result.FailureKind);
        var error = Assert.Single(result.Errors, error => error.Path == "wafer");
        Assert.Contains("no valid die", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WithImpossiblyDenseDieGrid_IsRejected()
    {
        using var workspace = new TestWorkspace();
        var result = new GenerationPipeline().Validate(workspace.WriteConfig(TestConfigs.WithWafer(
            """{ "die": { "widthMm": 0.2, "heightMm": 0.2, "scribeMm": 0 } }""")));

        Assert.False(result.IsValid);
        Assert.Equal(ConfigFailureKind.InvalidConfiguration, result.FailureKind);
        var error = Assert.Single(result.Errors, error => error.Path == "wafer");
        Assert.Contains("die positions", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
