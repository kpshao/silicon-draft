namespace SiliconDraft.Core;

public static class ConfigPaths
{
    /// <summary>
    /// Resolves the configured output directory. Relative paths are resolved against the
    /// configuration file's directory, so the same configuration writes to the same place
    /// regardless of the current working directory.
    /// </summary>
    public static string ResolveOutputDirectory(string configuredDirectory, string configPath)
    {
        if (Path.IsPathRooted(configuredDirectory))
        {
            return configuredDirectory;
        }

        var configDirectory = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
        return Path.GetFullPath(Path.Combine(configDirectory, configuredDirectory));
    }
}
