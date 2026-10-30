using SiliconDraft.TestSupport;

namespace SiliconDraft.Core.Tests;

public sealed class WatConfigValidationTests
{
    [Fact]
    public void Validate_WithoutWatSection_UsesNineDefaultSitesLongTemplateAndPerLotFiles()
    {
        using var workspace = new TestWorkspace();

        var result = new GenerationPipeline().Validate(workspace.WriteConfig(TestConfigs.Valid));

        Assert.True(result.IsValid);
        var wat = result.Config!.Wat;
        Assert.Equal(WatTemplate.Long, wat.Template);
        Assert.Equal(WatFileOrganization.PerLot, wat.Organization);
        Assert.Equal(9, wat.Sites.Count);

        var center = wat.Sites[0];
        Assert.Equal("S1", center.Id);
        Assert.Equal(0, center.XMm);
        Assert.Equal(0, center.YMm);

        // The default ring sits halfway between the wafer center and the wafer edge.
        var ring = wat.Sites.Skip(1).ToArray();
        Assert.Equal(8, ring.Length);
        Assert.All(
            ring,
            site => Assert.Equal(75, Math.Sqrt(site.XMm * site.XMm + site.YMm * site.YMm), 3));
        Assert.Equal(8, ring.Select(site => (site.XMm, site.YMm)).Distinct().Count());
        Assert.All(ring, site => Assert.StartsWith("S", site.Id, StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WithWatSection_RecordsConfiguredSitesTemplateAndOrganization()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.WriteConfig(TestConfigs.WithWat(
            """
            {
              "sites": [
                { "id": "C", "xMm": 0, "yMm": 0 },
                { "id": "E", "xMm": 40, "yMm": 0 }
              ],
              "template": "wide",
              "organization": "perWafer"
            }
            """));

        var result = new GenerationPipeline().Validate(configPath);

        Assert.True(result.IsValid);
        var wat = result.Config!.Wat;
        Assert.Equal(WatTemplate.Wide, wat.Template);
        Assert.Equal(WatFileOrganization.PerWafer, wat.Organization);
        Assert.Equal(2, wat.Sites.Count);
        Assert.Equal(new PcmSite("C", 0, 0), wat.Sites[0]);
        Assert.Equal(new PcmSite("E", 40, 0), wat.Sites[1]);
    }

    [Theory]
    [InlineData("""{ "sites": [] }""", "wat.sites", "at least one")]
    [InlineData("""{ "sites": [ null ] }""", "wat.sites[0]", "required")]
    [InlineData("""{ "sites": [ { "xMm": 0, "yMm": 0 } ] }""", "wat.sites[0].id", "required")]
    [InlineData("""{ "sites": [ { "id": "A" } ] }""", "wat.sites[0].xMm", "required")]
    [InlineData("""{ "sites": [ { "id": "A", "xMm": 0 } ] }""", "wat.sites[0].yMm", "required")]
    [InlineData(
        """{ "sites": [ { "id": "A", "xMm": 0, "yMm": 0 }, { "id": "a", "xMm": 1, "yMm": 1 } ] }""",
        "wat.sites[1].id",
        "duplicate")]
    [InlineData(
        """{ "sites": [ { "id": "A", "xMm": 151, "yMm": 0 } ] }""",
        "wat.sites[0]",
        "within the wafer")]
    [InlineData("""{ "sites": [ { "id": "A 1", "xMm": 0, "yMm": 0 } ] }""", "wat.sites[0].id", "letters")]
    [InlineData("""{ "template": "csv" }""", "wat.template", "long")]
    [InlineData("""{ "organization": "perFile" }""", "wat.organization", "perLot")]
    public void Validate_WithInvalidWatDefinition_IsRejected(
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
