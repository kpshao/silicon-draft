namespace SiliconDraft.Core;

/// <summary>
/// The only primary seam into SiliconDraft: configuration in, Dataset and manifest out.
/// CLI and GUI are adapters over this facade and carry no domain logic.
/// </summary>
public sealed class GenerationPipeline
{
    public ConfigValidationResult Validate(string configPath) => ConfigLoader.Validate(configPath);

    public Dataset Generate(ResolvedConfig config) => DatasetGenerator.Generate(config);

    public ExportResult Export(Dataset dataset, string outputDirectory, bool overwrite = false) =>
        ManifestWriter.Write(dataset, outputDirectory, overwrite);
}
