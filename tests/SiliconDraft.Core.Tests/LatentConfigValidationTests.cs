using SiliconDraft.TestSupport;

namespace SiliconDraft.Core.Tests;

public sealed class LatentConfigValidationTests
{
    [Fact]
    public void Validate_WithoutLatentSection_ResolvesTheDefaultModel()
    {
        var result = Validate(TestConfigs.Valid);

        Assert.True(result.IsValid);
        var latent = result.Config!.Latent;
        Assert.Equal(new VarianceComponents(0.03, 0.05, 0.05, 0.72, 0.15), latent.Variance);
        Assert.Equal(
            new[]
            {
                new DieCorrelationPair("Idsat", "Ioh", 0.70, 0.05),
                new DieCorrelationPair("Vt", "Fmax", -0.25, 0.05),
                new DieCorrelationPair("Cin", "Cin", 0.80, 0.05),
                new DieCorrelationPair("Leakage", "Iil", 0.65, 0.05),
                new DieCorrelationPair("Iddq", "Icc", 0.60, 0.05),
            },
            latent.DiePairs);
    }

    [Fact]
    public void Validate_WithConfiguredVarianceAndPairs_RecordsTheEffectiveSettings()
    {
        var result = Validate(TestConfigs.WithLatent(
            """
            {
              "variance": {
                "lot": 0.1,
                "wafer": 0.2,
                "spatial": 0.1,
                "die": 0.3,
                "measurement": 0.3
              },
              "diePairs": [
                { "cpItem": "Vt", "ftItem": "Voh", "targetR": 0.4, "tolerance": 0.08 }
              ]
            }
            """));

        Assert.True(result.IsValid);
        var latent = result.Config!.Latent;
        Assert.Equal(new VarianceComponents(0.1, 0.2, 0.1, 0.3, 0.3), latent.Variance);
        var pair = Assert.Single(latent.DiePairs);
        Assert.Equal("Vt", pair.CpItem);
        Assert.Equal("Voh", pair.FtItem);
        Assert.Equal(0.4, pair.TargetR);
        Assert.Equal(0.08, pair.Tolerance);
    }

    [Fact]
    public void Validate_WithoutSpatialLoadings_HasNoOverrides()
    {
        var result = Validate(TestConfigs.Valid);

        Assert.True(result.IsValid);
        Assert.Empty(result.Config!.Latent.SpatialLoadings);
    }

    [Fact]
    public void Validate_WithSpatialLoading_RecordsTheStepItemAndLoading()
    {
        var result = Validate(TestConfigs.WithLatent(
            """
            {
              "spatialLoadings": [
                { "step": "cp", "name": "Vt", "loading": 1.5 },
                { "step": "ft", "name": "Fmax", "loading": 0.5 }
              ]
            }
            """));

        Assert.True(result.IsValid);
        Assert.Equal(
            new[]
            {
                new SpatialLoading(TestStep.Cp, "Vt", 1.5),
                new SpatialLoading(TestStep.Ft, "Fmax", 0.5),
            },
            result.Config!.Latent.SpatialLoadings);
    }

    [Theory]
    [InlineData("""{ "spatialLoadings": [ { "name": "Vt", "loading": 1.5 } ] }""", "latent.spatialLoadings[0].step")]
    [InlineData("""{ "spatialLoadings": [ { "step": "wat", "name": "Vt", "loading": 1.5 } ] }""", "latent.spatialLoadings[0].step")]
    [InlineData("""{ "spatialLoadings": [ { "step": "cp", "loading": 1.5 } ] }""", "latent.spatialLoadings[0].name")]
    [InlineData("""{ "spatialLoadings": [ { "step": "cp", "name": "Unknown", "loading": 1.5 } ] }""", "latent.spatialLoadings[0].name")]
    [InlineData("""{ "spatialLoadings": [ { "step": "cp", "name": "Mbist", "loading": 1.5 } ] }""", "latent.spatialLoadings[0].name")]
    [InlineData("""{ "spatialLoadings": [ { "step": "cp", "name": "Ioh", "loading": 1.5 } ] }""", "latent.spatialLoadings[0].name")]
    [InlineData("""{ "spatialLoadings": [ { "step": "cp", "name": "Vt" } ] }""", "latent.spatialLoadings[0].loading")]
    [InlineData("""{ "spatialLoadings": [ { "step": "cp", "name": "Vt", "loading": -1 } ] }""", "latent.spatialLoadings[0].loading")]
    public void Validate_WithInvalidSpatialLoading_IsRejectedAtItsPath(
        string latentJson, string path)
    {
        var result = Validate(TestConfigs.WithLatent(latentJson));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Path == path);
    }

    [Fact]
    public void Validate_WithDuplicateSpatialLoading_IsRejected()
    {
        var result = Validate(TestConfigs.WithLatent(
            """
            {
              "spatialLoadings": [
                { "step": "cp", "name": "Vt", "loading": 1.5 },
                { "step": "cp", "name": "Vt", "loading": 2 }
              ]
            }
            """));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Path == "latent.spatialLoadings[1]");
    }

    [Fact]
    public void Validate_WithEmptyDiePairs_DisablesTheDefaultPairs()
    {
        var result = Validate(TestConfigs.WithLatent("""{ "diePairs": [] }"""));

        Assert.True(result.IsValid);
        Assert.Empty(result.Config!.Latent.DiePairs);
    }

    [Fact]
    public void Validate_WithOmittedTolerance_DefaultsToFiveHundredths()
    {
        var result = Validate(TestConfigs.WithLatent(
            """{ "diePairs": [ { "cpItem": "Vt", "ftItem": "Voh", "targetR": 0.4 } ] }"""));

        Assert.True(result.IsValid);
        Assert.Equal(0.05, Assert.Single(result.Config!.Latent.DiePairs).Tolerance);
    }

    [Theory]
    [InlineData("""{ "variance": { "lot": 0.1, "wafer": 0.2, "spatial": 0.1, "die": 0.3, "measurement": 0.2 } }""")]
    [InlineData("""{ "variance": { "lot": 0.1, "wafer": 0.2, "spatial": 0.1, "die": 0.4, "measurement": 0.1 } }""")]
    public void Validate_WithVarianceComponentsNotSummingToOne_IsRejected(string latentJson)
    {
        var result = Validate(TestConfigs.WithLatent(latentJson));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Path == "latent.variance");
    }

    [Fact]
    public void Validate_WithNegativeVarianceComponent_IsRejected()
    {
        var result = Validate(TestConfigs.WithLatent(
            """
            {
              "variance": {
                "lot": -0.1,
                "wafer": 0.2,
                "spatial": 0.1,
                "die": 0.5,
                "measurement": 0.3
              }
            }
            """));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Path == "latent.variance.lot");
    }

    [Fact]
    public void Validate_WithNonFiniteVarianceComponent_IsRejected()
    {
        var result = Validate(TestConfigs.WithLatent(
            """
            {
              "variance": {
                "lot": 1e400,
                "wafer": 0.2,
                "spatial": 0.1,
                "die": 0.5,
                "measurement": 0.3
              }
            }
            """));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Path == "latent.variance.lot");
    }

    [Theory]
    [InlineData("""{ "diePairs": [ { "cpItem": "Unknown", "ftItem": "Ioh", "targetR": 0.5 } ] }""", "latent.diePairs[0].cpItem")]
    [InlineData("""{ "diePairs": [ { "cpItem": "Mbist", "ftItem": "Ioh", "targetR": 0.5 } ] }""", "latent.diePairs[0].cpItem")]
    [InlineData("""{ "diePairs": [ { "cpItem": "Vt", "ftItem": "Unknown", "targetR": 0.5 } ] }""", "latent.diePairs[0].ftItem")]
    [InlineData("""{ "diePairs": [ { "cpItem": "Vt", "ftItem": "ScanFT", "targetR": 0.5 } ] }""", "latent.diePairs[0].ftItem")]
    [InlineData("""{ "diePairs": [ { "cpItem": "Vt", "ftItem": "Voh", "targetR": 1.5 } ] }""", "latent.diePairs[0].targetR")]
    [InlineData("""{ "diePairs": [ { "cpItem": "Vt", "ftItem": "Voh", "targetR": 0.5, "tolerance": 0 } ] }""", "latent.diePairs[0].tolerance")]
    [InlineData("""{ "diePairs": [ { "cpItem": "Vt", "ftItem": "Voh", "targetR": 0.5, "tolerance": -0.1 } ] }""", "latent.diePairs[0].tolerance")]
    public void Validate_WithInvalidDiePair_IsRejectedAtItsPath(string latentJson, string path)
    {
        var result = Validate(TestConfigs.WithLatent(latentJson));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Path == path);
    }

    [Fact]
    public void Validate_WithDuplicateDiePair_IsRejected()
    {
        var result = Validate(TestConfigs.WithLatent(
            """
            {
              "diePairs": [
                { "cpItem": "Vt", "ftItem": "Voh", "targetR": 0.4 },
                { "cpItem": "Vt", "ftItem": "Voh", "targetR": 0.5 }
              ]
            }
            """));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Path == "latent.diePairs[1]");
    }

    [Fact]
    public void Validate_TargetRThatTheVarianceComponentsCannotReach_IsRejected()
    {
        var result = Validate(TestConfigs.WithLatent(
            """
            {
              "variance": {
                "lot": 0.02,
                "wafer": 0.02,
                "spatial": 0.01,
                "die": 0.05,
                "measurement": 0.9
              },
              "diePairs": [
                { "cpItem": "Vt", "ftItem": "Voh", "targetR": 0.9 }
              ]
            }
            """));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Path == "latent.diePairs[0].targetR" &&
            error.Message.Contains("variance components", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_WhenOneItemNeedsMoreDieVarianceThanItHas_IsRejected()
    {
        var result = Validate(TestConfigs.WithLatent(
            """
            {
              "diePairs": [
                { "cpItem": "Vt", "ftItem": "Fmax", "targetR": -0.25 },
                { "cpItem": "Vt", "ftItem": "Voh", "targetR": 0.8 }
              ]
            }
            """));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Path == "latent.diePairs[1].cpItem" &&
            error.Message.Contains("Vt", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WithUnknownLatentField_IsRejected()
    {
        var result = Validate(TestConfigs.WithLatent("""{ "unknown": true }"""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Path.Contains("latent", StringComparison.OrdinalIgnoreCase));
    }

    private static ConfigValidationResult Validate(string json)
    {
        using var workspace = new TestWorkspace();
        return new GenerationPipeline().Validate(workspace.WriteConfig(json));
    }
}
