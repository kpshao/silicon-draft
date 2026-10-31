using SiliconDraft.TestSupport;

namespace SiliconDraft.Core.Tests;

public sealed class CpConfigValidationTests
{
    [Fact]
    public void Validate_WithPassSoftBinEqualToAnFtFailSoftBin_RejectsTheConfig()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(
            TestConfigs.WithCp("""{ "bins": { "passSoftBin": 300 } }"""));

        var result = new GenerationPipeline().Validate(configPath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Path == "cp.bins.passSoftBin");
    }

    [Fact]
    public void Validate_WithoutCpSection_UsesDefaultBinsSitesAndCatalog()
    {
        using var workspace = new TestWorkspace();
        var result = new GenerationPipeline().Validate(workspace.WriteConfig(TestConfigs.Valid));

        Assert.True(result.IsValid);
        var cp = result.Config!.Cp;
        Assert.Equal(1, cp.Bins.PassHardBin);
        Assert.Equal(1, cp.Bins.PassSoftBin);
        Assert.Equal(2, cp.Bins.ParametricFailHardBin);
        Assert.Equal(3, cp.Bins.FunctionalFailHardBin);
        Assert.Equal(8, cp.SiteCount);
        Assert.Empty(cp.TestItems);
    }

    [Fact]
    public void Validate_WithSiteCount_RecordsTheEffectiveValue()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(
            TestConfigs.WithCp("""{ "siteCount": 4 }"""));

        var result = new GenerationPipeline().Validate(configPath);

        Assert.True(result.IsValid);
        Assert.Equal(4, result.Config!.Cp.SiteCount);
    }

    [Theory]
    [InlineData("""{ "siteCount": 0 }""")]
    [InlineData("""{ "siteCount": 255 }""")]
    [InlineData("""{ "siteCount": 300 }""")]
    public void Validate_WithSiteCountOutOfRange_IsRejected(string cpJson)
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(TestConfigs.WithCp(cpJson));

        var result = new GenerationPipeline().Validate(configPath);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors, error => error.Path == "cp.siteCount");
        Assert.Contains("between 1 and 254", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WithSiteOffsets_RecordsTheEffectiveValues()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(TestConfigs.WithCp(
            """
            {
              "siteCount": 2,
              "testItems": [
                { "name": "Vt", "siteOffsets": [-0.001, 0.002] }
              ]
            }
            """));

        var result = new GenerationPipeline().Validate(configPath);

        Assert.True(result.IsValid);
        var item = Assert.Single(result.Config!.Cp.TestItems);
        Assert.Equal(new[] { -0.001, 0.002 }, item.SiteOffsets);
    }

    [Theory]
    [InlineData(
        """{ "siteCount": 4, "testItems": [ { "name": "Vt", "siteOffsets": [0, 1, 2] } ] }""",
        "cp.testItems[0].siteOffsets",
        "exactly 4")]
    [InlineData(
        """{ "siteCount": 4, "testItems": [ { "name": "Vt", "siteOffsets": [0, null, 1, 2] } ] }""",
        "cp.testItems[0].siteOffsets[1]",
        "finite")]
    [InlineData(
        """
        { "testItems": [ { "name": "Scan", "siteOffsets": [0, 0, 0, 0, 0, 0, 0, 0] } ] }
        """,
        "cp.testItems[0].siteOffsets",
        "numeric")]
    public void Validate_WithInvalidSiteOffsets_IsRejected(
        string cpJson, string expectedPath, string expectedMessagePart)
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(TestConfigs.WithCp(cpJson));

        var result = new GenerationPipeline().Validate(configPath);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors, error => error.Path == expectedPath);
        Assert.Contains(expectedMessagePart, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WithCpOverrides_RecordsEffectiveValues()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(TestConfigs.WithCp(
            """
            {
              "bins": {
                "passHardBin": 10,
                "passSoftBin": 11,
                "parametricFailHardBin": 12,
                "functionalFailHardBin": 13
              },
              "testItems": [
                {
                  "name": "Vt",
                  "specLimits": { "low": 0.41, "high": 0.49 },
                  "testLimits": { "low": 0.4, "high": 0.5 },
                  "failSoftBin": 130
                }
              ]
            }
            """));

        var result = new GenerationPipeline().Validate(configPath);

        Assert.True(result.IsValid);
        var cp = result.Config!.Cp;
        Assert.Equal(10, cp.Bins.PassHardBin);
        Assert.Equal(11, cp.Bins.PassSoftBin);
        Assert.Equal(12, cp.Bins.ParametricFailHardBin);
        Assert.Equal(13, cp.Bins.FunctionalFailHardBin);
        var item = Assert.Single(cp.TestItems);
        Assert.Equal("Vt", item.Name);
        Assert.Equal(new Limits(0.41, 0.49), item.SpecLimits);
        Assert.Equal(new Limits(0.4, 0.5), item.TestLimits);
        Assert.Equal(130, item.FailSoftBin);
    }

    [Theory]
    [InlineData(
        """{ "testItems": [ { "name": "Vt", "specLimits": { "low": 0.5, "high": 0.4 } } ] }""",
        "cp.testItems[0].specLimits",
        "less than")]
    [InlineData(
        """{ "testItems": [ { "name": "Vt", "testLimits": { "low": 0.4, "high": 0.4 } } ] }""",
        "cp.testItems[0].testLimits",
        "less than")]
    [InlineData(
        """{ "testItems": [ { "name": "Vt", "specLimits": { "low": 0.4 } } ] }""",
        "cp.testItems[0].specLimits.high",
        "required")]
    [InlineData(
        """{ "testItems": [ { "name": "Vt", "failSoftBin": 0 } ] }""",
        "cp.testItems[0].failSoftBin",
        "between 1 and 65535")]
    [InlineData(
        """
        { "testItems": [
            { "name": "Vt", "failSoftBin": 120 },
            { "name": "Vt", "failSoftBin": 121 }
        ] }
        """,
        "cp.testItems[1].name",
        "duplicate")]
    [InlineData(
        """{ "bins": { "passHardBin": 0 } }""",
        "cp.bins.passHardBin",
        "between 1 and 65535")]
    [InlineData(
        """{ "bins": { "parametricFailHardBin": 1 } }""",
        "cp.bins.parametricFailHardBin",
        "distinct")]
    [InlineData(
        """{ "bins": { "functionalFailHardBin": 2 } }""",
        "cp.bins.functionalFailHardBin",
        "distinct")]
    [InlineData(
        """{ "bins": { "passSoftBin": 100 } }""",
        "cp.bins.passSoftBin",
        "conflicts")]
    [InlineData(
        """{ "testItems": [ { "name": "Vt", "failSoftBin": 101 } ] }""",
        "cp.testItems[0].failSoftBin",
        "duplicate")]
    public void Validate_WithInvalidCpDefinition_IsRejected(
        string cpJson, string expectedPath, string expectedMessagePart)
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(TestConfigs.WithCp(cpJson));

        var result = new GenerationPipeline().Validate(configPath);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors, error => error.Path == expectedPath);
        Assert.Contains(expectedMessagePart, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(
        """{ "bins": { "passHardBin": 1, "passSoftBin": 1 } }""")]
    [InlineData(
        """{ "testItems": [ { "name": "Vt", "failSoftBin": 120 } ] }""")]
    public void Validate_WithValidCpDefinition_IsAccepted(string cpJson)
    {
        using var workspace = new TestWorkspace();
        var result = new GenerationPipeline().Validate(workspace.WriteConfig(TestConfigs.WithCp(cpJson)));

        Assert.True(result.IsValid);
    }
}
