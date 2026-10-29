namespace SiliconDraft.Core;

public enum ConfigFailureKind
{
    None,
    UnreadableFile,
    InvalidConfiguration,
}

public sealed class ConfigValidationResult
{
    private ConfigValidationResult(
        IReadOnlyList<ConfigError> errors,
        ResolvedConfig? config,
        ConfigFailureKind failureKind)
    {
        Errors = errors;
        Config = config;
        FailureKind = failureKind;
    }

    public IReadOnlyList<ConfigError> Errors { get; }

    public ResolvedConfig? Config { get; }

    public ConfigFailureKind FailureKind { get; }

    public bool IsValid => FailureKind == ConfigFailureKind.None;

    internal static ConfigValidationResult Valid(ResolvedConfig config) =>
        new([], config, ConfigFailureKind.None);

    internal static ConfigValidationResult Invalid(IEnumerable<ConfigError> errors) =>
        new(errors.ToArray(), null, ConfigFailureKind.InvalidConfiguration);

    internal static ConfigValidationResult Unreadable(IEnumerable<ConfigError> errors) =>
        new(errors.ToArray(), null, ConfigFailureKind.UnreadableFile);
}

public sealed record ConfigError(string Path, string Message)
{
    public override string ToString() => $"{Path}: {Message}";
}
