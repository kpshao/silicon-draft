using System.Text.Json;
using SiliconDraft.TestSupport;

namespace SiliconDraft.Core.Tests;

/// <summary>
/// Covers configuring Test Items outside the built-in catalogs: overriding a catalog
/// item and extending a Test Program with a fully defined custom item. Every effective
/// definition is recorded in the Resolved Config.
/// </summary>
public sealed class TestItemConfigurationTests
{
    private const string CustomCpNumeric = """
        {
          "testItems": [
            {
              "name": "Vbr",
              "testNumber": 1021,
              "unit": "V",
              "type": "numeric",
              "specLimits": { "low": 25, "high": 35 },
              "testLimits": { "low": 26, "high": 34 },
              "failSoftBin": 120,
              "distribution": { "kind": "normal", "mean": 30, "sigma": 1.3 },
              "siteOffsets": [0, 0.1, -0.08, 0.06, -0.1, 0.08, -0.05, 0.03]
            }
          ]
        }
        """;

    [Fact]
    public void Validate_WithCustomCpNumericItem_RecordsTheDefinitionAndExtendsTheProgram()
    {
        using var workspace = new TestWorkspace();
        var result = new GenerationPipeline().Validate(
            workspace.WriteConfig(TestConfigs.WithCp(CustomCpNumeric)));

        Assert.True(result.IsValid);
        var custom = Assert.Single(result.Config!.Cp.CustomTestItems);
        Assert.Equal(1021, custom.TestNumber);
        Assert.Equal("Vbr", custom.Name);
        Assert.Equal("V", custom.Unit);
        Assert.Equal(TestItemKind.Numeric, custom.Kind);
        Assert.Equal(new Limits(25, 35), custom.SpecLimits);
        Assert.Equal(new Limits(26, 34), custom.TestLimits);
        Assert.Equal(120, custom.FailSoftBin);
        Assert.Equal(new Distribution(DistributionKind.Normal, 30, 1.3), custom.Distribution);
        Assert.Null(custom.PassRate);
        Assert.Equal(8, custom.SiteOffsets.Count);

        var dataset = new GenerationPipeline().Generate(result.Config);
        var item = Assert.IsType<NumericTestItem>(dataset.CpProgram.Items[^1]);
        Assert.Equal("Vbr", item.Name);
        Assert.Equal(new Limits(26, 34), item.TestLimits);
        Assert.Equal(120, item.FailSoftBin);
        Assert.Equal(8, item.SiteOffsets.Count);
    }

    [Fact]
    public void Export_WithCustomCpItem_RecordsItInTheManifestResolvedConfig()
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var config = pipeline.Validate(
            workspace.WriteConfig(TestConfigs.WithCp(CustomCpNumeric))).Config!;
        var dataset = pipeline.Generate(config);
        var result = pipeline.Export(dataset, workspace.PathFor("dataset"));
        Assert.True(result.Success);

        using var manifest = JsonDocument.Parse(File.ReadAllText(result.ManifestPath!));
        var custom = Assert.Single(
            manifest.RootElement.GetProperty("resolvedConfig").GetProperty("cp")
                .GetProperty("customTestItems").EnumerateArray());
        Assert.Equal("Vbr", custom.GetProperty("name").GetString());
        Assert.Equal(1021, custom.GetProperty("testNumber").GetInt32());
        Assert.Equal("normal", custom.GetProperty("distribution").GetProperty("kind").GetString());
        Assert.False(custom.TryGetProperty("passRate", out _));
    }

    [Fact]
    public void Validate_WithCustomItemBeforeAnOverride_ReportsTheOverridePath()
    {
        using var workspace = new TestWorkspace();
        var result = new GenerationPipeline().Validate(workspace.WriteConfig(TestConfigs.WithCp(
            """
            {
              "testItems": [
                {
                  "name": "Vbr",
                  "testNumber": 1021,
                  "unit": "V",
                  "type": "numeric",
                  "testLimits": { "low": 26, "high": 34 },
                  "failSoftBin": 120,
                  "distribution": { "kind": "normal", "mean": 30, "sigma": 1.3 }
                },
                { "name": "Vt", "failSoftBin": 101 }
              ]
            }
            """)));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors, error => error.Path == "cp.testItems[1].failSoftBin");
        Assert.Contains("duplicate", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WithCustomFtItemBeforeAnOverride_ReportsTheOverridePath()
    {
        using var workspace = new TestWorkspace();
        var result = new GenerationPipeline().Validate(workspace.WriteConfig(TestConfigs.WithFt(
            """
            {
              "testItems": [
                {
                  "name": "Tj",
                  "testNumber": 4016,
                  "unit": "C",
                  "type": "numeric",
                  "testLimits": { "low": 20, "high": 100 },
                  "failSoftBin": 315,
                  "distribution": { "kind": "normal", "mean": 60, "sigma": 8 }
                },
                { "name": "Icc", "failSoftBin": 301 }
              ]
            }
            """)));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors, error => error.Path == "ft.testItems[1].failSoftBin");
        Assert.Contains("duplicate", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WithTextThatCannotBeExported_IsRejected()
    {
        var longName = new string('A', 256);
        using var workspace = new TestWorkspace();
        var result = new GenerationPipeline().Validate(workspace.WriteConfig(TestConfigs.WithCp(
            $$"""
            {
              "testItems": [
                {
                  "name": "{{longName}}",
                  "testNumber": 1021,
                  "unit": "V",
                  "type": "numeric",
                  "testLimits": { "low": 26, "high": 34 },
                  "failSoftBin": 120,
                  "distribution": { "kind": "normal", "mean": 30, "sigma": 1.3 }
                }
              ]
            }
            """)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Path == "cp.testItems[0].name");
    }

    [Fact]
    public void Validate_WithTabInAName_IsRejected()
    {
        using var workspace = new TestWorkspace();
        var result = new GenerationPipeline().Validate(workspace.WriteConfig(TestConfigs.WithCp(
            """
            {
              "testItems": [
                {
                  "name": "V\tbr",
                  "testNumber": 1021,
                  "unit": "V",
                  "type": "numeric",
                  "testLimits": { "low": 26, "high": 34 },
                  "failSoftBin": 120,
                  "distribution": { "kind": "normal", "mean": 30, "sigma": 1.3 }
                }
              ]
            }
            """)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Path == "cp.testItems[0].name");
    }

    [Fact]
    public void Validate_WithCustomCpFunctionalItem_RecordsTheDefinitionAndExtendsTheProgram()
    {
        using var workspace = new TestWorkspace();
        var result = new GenerationPipeline().Validate(workspace.WriteConfig(TestConfigs.WithCp(
            """
            {
              "testItems": [
                {
                  "name": "IdsatBinning",
                  "testNumber": 2004,
                  "type": "functional",
                  "testLimits": { "low": 0.5, "high": 1.5 },
                  "failSoftBin": 203,
                  "passRate": 0.99
                }
              ]
            }
            """)));

        Assert.True(result.IsValid);
        var custom = Assert.Single(result.Config!.Cp.CustomTestItems);
        Assert.Equal(TestItemKind.Functional, custom.Kind);
        Assert.Equal(0.99, custom.PassRate);
        Assert.Null(custom.Distribution);
        Assert.Equal("P/F", custom.Unit);
        Assert.Equal(new Limits(0.5, 1.5), custom.SpecLimits);

        var dataset = new GenerationPipeline().Generate(result.Config);
        var item = Assert.IsType<FunctionalTestItem>(dataset.CpProgram.Items[^1]);
        Assert.Equal("IdsatBinning", item.Name);
        Assert.Equal(0.99, item.PassRate);
        Assert.Equal(203, item.FailSoftBin);
    }

    [Fact]
    public void Validate_WithCustomCpItemWithoutSpecLimits_DefaultsSpecToTestLimits()
    {
        using var workspace = new TestWorkspace();
        var result = new GenerationPipeline().Validate(workspace.WriteConfig(TestConfigs.WithCp(
            """
            {
              "testItems": [
                {
                  "name": "Vbr",
                  "testNumber": 1021,
                  "unit": "V",
                  "type": "numeric",
                  "testLimits": { "low": 26, "high": 34 },
                  "failSoftBin": 120,
                  "distribution": { "kind": "normal", "mean": 30, "sigma": 1.3 }
                }
              ]
            }
            """)));

        Assert.True(result.IsValid);
        var custom = Assert.Single(result.Config!.Cp.CustomTestItems);
        Assert.Equal(new Limits(26, 34), custom.SpecLimits);
        Assert.Empty(custom.SiteOffsets);
    }

    [Theory]
    [InlineData(
        """{ "testItems": [ { "name": "Vbr", "testNumber": 1021, "unit": "V", "type": "numeric", "failSoftBin": 120, "distribution": { "kind": "normal", "mean": 30, "sigma": 1.3 } } ] }""",
        "cp.testItems[0].testLimits",
        "required")]
    [InlineData(
        """{ "testItems": [ { "name": "Vbr", "unit": "V", "type": "numeric", "testLimits": { "low": 26, "high": 34 }, "failSoftBin": 120, "distribution": { "kind": "normal", "mean": 30, "sigma": 1.3 } } ] }""",
        "cp.testItems[0].testNumber",
        "required")]
    [InlineData(
        """{ "testItems": [ { "name": "Vbr", "testNumber": 1021, "unit": "V", "type": "numeric", "testLimits": { "low": 26, "high": 34 }, "distribution": { "kind": "normal", "mean": 30, "sigma": 1.3 } } ] }""",
        "cp.testItems[0].failSoftBin",
        "required")]
    [InlineData(
        """{ "testItems": [ { "name": "Vbr", "testNumber": 1021, "type": "numeric", "testLimits": { "low": 26, "high": 34 }, "failSoftBin": 120, "distribution": { "kind": "normal", "mean": 30, "sigma": 1.3 } } ] }""",
        "cp.testItems[0].unit",
        "required")]
    [InlineData(
        """{ "testItems": [ { "name": "Vbr", "testNumber": 1021, "unit": "V", "testLimits": { "low": 26, "high": 34 }, "failSoftBin": 120, "distribution": { "kind": "normal", "mean": 30, "sigma": 1.3 } } ] }""",
        "cp.testItems[0].type",
        "required")]
    [InlineData(
        """{ "testItems": [ { "name": "Vbr", "testNumber": 1021, "unit": "V", "type": "numeric", "testLimits": { "low": 26, "high": 34 }, "failSoftBin": 120 } ] }""",
        "cp.testItems[0].distribution",
        "required")]
    [InlineData(
        """{ "testItems": [ { "name": "IdsatBinning", "testNumber": 2004, "type": "functional", "testLimits": { "low": 0.5, "high": 1.5 }, "failSoftBin": 203 } ] }""",
        "cp.testItems[0].passRate",
        "required")]
    [InlineData(
        """{ "testItems": [ { "name": "IdsatBinning", "testNumber": 2004, "type": "functional", "testLimits": { "low": 0.5, "high": 1.5 }, "failSoftBin": 203, "passRate": 0.99, "distribution": { "kind": "normal", "mean": 0, "sigma": 1 } } ] }""",
        "cp.testItems[0].distribution",
        "numeric")]
    [InlineData(
        """{ "testItems": [ { "name": "Vbr", "testNumber": 1021, "unit": "V", "type": "numeric", "testLimits": { "low": 26, "high": 34 }, "failSoftBin": 120, "distribution": { "kind": "normal", "mean": 30, "sigma": 1.3 }, "passRate": 0.9 } ] }""",
        "cp.testItems[0].passRate",
        "functional")]
    [InlineData(
        """{ "testItems": [ { "name": "Vt", "testNumber": 1021 } ] }""",
        "cp.testItems[0]",
        "does not define")]
    [InlineData(
        """{ "testItems": [ { "name": "Vbr", "testNumber": 1001, "unit": "V", "type": "numeric", "testLimits": { "low": 26, "high": 34 }, "failSoftBin": 120, "distribution": { "kind": "normal", "mean": 30, "sigma": 1.3 } } ] }""",
        "cp.testItems[0].testNumber",
        "duplicate")]
    [InlineData(
        """{ "testItems": [ { "name": "Vbr", "testNumber": 1021, "unit": "V", "type": "numeric", "testLimits": { "low": 26, "high": 34 }, "failSoftBin": 100, "distribution": { "kind": "normal", "mean": 30, "sigma": 1.3 } } ] }""",
        "cp.testItems[0].failSoftBin",
        "duplicate")]
    [InlineData(
        """{ "testItems": [ { "name": "Vbr", "testNumber": 65536, "unit": "V", "type": "numeric", "testLimits": { "low": 26, "high": 34 }, "failSoftBin": 120, "distribution": { "kind": "normal", "mean": 30, "sigma": 1.3 } } ] }""",
        "cp.testItems[0].testNumber",
        "between 1 and 65535")]
    [InlineData(
        """{ "testItems": [ { "name": "IdsatBinning", "testNumber": 2004, "type": "functional", "testLimits": { "low": 2, "high": 3 }, "failSoftBin": 203, "passRate": 0.99 } ] }""",
        "cp.testItems[0].testLimits",
        "pass value 1")]
    [InlineData(
        """{ "testItems": [ { "name": "Scan", "testLimits": { "low": 2, "high": 3 } } ] }""",
        "cp.testItems[0].testLimits",
        "pass value 1")]
    public void Validate_WithInvalidCustomCpItem_IsRejected(
        string cpJson, string expectedPath, string expectedMessagePart)
    {
        using var workspace = new TestWorkspace();
        var result = new GenerationPipeline().Validate(
            workspace.WriteConfig(TestConfigs.WithCp(cpJson)));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors, error => error.Path == expectedPath);
        Assert.Contains(expectedMessagePart, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WithFtOverridesAndCustomItems_RecordsAndAppliesThem()
    {
        using var workspace = new TestWorkspace();
        var result = new GenerationPipeline().Validate(workspace.WriteConfig(TestConfigs.WithFt(
            """
            {
              "testItems": [
                { "name": "Icc", "testLimits": { "low": 0, "high": 15 } },
                {
                  "name": "Tj",
                  "testNumber": 4016,
                  "unit": "C",
                  "type": "numeric",
                  "testLimits": { "low": 20, "high": 100 },
                  "failSoftBin": 315,
                  "distribution": { "kind": "normal", "mean": 60, "sigma": 8 }
                }
              ]
            }
            """)));

        Assert.True(result.IsValid);
        var ft = result.Config!.Ft;
        var icc = Assert.Single(ft.TestItems);
        Assert.Equal("Icc", icc.Name);
        Assert.Equal(new Limits(0, 15), icc.TestLimits);
        var custom = Assert.Single(ft.CustomTestItems);
        Assert.Equal("Tj", custom.Name);
        Assert.Equal(315, custom.FailSoftBin);

        var dataset = new GenerationPipeline().Generate(result.Config);
        Assert.Equal(16, dataset.FtProgram.Items.OfType<NumericTestItem>().Count());
        Assert.Equal(3, dataset.FtProgram.Items.OfType<FunctionalTestItem>().Count());
        var effectiveIcc = dataset.FtProgram.Items.Single(item => item.Name == "Icc");
        Assert.Equal(new Limits(0, 15), effectiveIcc.TestLimits);
        Assert.Equal("Tj", dataset.FtProgram.Items[^1].Name);
    }

    [Fact]
    public void Validate_WithFtOverridesWhoseFailSoftBinCollidesWithCpPassSoftBin_IsRejected()
    {
        using var workspace = new TestWorkspace();
        var result = new GenerationPipeline().Validate(workspace.WriteConfig(TestConfigs.WithFt(
            """{ "testItems": [ { "name": "Icc", "failSoftBin": 1 } ] }""")));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Path == "ft.testItems[0].failSoftBin" &&
                     error.Message.Contains("cp.bins.passSoftBin", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_WithCustomFtItemWhoseFailSoftBinIsDuplicated_IsRejected()
    {
        using var workspace = new TestWorkspace();
        var result = new GenerationPipeline().Validate(workspace.WriteConfig(TestConfigs.WithFt(
            """
            {
              "testItems": [
                {
                  "name": "Tj",
                  "testNumber": 4016,
                  "unit": "C",
                  "type": "numeric",
                  "testLimits": { "low": 20, "high": 100 },
                  "failSoftBin": 300,
                  "distribution": { "kind": "normal", "mean": 60, "sigma": 8 }
                }
              ]
            }
            """)));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors, error => error.Path == "ft.testItems[0].failSoftBin");
        Assert.Contains("duplicate", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WithWatOverridesAndCustomItems_RecordsAndAppliesThem()
    {
        using var workspace = new TestWorkspace();
        var result = new GenerationPipeline().Validate(workspace.WriteConfig(TestConfigs.WithWat(
            """
            {
              "testItems": [
                { "name": "Vtn", "testLimits": { "low": 0.41, "high": 0.49 } },
                {
                  "name": "Rsn",
                  "testNumber": 3013,
                  "unit": "Ohm/sq",
                  "testLimits": { "low": 110, "high": 130 },
                  "distribution": { "kind": "normal", "mean": 120, "sigma": 4 }
                }
              ]
            }
            """)));

        Assert.True(result.IsValid);
        var wat = result.Config!.Wat;
        var vtn = Assert.Single(wat.TestItems);
        Assert.Equal("Vtn", vtn.Name);
        Assert.Equal(new Limits(0.41, 0.49), vtn.TestLimits);
        var custom = Assert.Single(wat.CustomTestItems);
        Assert.Equal("Rsn", custom.Name);
        Assert.Equal(3013, custom.TestNumber);

        var dataset = new GenerationPipeline().Generate(result.Config);
        Assert.Equal(13, dataset.WatProgram.Items.Count);
        Assert.Equal(new Limits(0.41, 0.49), dataset.WatProgram.Items[0].TestLimits);
        Assert.Equal("Rsn", dataset.WatProgram.Items[^1].Name);
        Assert.All(
            dataset.Lots.SelectMany(lot => lot.Wafers).SelectMany(wafer => wafer.WatResults),
            measurement => Assert.Contains(
                dataset.WatProgram.Items, item => item.Name == measurement.Item.Name));
    }

    [Theory]
    [InlineData(
        """{ "testItems": [ { "name": "Rsn", "testNumber": 3013, "unit": "Ohm/sq", "distribution": { "kind": "normal", "mean": 120, "sigma": 4 } } ] }""",
        "wat.testItems[0].testLimits",
        "required")]
    [InlineData(
        """{ "testItems": [ { "name": "Rsn", "testNumber": 3013, "unit": "Ohm/sq", "testLimits": { "low": 110, "high": 130 } } ] }""",
        "wat.testItems[0].distribution",
        "required")]
    [InlineData(
        """{ "testItems": [ { "name": "Rsn", "testNumber": 3001, "unit": "Ohm/sq", "testLimits": { "low": 110, "high": 130 }, "distribution": { "kind": "normal", "mean": 120, "sigma": 4 } } ] }""",
        "wat.testItems[0].testNumber",
        "duplicate")]
    public void Validate_WithInvalidCustomWatItem_IsRejected(
        string watJson, string expectedPath, string expectedMessagePart)
    {
        using var workspace = new TestWorkspace();
        var result = new GenerationPipeline().Validate(
            workspace.WriteConfig(TestConfigs.WithWat(watJson)));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors, error => error.Path == expectedPath);
        Assert.Contains(expectedMessagePart, error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
