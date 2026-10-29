using SiliconDraft.TestSupport;

namespace SiliconDraft.Core.Tests;

public sealed class ConfigValidationTests
{
    [Theory]
    [InlineData(
        """
        {
          "schemaVersion": 2,
          "product": { "name": "DemoProduct" },
          "seed": 42,
          "lots": [ { "id": "LOT-0001", "waferCount": 1 } ],
          "output": { "directory": "out" }
        }
        """,
        "schemaVersion",
        "supported")]
    [InlineData(
        """
        {
          "product": { "name": "DemoProduct" },
          "seed": 42,
          "lots": [ { "id": "LOT-0001", "waferCount": 1 } ],
          "output": { "directory": "out" }
        }
        """,
        "schemaVersion",
        "required")]
    [InlineData(
        """
        {
          "schemaVersion": 1,
          "product": { "name": "DemoProduct" },
          "lots": [ { "id": "LOT-0001", "waferCount": 1 } ],
          "output": { "directory": "out" }
        }
        """,
        "seed",
        "required")]
    [InlineData(
        """
        {
          "schemaVersion": 1,
          "seed": 42,
          "lots": [ { "id": "LOT-0001", "waferCount": 1 } ],
          "output": { "directory": "out" }
        }
        """,
        "product",
        "required")]
    [InlineData(
        """
        {
          "schemaVersion": 1,
          "product": { "name": "" },
          "seed": 42,
          "lots": [ { "id": "LOT-0001", "waferCount": 1 } ],
          "output": { "directory": "out" }
        }
        """,
        "product.name",
        "required")]
    [InlineData(
        """
        {
          "schemaVersion": 1,
          "product": { "name": "DemoProduct" },
          "seed": 42,
          "output": { "directory": "out" }
        }
        """,
        "lots",
        "at least one")]
    [InlineData(
        """
        {
          "schemaVersion": 1,
          "product": { "name": "DemoProduct" },
          "seed": 42,
          "lots": [],
          "output": { "directory": "out" }
        }
        """,
        "lots",
        "at least one")]
    [InlineData(
        """
        {
          "schemaVersion": 1,
          "product": { "name": "DemoProduct" },
          "seed": 42,
          "lots": [
            { "id": "LOT-0001", "waferCount": 1 },
            { "id": "LOT-0001", "waferCount": 1 }
          ],
          "output": { "directory": "out" }
        }
        """,
        "lots[1].id",
        "duplicate")]
    [InlineData(
        """
        {
          "schemaVersion": 1,
          "product": { "name": "DemoProduct" },
          "seed": 42,
          "lots": [
            { "id": "LOT-0001", "waferCount": 1 },
            { "id": "lot-0001", "waferCount": 1 }
          ],
          "output": { "directory": "out" }
        }
        """,
        "lots[1].id",
        "duplicate")]
    [InlineData(
        """
        {
          "schemaVersion": 1,
          "product": { "name": "DemoProduct" },
          "seed": 42,
          "lots": [ { "id": "LOT-0001", "waferCount": 0 } ],
          "output": { "directory": "out" }
        }
        """,
        "lots[0].waferCount",
        "at least 1")]
    [InlineData(
        """
        {
          "schemaVersion": 1,
          "product": { "name": "DemoProduct" },
          "seed": 42,
          "lots": [ { "waferCount": 1 } ],
          "output": { "directory": "out" }
        }
        """,
        "lots[0].id",
        "required")]
    [InlineData(
        """
        {
          "schemaVersion": 1,
          "product": { "name": "DemoProduct" },
          "seed": 42,
          "lots": [ null ],
          "output": { "directory": "out" }
        }
        """,
        "lots[0]",
        "required")]
    [InlineData(
        """
        {
          "schemaVersion": 1,
          "product": { "name": "DemoProduct" },
          "seed": 42,
          "lots": [ { "id": "LOT-0001" } ],
          "output": { "directory": "out" }
        }
        """,
        "lots[0].waferCount",
        "required")]
    [InlineData(
        """
        {
          "schemaVersion": 1,
          "product": { "name": "DemoProduct" },
          "seed": 42,
          "lots": [ { "id": "LOT-0001", "waferCount": 1 } ]
        }
        """,
        "output",
        "required")]
    [InlineData(
        """
        {
          "schemaVersion": 1,
          "product": { "name": "DemoProduct" },
          "seed": 42,
          "lots": [ { "id": "LOT-0001", "waferCount": 1 } ],
          "output": { "directory": "  " }
        }
        """,
        "output.directory",
        "required")]
    public void Validate_WithInvalidConfig_ReportsActionableError(
        string json, string expectedPath, string expectedMessagePart)
    {
        using var workspace = new TestWorkspace();
        var result = new GenerationPipeline().Validate(workspace.WriteConfig(json));

        Assert.False(result.IsValid);
        Assert.Null(result.Config);
        Assert.Equal(ConfigFailureKind.InvalidConfiguration, result.FailureKind);
        var error = Assert.Single(result.Errors, error => error.Path == expectedPath);
        Assert.Contains(expectedMessagePart, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("lot/one")]
    [InlineData("lot one")]
    [InlineData("lot,one")]
    [InlineData(".hidden")]
    public void Validate_WithUnsafeLotId_IsRejected(string lotId)
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(
            $$"""
            {
              "schemaVersion": 1,
              "product": { "name": "DemoProduct" },
              "seed": 42,
              "lots": [ { "id": "{{lotId}}", "waferCount": 1 } ],
              "output": { "directory": "out" }
            }
            """);

        var result = new GenerationPipeline().Validate(configPath);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors, error => error.Path == "lots[0].id");
        Assert.Contains("letters", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WithSeveralProblems_ReportsAllOfThem()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(
            """
            {
              "schemaVersion": 1,
              "product": { "name": "" },
              "lots": [ { "id": "LOT-0001", "waferCount": 1 } ],
              "output": { "directory": "out" }
            }
            """);

        var result = new GenerationPipeline().Validate(configPath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Path == "product.name");
        Assert.Contains(result.Errors, error => error.Path == "seed");
    }

    [Fact]
    public void Validate_WithUnknownProperty_RejectsTheConfiguration()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(
            """
            {
              "schemaVersion": 1,
              "product": { "name": "DemoProduct" },
              "seed": 42,
              "lots": [ { "id": "LOT-0001", "waferCount": 1 } ],
              "output": { "directory": "out" },
              "colour": "red"
            }
            """);

        var result = new GenerationPipeline().Validate(configPath);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Message.Contains("colour", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_WithMalformedJson_ReportsParseError()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig("{ \"schemaVersion\": ");

        var result = new GenerationPipeline().Validate(configPath);

        Assert.False(result.IsValid);
        Assert.Contains(
            "invalid JSON",
            Assert.Single(result.Errors).Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WithMissingFile_ReportsUnreadableFile()
    {
        using var workspace = new TestWorkspace();
        var missingPath = workspace.PathFor("missing/config.json");

        var result = new GenerationPipeline().Validate(missingPath);

        Assert.False(result.IsValid);
        Assert.Equal(ConfigFailureKind.UnreadableFile, result.FailureKind);
        Assert.Contains(
            "config.json",
            Assert.Single(result.Errors).Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WithDuplicateProperty_RejectsConfiguration()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(
            """
            {
              "schemaVersion": 1,
              "product": { "name": "DemoProduct" },
              "seed": 42,
              "seed": 43,
              "lots": [ { "id": "LOT-0001", "waferCount": 1 } ],
              "output": { "directory": "out" }
            }
            """);

        var result = new GenerationPipeline().Validate(configPath);

        Assert.False(result.IsValid);
        Assert.Equal(ConfigFailureKind.InvalidConfiguration, result.FailureKind);
    }

    [Fact]
    public void Validate_WithComment_RejectsConfiguration()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(
            """
            {
              // comments are not part of the strict configuration format
              "schemaVersion": 1,
              "product": { "name": "DemoProduct" },
              "seed": 42,
              "lots": [ { "id": "LOT-0001", "waferCount": 1 } ],
              "output": { "directory": "out" }
            }
            """);

        var result = new GenerationPipeline().Validate(configPath);

        Assert.False(result.IsValid);
        Assert.Equal(ConfigFailureKind.InvalidConfiguration, result.FailureKind);
    }
}
