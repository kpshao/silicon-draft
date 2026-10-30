using SiliconDraft.TestSupport;

namespace SiliconDraft.Core.Tests;

public sealed class FtConfigValidationTests
{
    [Fact]
    public void Validate_WithoutFtSection_UsesDefaultAssemblyLossAndEscapeRate()
    {
        using var workspace = new TestWorkspace();
        var result = new GenerationPipeline().Validate(workspace.WriteConfig(TestConfigs.Valid));

        Assert.True(result.IsValid);
        var ft = result.Config!.Ft;
        Assert.Equal(0, ft.AssemblyLoss);
        Assert.Equal(0, ft.EscapeRate);
    }

    [Fact]
    public void Validate_WithFtSection_RecordsEffectiveValues()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(
            TestConfigs.WithFt("""{ "assemblyLoss": 3, "escapeRate": 0.25 }"""));

        var result = new GenerationPipeline().Validate(configPath);

        Assert.True(result.IsValid);
        var ft = result.Config!.Ft;
        Assert.Equal(3, ft.AssemblyLoss);
        Assert.Equal(0.25, ft.EscapeRate);
    }

    [Fact]
    public void Validate_WithNegativeAssemblyLoss_RejectsTheConfig()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(
            TestConfigs.WithFt("""{ "assemblyLoss": -1 }"""));

        var result = new GenerationPipeline().Validate(configPath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Path == "ft.assemblyLoss");
    }

    [Theory]
    [InlineData("-0.1")]
    [InlineData("1.5")]
    public void Validate_WithEscapeRateOutsideZeroToOne_RejectsTheConfig(string escapeRate)
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(
            TestConfigs.WithFt($$"""{ "escapeRate": {{escapeRate}} }"""));

        var result = new GenerationPipeline().Validate(configPath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Path == "ft.escapeRate");
    }

    [Fact]
    public void Validate_WithNonIntegerAssemblyLoss_RejectsTheConfig()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(
            TestConfigs.WithFt("""{ "assemblyLoss": 1.5 }"""));

        var result = new GenerationPipeline().Validate(configPath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Path.Contains("assemblyLoss"));
    }

    [Fact]
    public void Validate_WithUnknownFtProperty_RejectsTheConfig()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(
            TestConfigs.WithFt("""{ "assemblyLossRate": 0.1 }"""));

        var result = new GenerationPipeline().Validate(configPath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Path.Contains("assemblyLossRate"));
    }
}
