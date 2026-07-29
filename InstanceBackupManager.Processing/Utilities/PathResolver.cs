namespace InstanceBackupManager.Processing.Utilities;

/// <summary>
/// Provides consistent resolution of configured filesystem paths.
/// </summary>
internal static class PathResolver
{
    #region Internal Methods

    /// <summary>
    /// Expands environment variables and converts a configured path into an absolute filesystem path.
    /// </summary>
    internal static string ResolveConfiguredPath(
        string configuredPath,
        string instancePath
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(instancePath);

        var expandedPath = Environment.ExpandEnvironmentVariables(configuredPath);

        return Path.IsPathRooted(expandedPath)
            ? Path.GetFullPath(expandedPath)
            : Path.GetFullPath(expandedPath, instancePath);
    }

    /// <summary>
    /// Resolves a configured target source path relative to its containing instance directory.
    /// </summary>
    internal static string ResolveSourcePath(
        string source,
        string instancePath
    )
    {
        return ResolveConfiguredPath(source, instancePath);
    }

    #endregion
}
