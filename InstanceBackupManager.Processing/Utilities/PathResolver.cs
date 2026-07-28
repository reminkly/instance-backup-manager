namespace InstanceBackupManager.Processing.Utilities;

/// <summary>
/// Provides consistent resolution of configured filesystem paths.
/// </summary>
internal static class PathResolver
{
    #region Internal Methods

    /// <summary>
    /// Expands environment variables and converts a configured source path into an absolute filesystem path.
    /// </summary>
    /// <param name="source">The configured source path.</param>
    /// <param name="instancePath">The absolute instance directory used as the base for relative source paths.</param>
    /// <returns>The normalized absolute source path.</returns>
    /// <remarks>
    /// Relative source paths are interpreted relative to the instance directory containing <c>instance.json</c>.
    /// </remarks>
    internal static string ResolveSourcePath(
        string source,
        string instancePath
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(instancePath);

        var expandedSource = Environment.ExpandEnvironmentVariables(source);

        return Path.IsPathRooted(expandedSource)
            ? Path.GetFullPath(expandedSource)
            : Path.GetFullPath(expandedSource, instancePath);
    }

    #endregion
}