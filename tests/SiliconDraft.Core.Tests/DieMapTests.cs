using SiliconDraft.TestSupport;

namespace SiliconDraft.Core.Tests;

public sealed class DieMapTests
{
    [Fact]
    public void Generate_WithDefaultGeometry_ProducesStableDieMap()
    {
        var map = Generate(TestConfigs.Valid).SingleMap();

        Assert.Equal(2517, map.GrossDie);
        Assert.Equal(336, map.PartialDieFound);
        Assert.Equal(0, map.PartialDieRetained);
        Assert.Equal(2517, map.Dies.Count);
        Assert.All(map.Dies, die =>
        {
            Assert.Equal(DieDisposition.Valid, die.Disposition);
            Assert.True(die.Tested);
        });
        Assert.Contains(map.Dies, die => die is { X: 0, Y: 0 });
    }

    [Fact]
    public void Generate_WithSmallGeometry_ExcludesPartialDieByDefault()
    {
        var map = Generate(TestConfigs.WithWafer(SmallGeometry("exclude"))).SingleMap();

        Assert.Equal(61, map.GrossDie);
        Assert.Equal(40, map.PartialDieFound);
        Assert.Equal(0, map.PartialDieRetained);
        Assert.Equal(61, map.Dies.Count);
        Assert.Contains(map.Dies, die => die is { X: 0, Y: 0, Disposition: DieDisposition.Valid });
        Assert.Contains(map.Dies, die => die is { X: 4, Y: 0 });
        Assert.DoesNotContain(map.Dies, die => die is { X: 4, Y: 3 });
        Assert.DoesNotContain(map.Dies, die => die is { X: 5, Y: 0 });
    }

    [Fact]
    public void Generate_WithRetainPolicy_IncludesPartialDieAsUntested()
    {
        var excluded = Generate(TestConfigs.WithWafer(SmallGeometry("exclude"))).SingleMap();
        var retained = Generate(TestConfigs.WithWafer(SmallGeometry("retain"))).SingleMap();

        Assert.Equal(61, retained.GrossDie);
        Assert.Equal(40, retained.PartialDieFound);
        Assert.Equal(40, retained.PartialDieRetained);
        Assert.Equal(101, retained.Dies.Count);
        Assert.Equal(
            40,
            retained.Dies.Count(die => die.Disposition == DieDisposition.Partial && !die.Tested));

        Assert.Equal(ValidCoordinates(excluded), ValidCoordinates(retained));
    }

    [Fact]
    public void Generate_WithGridOffset_TranslatesTheGridAndChangesTheMap()
    {
        var centered = Generate(TestConfigs.WithWafer(SmallGeometry("exclude"))).SingleMap();
        var offset = Generate(TestConfigs.WithWafer(
                SmallGeometry("exclude", "\"offsetMm\": { \"x\": 5, \"y\": 0 }")))
            .SingleMap();

        Assert.Equal(61, centered.GrossDie);
        Assert.Equal(60, offset.GrossDie);
        Assert.Equal(40, offset.PartialDieFound);
        AssertCenter(offset, 0, 0, -5, 0);
        Assert.Equal(-3, offset.Dies.Min(die => die.X));
        Assert.Equal(4, offset.Dies.Max(die => die.X));
        Assert.Equal(-4, offset.Dies.Min(die => die.Y));
        Assert.Equal(4, offset.Dies.Max(die => die.Y));
    }

    [Theory]
    [InlineData(0, 20, 0, 0, 10)]
    [InlineData(90, 0, 20, -10, 0)]
    [InlineData(180, -20, 0, 0, -10)]
    [InlineData(270, 0, -20, 10, 0)]
    public void Generate_WithRotation_RotatesDiePlacementAndCoordinates(
        int rotationDeg,
        double columnX,
        double columnY,
        double rowX,
        double rowY)
    {
        var map = Generate(TestConfigs.WithWafer(
                RectGeometry($"\"rotationDeg\": {rotationDeg}")))
            .SingleMap();

        Assert.Equal(23, map.GrossDie);
        AssertCenter(map, 1, 0, columnX, columnY);
        AssertCenter(map, 0, 1, rowX, rowY);
    }

    [Fact]
    public void Generate_WithMirroring_MirrorsDiePlacementAndCoordinates()
    {
        var mirroredX = Generate(TestConfigs.WithWafer(RectGeometry("\"mirrorX\": true"))).SingleMap();
        var mirroredY = Generate(TestConfigs.WithWafer(RectGeometry("\"mirrorY\": true"))).SingleMap();

        Assert.Equal(23, mirroredX.GrossDie);
        AssertCenter(mirroredX, 1, 0, -20, 0);
        AssertCenter(mirroredX, 0, 1, 0, 10);
        Assert.Equal(23, mirroredY.GrossDie);
        AssertCenter(mirroredY, 1, 0, 20, 0);
        AssertCenter(mirroredY, 0, 1, 0, -10);
    }

    [Theory]
    [InlineData("center", -4, 4, -4, 4)]
    [InlineData("bottomLeft", 0, 8, 0, 8)]
    [InlineData("topLeft", 0, 8, -8, 0)]
    [InlineData("topRight", -8, 0, -8, 0)]
    [InlineData("bottomRight", -8, 0, 0, 8)]
    public void Generate_WithIndexOrigin_PlacesCoordinatesInConfiguredFrame(
        string indexOrigin, int minX, int maxX, int minY, int maxY)
    {
        var map = Generate(TestConfigs.WithWafer(
                SmallGeometry("exclude", "\"indexOrigin\": \"" + indexOrigin + "\"")))
            .SingleMap();

        Assert.Equal(61, map.GrossDie);
        Assert.Equal(minX, map.Dies.Min(die => die.X));
        Assert.Equal(maxX, map.Dies.Max(die => die.X));
        Assert.Equal(minY, map.Dies.Min(die => die.Y));
        Assert.Equal(maxY, map.Dies.Max(die => die.Y));
    }

    private static string SmallGeometry(string partialDiePolicy, string? grid = null) => $$"""
        {
          "diameterMm": 100,
          "edgeExclusionMm": 0,
          "die": { "widthMm": 10, "heightMm": 10, "scribeMm": 0 },
          "grid": { {{grid}} },
          "partialDiePolicy": "{{partialDiePolicy}}"
        }
        """;

    private static string RectGeometry(string? grid = null) => $$"""
        {
          "diameterMm": 100,
          "edgeExclusionMm": 0,
          "die": { "widthMm": 20, "heightMm": 10, "scribeMm": 0 },
          "grid": { {{grid}} }
        }
        """;

    private static void AssertCenter(DieMap map, int x, int y, double expectedX, double expectedY)
    {
        var die = Assert.Single(map.Dies, die => die.X == x && die.Y == y);
        Assert.Equal(expectedX, die.CenterXmm, 6);
        Assert.Equal(expectedY, die.CenterYmm, 6);
    }

    private static string[] ValidCoordinates(DieMap map) => map.Dies
        .Where(die => die.Disposition == DieDisposition.Valid)
        .Select(die => $"{die.X},{die.Y}")
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    private static Dataset Generate(string json)
    {
        using var workspace = new TestWorkspace();
        var pipeline = new GenerationPipeline();
        var config = pipeline.Validate(workspace.WriteConfig(json)).Config!;
        return pipeline.Generate(config);
    }
}

internal static class DatasetTestExtensions
{
    public static DieMap SingleMap(this Dataset dataset) =>
        Assert.Single(Assert.Single(dataset.Lots).Wafers).DieMap;
}
