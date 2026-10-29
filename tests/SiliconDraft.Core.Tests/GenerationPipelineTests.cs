using System.Text.Json;
using SiliconDraft.TestSupport;

namespace SiliconDraft.Core.Tests;

public sealed class GenerationPipelineTests
{
    [Fact]
    public void Validate_WithValidConfig_ReturnsResolvedConfig()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(TestConfigs.Valid);

        var result = new GenerationPipeline().Validate(configPath);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        var resolved = Assert.IsType<ResolvedConfig>(result.Config);
        Assert.Equal(1, resolved.SchemaVersion);
        Assert.Equal("DemoProduct", resolved.Product.Name);
        Assert.Equal(42, resolved.Seed);
        var lot = Assert.Single(resolved.Lots);
        Assert.Equal("LOT-0001", lot.Id);
        Assert.Equal(1, lot.WaferCount);
        Assert.Equal("out", resolved.Output.Directory);
    }

    [Fact]
    public void Generate_WithValidConfig_ReturnsDatasetWithConfiguredProductAndWafers()
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var config = LoadConfig(pipeline, workspace);

        var dataset = pipeline.Generate(config);

        Assert.Equal("DemoProduct", dataset.ProductName);
        Assert.Equal(42, dataset.Seed);
        Assert.Equal(1, dataset.LotCount);
        Assert.Equal(1, dataset.WaferCount);
        var lot = Assert.Single(dataset.Lots);
        Assert.Equal("LOT-0001", lot.Id);
        Assert.Equal("LOT-0001-W01", Assert.Single(lot.Wafers).Id);
    }

    [Fact]
    public void Export_WritesGroundTruthManifest()
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var dataset = pipeline.Generate(LoadConfig(pipeline, workspace));
        var outputDirectory = workspace.PathFor("dataset");

        var result = pipeline.Export(dataset, outputDirectory);

        Assert.True(result.Success);
        var manifestPath = Path.Combine(outputDirectory, "_truth", "manifest.json");
        Assert.Equal(manifestPath, result.ManifestPath);
        Assert.True(File.Exists(manifestPath));

        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = manifest.RootElement;
        Assert.Equal(3, root.GetProperty("manifestSchemaVersion").GetInt32());
        Assert.Equal(1, root.GetProperty("configSchemaVersion").GetInt32());
        Assert.Equal(42, root.GetProperty("seed").GetInt64());

        var resolved = root.GetProperty("resolvedConfig");
        Assert.Equal(1, resolved.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("DemoProduct", resolved.GetProperty("product").GetProperty("name").GetString());
        Assert.Equal("out", resolved.GetProperty("output").GetProperty("directory").GetString());
        var resolvedWafer = resolved.GetProperty("wafer");
        Assert.Equal(300, resolvedWafer.GetProperty("diameterMm").GetDouble());
        Assert.Equal(5.08, resolvedWafer.GetProperty("pitchWidthMm").GetDouble(), 6);
        Assert.Equal("exclude", resolvedWafer.GetProperty("partialDiePolicy").GetString());
        var resolvedCp = resolved.GetProperty("cp");
        Assert.Equal(1, resolvedCp.GetProperty("bins").GetProperty("passHardBin").GetInt32());
        Assert.Equal(2, resolvedCp.GetProperty("bins").GetProperty("parametricFailHardBin").GetInt32());
        Assert.Empty(resolvedCp.GetProperty("testItems").EnumerateArray());

        var datasetMetadata = root.GetProperty("dataset");
        Assert.Equal("DemoProduct", datasetMetadata.GetProperty("product").GetString());
        Assert.Equal(1, datasetMetadata.GetProperty("lotCount").GetInt32());
        Assert.Equal(1, datasetMetadata.GetProperty("waferCount").GetInt32());
        Assert.Equal(2517, datasetMetadata.GetProperty("grossDie").GetInt32());
        Assert.Equal(336, datasetMetadata.GetProperty("partialDie").GetInt32());

        var cp = datasetMetadata.GetProperty("cp");
        Assert.Equal("cp-catalog-v1", cp.GetProperty("catalogVersion").GetString());
        Assert.Equal(2517, cp.GetProperty("testedDie").GetInt32());
        var passedDie = cp.GetProperty("passedDie").GetInt32();
        Assert.InRange(passedDie, 1, 2516);
        Assert.Equal(passedDie, datasetMetadata.GetProperty("netDie").GetInt32());
        Assert.Equal(
            passedDie,
            cp.GetProperty("hardBins").EnumerateArray()
                .Single(bin => bin.GetProperty("pass").GetBoolean())
                .GetProperty("count")
                .GetInt32());

        var lot = Assert.Single(datasetMetadata.GetProperty("lots").EnumerateArray());
        Assert.Equal("LOT-0001", lot.GetProperty("lotId").GetString());
        Assert.Equal(1, lot.GetProperty("waferCount").GetInt32());
        Assert.Equal(passedDie, lot.GetProperty("netDie").GetInt32());
        Assert.Equal(passedDie, lot.GetProperty("cp").GetProperty("passedDie").GetInt32());
        var wafer = Assert.Single(lot.GetProperty("wafers").EnumerateArray());
        Assert.Equal("LOT-0001-W01", wafer.GetProperty("waferId").GetString());
        Assert.Equal(2517, wafer.GetProperty("grossDie").GetInt32());
        Assert.Equal(passedDie, wafer.GetProperty("netDie").GetInt32());
    }

    [Fact]
    public void Export_SameConfigAndSeed_ProducesIdenticalManifest()
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var config = LoadConfig(pipeline, workspace);

        var first = pipeline.Export(pipeline.Generate(config), workspace.PathFor("run-a"));
        var second = pipeline.Export(pipeline.Generate(config), workspace.PathFor("run-b"));

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(File.ReadAllBytes(first.ManifestPath!), File.ReadAllBytes(second.ManifestPath!));
    }

    [Fact]
    public void Export_DifferentSeed_ProducesDifferentManifest()
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var firstConfig = LoadConfig(pipeline, workspace);
        var secondConfig = pipeline.Validate(workspace.WriteConfig(
            TestConfigs.Valid.Replace("\"seed\": 42", "\"seed\": 43"),
            "second.json")).Config!;

        var first = pipeline.Export(pipeline.Generate(firstConfig), workspace.PathFor("run-a"));
        var second = pipeline.Export(pipeline.Generate(secondConfig), workspace.PathFor("run-b"));

        Assert.NotEqual(File.ReadAllBytes(first.ManifestPath!), File.ReadAllBytes(second.ManifestPath!));
    }

    [Fact]
    public void Export_WhenManifestAlreadyExists_RequiresOverwriteConfirmation()
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var dataset = pipeline.Generate(LoadConfig(pipeline, workspace));
        var outputDirectory = workspace.PathFor("dataset");
        Assert.True(pipeline.Export(dataset, outputDirectory).Success);

        var second = pipeline.Export(dataset, outputDirectory);

        Assert.False(second.Success);
        Assert.Contains("already exists", second.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_WithOverwriteConfirmation_ReplacesExistingManifest()
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var dataset = pipeline.Generate(LoadConfig(pipeline, workspace));
        var outputDirectory = workspace.PathFor("dataset");
        Assert.True(pipeline.Export(dataset, outputDirectory).Success);

        var second = pipeline.Export(dataset, outputDirectory, overwrite: true);

        Assert.True(second.Success);
        Assert.True(File.Exists(second.ManifestPath));
    }

    private static ResolvedConfig LoadConfig(GenerationPipeline pipeline, TestWorkspace workspace)
    {
        var configPath = workspace.WriteConfig(TestConfigs.Valid);
        return pipeline.Validate(configPath).Config!;
    }
}
