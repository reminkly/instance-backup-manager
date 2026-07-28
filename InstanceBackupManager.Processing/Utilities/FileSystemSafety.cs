namespace InstanceBackupManager.Processing.Utilities;

/// <summary>
/// Provides consistent operating-system-aware path comparison, containment, overlap, and reparse-point safety rules.
/// </summary>
internal static class FileSystemSafety
{
    #region Path Comparison

    /// <summary>
    /// Determines whether two filesystem paths refer to the same normalized location.
    /// </summary>
    /// <param name="firstPath">The first path to compare.</param>
    /// <param name="secondPath">The second path to compare.</param>
    /// <returns><see langword="true"/> when both paths refer to the same location; otherwise, <see langword="false"/>.</returns>
    internal static bool PathsEqual(
        string firstPath,
        string secondPath
    )
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(firstPath)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(secondPath)),
            GetPathComparison()
        );
    }

    /// <summary>
    /// Determines whether two filesystem paths are equal or whether either path is contained beneath the other.
    /// </summary>
    /// <param name="firstPath">The first path to compare.</param>
    /// <param name="secondPath">The second path to compare.</param>
    /// <returns><see langword="true"/> when the paths overlap; otherwise, <see langword="false"/>.</returns>
    internal static bool PathsOverlap(
        string firstPath,
        string secondPath
    )
    {
        return IsSamePathOrChildOf(
            firstPath,
            secondPath
        ) || IsSamePathOrChildOf(
            secondPath,
            firstPath
        );
    }

    /// <summary>
    /// Determines whether a path is equal to or contained beneath another path.
    /// </summary>
    /// <param name="candidatePath">The candidate path.</param>
    /// <param name="parentPath">The possible parent path.</param>
    /// <returns>
    /// <see langword="true"/> when the candidate is equal to or beneath the parent; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    internal static bool IsSamePathOrChildOf(
        string candidatePath,
        string parentPath
    )
    {
        var normalizedCandidate = NormalizeDirectoryPath(candidatePath);
        var normalizedParent = NormalizeDirectoryPath(parentPath);

        return normalizedCandidate.StartsWith(
            normalizedParent,
            GetPathComparison()
        );
    }

    /// <summary>
    /// Gets the appropriate path comparer for the current operating system.
    /// </summary>
    /// <returns>A case-insensitive comparer on Windows and a case-sensitive comparer on other operating systems.</returns>
    internal static StringComparer GetPathComparer()
    {
        return OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }

    /// <summary>
    /// Gets the appropriate path-comparison behavior for the current operating system.
    /// </summary>
    /// <returns>A case-insensitive comparison on Windows and a case-sensitive comparison on other operating systems.</returns>
    internal static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    #endregion

    #region Path Containment

    /// <summary>
    /// Ensures that a resolved path is located strictly beneath a required parent directory.
    /// </summary>
    /// <param name="candidatePath">The resolved candidate path.</param>
    /// <param name="parentPath">The directory that must contain the candidate.</param>
    /// <param name="description">The description used when reporting an unsafe path.</param>
    /// <exception cref="InvalidDataException">
    /// Thrown when the candidate is equal to or is not contained beneath the required parent.
    /// </exception>
    internal static void EnsurePathIsWithinDirectory(
        string candidatePath,
        string parentPath,
        string description
    )
    {
        var normalizedCandidate = Path.GetFullPath(candidatePath);
        var normalizedParent = Path.GetFullPath(parentPath);

        if (PathsEqual(
            normalizedCandidate,
            normalizedParent
        ) || !normalizedCandidate.StartsWith(
            NormalizeDirectoryPath(normalizedParent),
            GetPathComparison()
        ))
        {
            throw new InvalidDataException(
                $"{description} escapes its required parent directory."
            );
        }
    }

    #endregion

    #region Reparse-Point Safety

    /// <summary>
    /// Ensures that every existing component of a path is a normal filesystem entry rather than a reparse point.
    /// </summary>
    /// <param name="path">The path whose existing components will be inspected.</param>
    /// <exception cref="IOException">
    /// Thrown when an existing path component is a symbolic link, junction, or another reparse point.
    /// </exception>
    internal static void EnsureExistingPathContainsNoReparsePoints(string path)
    {
        var currentPath = Path.GetFullPath(path);

        while (!string.IsNullOrWhiteSpace(currentPath))
        {
            if (Directory.Exists(currentPath))
            {
                ThrowIfReparsePoint(
                    new DirectoryInfo(currentPath)
                );
            }
            else if (File.Exists(currentPath))
            {
                ThrowIfReparsePoint(
                    new FileInfo(currentPath)
                );
            }

            var parentPath = Path.GetDirectoryName(currentPath);

            if (string.IsNullOrWhiteSpace(parentPath) || PathsEqual(currentPath, parentPath))
            {
                break;
            }

            currentPath = parentPath;
        }
    }

    /// <summary>
    /// Throws an exception when a filesystem entry is a symbolic link, junction, or another reparse-point type.
    /// </summary>
    /// <param name="entry">The filesystem entry to inspect.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entry"/> is null.</exception>
    /// <exception cref="IOException">Thrown when <paramref name="entry"/> is a reparse point.</exception>
    internal static void ThrowIfReparsePoint(FileSystemInfo entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException(
                $"Symbolic links and junctions are not currently supported: '{entry.FullName}'."
            );
        }
    }

    #endregion

    #region Path Normalization

    /// <summary>
    /// Normalizes a directory path and ensures that it ends with exactly one directory separator.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized absolute directory path.</returns>
    private static string NormalizeDirectoryPath(string path)
    {
        var normalizedPath = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(path)
        );

        return Path.EndsInDirectorySeparator(normalizedPath)
            ? normalizedPath
            : normalizedPath + Path.DirectorySeparatorChar;
    }

    #endregion
}