using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Clear;
using InstanceBackupManager.Processing.Models.Configuration;
using InstanceBackupManager.Processing.Models.Instances;
using InstanceBackupManager.Processing.Utilities;

namespace InstanceBackupManager.Processing;

/// <summary>
/// Validates and clears files and directory contents for targets that explicitly permit clearing.
/// </summary>
public sealed class ClearProcessor
{
    #region Properties

    /// <summary>
    /// Gets the time provider used to determine when clear operations complete.
    /// </summary>
    private TimeProvider TimeProvider { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new clear processor using the system time provider.
    /// </summary>
    public ClearProcessor()
        : this(TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a new clear processor using the specified time provider.
    /// </summary>
    /// <param name="timeProvider">The time provider used when assigning clear completion timestamps.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="timeProvider"/> is null.</exception>
    public ClearProcessor(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        TimeProvider = timeProvider;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Clears every enabled target that explicitly permits the clear operation.
    /// </summary>
    /// <param name="instance">The loaded instance containing the targets to clear.</param>
    /// <returns>A summary describing the targets and files processed by the operation.</returns>
    /// <remarks>
    /// Configured files are deleted. For configured directories, the contents are deleted while the configured root
    /// directory itself is preserved. Every target is validated before any data is removed.
    /// </remarks>
    public ClearResult ClearInstance(InstanceContext instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (!instance.Config.Enabled)
        {
            throw new InvalidOperationException($"Instance '{instance.Config.Name}' is disabled.");
        }

        var planEntries = CreateClearPlan(instance);

        if (planEntries.Count == 0)
        {
            throw new InvalidOperationException(
                $"Instance '{instance.Config.Name}' does not contain any enabled targets that permit clearing."
            );
        }

        var resultEntries = new List<ClearResultEntry>();

        foreach (var planEntry in planEntries)
        {
            resultEntries.Add(
                ClearTarget(planEntry)
            );
        }

        return new ClearResult
        {
            CompletedUtc = TimeProvider.GetUtcNow(),
            Entries = resultEntries.AsReadOnly()
        };
    }

    #endregion

    #region Clear Planning

    /// <summary>
    /// Creates and validates the complete set of clear operations before any files are removed.
    /// </summary>
    /// <param name="instance">The loaded instance containing the configured targets.</param>
    /// <returns>A read-only collection of validated clear-plan entries.</returns>
    private static IReadOnlyCollection<ClearPlanEntry> CreateClearPlan(InstanceContext instance)
    {
        var planEntries = new List<ClearPlanEntry>();

        foreach (var target in instance.Config.Targets)
        {
            if (!target.Enabled || !target.AllowClear)
            {
                continue;
            }

            var sourcePath = PathResolver.ResolveSourcePath(
                target.Source,
                instance.InstancePath
            );

            ValidateClearPath(
                target,
                sourcePath,
                instance
            );

            var statistics = InspectTarget(
                target,
                sourcePath
            );

            planEntries.Add(
                new ClearPlanEntry(
                    Target: target,
                    SourcePath: sourcePath,
                    FileCount: statistics.FileCount,
                    TotalBytes: statistics.TotalBytes
                )
            );
        }

        ValidateTargetsDoNotOverlap(planEntries);

        return planEntries.AsReadOnly();
    }

    /// <summary>
    /// Validates that a target path is safe for a destructive clear operation.
    /// </summary>
    /// <param name="target">The configured target being validated.</param>
    /// <param name="sourcePath">The resolved absolute source path.</param>
    /// <param name="instance">The current instance and its protected runtime paths.</param>
    private static void ValidateClearPath(
        TargetPath target,
        string sourcePath,
        InstanceContext instance
    )
    {
        if (target.Type == TargetPathType.Unknown || !Enum.IsDefined(target.Type))
        {
            throw new InvalidOperationException(
                $"Target '{target.Id}' has an unsupported target type '{target.Type}'."
            );
        }

        if (IsRootPath(sourcePath))
        {
            throw new InvalidOperationException(
                $"Target '{target.Id}' resolves to filesystem root '{sourcePath}' and cannot be cleared."
            );
        }

        if (PathsEqual(sourcePath, instance.InstancePath))
        {
            throw new InvalidOperationException(
                $"Target '{target.Id}' resolves to the instance directory and cannot be cleared."
            );
        }

        if (PathsOverlap(sourcePath, instance.BackupsPath))
        {
            throw new InvalidOperationException(
                $"Target '{target.Id}' overlaps the instance backups directory and cannot be cleared."
            );
        }

        EnsureExistingPathContainsNoReparsePoints(sourcePath);

        switch (target.Type)
        {
            case TargetPathType.File when Directory.Exists(sourcePath):
                throw new InvalidOperationException(
                    $"Target '{target.Id}' is configured as a file, but its source is an existing directory."
                );

            case TargetPathType.Directory when File.Exists(sourcePath):
                throw new InvalidOperationException(
                    $"Target '{target.Id}' is configured as a directory, but its source is an existing file."
                );
        }
    }

    /// <summary>
    /// Ensures that no two planned clear targets are equal, nested, or otherwise overlapping.
    /// </summary>
    /// <param name="planEntries">The validated clear targets to inspect.</param>
    private static void ValidateTargetsDoNotOverlap(IReadOnlyList<ClearPlanEntry> planEntries)
    {
        for (var firstIndex = 0; firstIndex < planEntries.Count; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < planEntries.Count; secondIndex++)
            {
                var firstEntry = planEntries[firstIndex];
                var secondEntry = planEntries[secondIndex];

                if (!PathsOverlap(firstEntry.SourcePath, secondEntry.SourcePath))
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"Clear targets '{firstEntry.Target.Id}' and '{secondEntry.Target.Id}' have overlapping source paths."
                );
            }
        }
    }

    #endregion

    #region Target Inspection

    /// <summary>
    /// Inspects a target and calculates the files and bytes that will be removed.
    /// </summary>
    /// <param name="target">The configured target being inspected.</param>
    /// <param name="sourcePath">The resolved absolute source path.</param>
    /// <returns>The number of files and combined bytes currently present.</returns>
    private static ClearStatistics InspectTarget(
        TargetPath target,
        string sourcePath
    )
    {
        return target.Type switch
        {
            TargetPathType.File => InspectFile(sourcePath),
            TargetPathType.Directory => InspectDirectory(sourcePath),
            _ => throw new InvalidOperationException(
                $"Target '{target.Id}' has an unsupported target type '{target.Type}'."
            )
        };
    }

    /// <summary>
    /// Inspects a configured file without modifying it.
    /// </summary>
    /// <param name="sourcePath">The absolute configured file path.</param>
    /// <returns>Statistics describing the file, or zero values when it does not exist.</returns>
    private static ClearStatistics InspectFile(string sourcePath)
    {
        if (!File.Exists(sourcePath))
        {
            return new ClearStatistics(
                FileCount: 0,
                TotalBytes: 0
            );
        }

        var file = new FileInfo(sourcePath);

        ThrowIfReparsePoint(file);

        return new ClearStatistics(
            FileCount: 1,
            TotalBytes: file.Length
        );
    }

    /// <summary>
    /// Recursively inspects a configured directory without modifying it.
    /// </summary>
    /// <param name="sourcePath">The absolute configured directory path.</param>
    /// <returns>Statistics describing all files contained by the directory.</returns>
    private static ClearStatistics InspectDirectory(string sourcePath)
    {
        if (!Directory.Exists(sourcePath))
        {
            return new ClearStatistics(
                FileCount: 0,
                TotalBytes: 0
            );
        }

        return InspectDirectoryContents(
            new DirectoryInfo(sourcePath)
        );
    }

    /// <summary>
    /// Recursively validates directory contents and calculates their file count and combined size.
    /// </summary>
    /// <param name="directory">The directory currently being inspected.</param>
    /// <returns>Statistics describing all files contained by the directory.</returns>
    private static ClearStatistics InspectDirectoryContents(DirectoryInfo directory)
    {
        ThrowIfReparsePoint(directory);

        long fileCount = 0;
        long totalBytes = 0;

        foreach (var file in directory.EnumerateFiles())
        {
            ThrowIfReparsePoint(file);

            fileCount++;
            totalBytes += file.Length;
        }

        foreach (var childDirectory in directory.EnumerateDirectories())
        {
            var childStatistics = InspectDirectoryContents(childDirectory);

            fileCount += childStatistics.FileCount;
            totalBytes += childStatistics.TotalBytes;
        }

        return new ClearStatistics(
            FileCount: fileCount,
            TotalBytes: totalBytes
        );
    }

    #endregion

    #region Clear Operations

    /// <summary>
    /// Executes a previously validated clear operation for one target.
    /// </summary>
    /// <param name="planEntry">The validated target and its pre-clear statistics.</param>
    /// <returns>A result entry describing the completed target operation.</returns>
    private static ClearResultEntry ClearTarget(ClearPlanEntry planEntry)
    {
        switch (planEntry.Target.Type)
        {
            case TargetPathType.File:
                ClearFile(planEntry.SourcePath);
                break;

            case TargetPathType.Directory:
                ClearDirectory(planEntry.SourcePath);
                break;

            default:
                throw new InvalidOperationException(
                    $"Target '{planEntry.Target.Id}' has an unsupported target type '{planEntry.Target.Type}'."
                );
        }

        return new ClearResultEntry
        {
            TargetId = planEntry.Target.Id,
            TargetName = planEntry.Target.Name,
            SourcePath = planEntry.SourcePath,
            Type = planEntry.Target.Type,
            FileCount = planEntry.FileCount,
            TotalBytes = planEntry.TotalBytes
        };
    }

    /// <summary>
    /// Deletes a configured file when it exists.
    /// </summary>
    /// <param name="sourcePath">The absolute file path to clear.</param>
    private static void ClearFile(string sourcePath)
    {
        if (File.Exists(sourcePath))
        {
            File.Delete(sourcePath);
        }
    }

    /// <summary>
    /// Deletes the contents of a configured directory while preserving the configured root directory.
    /// </summary>
    /// <param name="sourcePath">The absolute directory whose contents will be cleared.</param>
    private static void ClearDirectory(string sourcePath)
    {
        if (!Directory.Exists(sourcePath))
        {
            return;
        }

        var directory = new DirectoryInfo(sourcePath);

        foreach (var file in directory.EnumerateFiles())
        {
            file.Delete();
        }

        foreach (var childDirectory in directory.EnumerateDirectories())
        {
            childDirectory.Delete(recursive: true);
        }
    }

    #endregion

    #region Path Safety

    /// <summary>
    /// Determines whether a path represents its filesystem root.
    /// </summary>
    /// <param name="path">The absolute path to inspect.</param>
    /// <returns><see langword="true"/> when the path is a filesystem root; otherwise, <see langword="false"/>.</returns>
    private static bool IsRootPath(string path)
    {
        var normalizedPath = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(path)
        );

        var rootPath = Path.GetPathRoot(normalizedPath);

        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return false;
        }

        var normalizedRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(rootPath)
        );

        return string.Equals(
            normalizedPath,
            normalizedRoot,
            GetPathComparison()
        );
    }

    /// <summary>
    /// Determines whether two filesystem paths refer to the same location.
    /// </summary>
    /// <param name="firstPath">The first absolute path.</param>
    /// <param name="secondPath">The second absolute path.</param>
    /// <returns><see langword="true"/> when the paths are equal; otherwise, <see langword="false"/>.</returns>
    private static bool PathsEqual(
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
    /// Determines whether two filesystem paths are equal or whether either path contains the other.
    /// </summary>
    /// <param name="firstPath">The first absolute path.</param>
    /// <param name="secondPath">The second absolute path.</param>
    /// <returns><see langword="true"/> when the paths overlap; otherwise, <see langword="false"/>.</returns>
    private static bool PathsOverlap(
        string firstPath,
        string secondPath
    )
    {
        return IsSamePathOrChildOf(firstPath, secondPath)
               || IsSamePathOrChildOf(secondPath, firstPath);
    }

    /// <summary>
    /// Determines whether a path is equal to or contained beneath another path.
    /// </summary>
    /// <param name="candidatePath">The candidate absolute path.</param>
    /// <param name="parentPath">The possible parent absolute path.</param>
    /// <returns><see langword="true"/> when the candidate is equal to or beneath the parent; otherwise, <see langword="false"/>.</returns>
    private static bool IsSamePathOrChildOf(
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
    /// Normalizes a path and ensures that it ends with a directory separator.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized absolute path ending with a directory separator.</returns>
    private static string NormalizeDirectoryPath(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path))
               + Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// Ensures that every existing component of a path is a normal filesystem entry rather than a symbolic link or junction.
    /// </summary>
    /// <param name="path">The path whose existing components will be inspected.</param>
    private static void EnsureExistingPathContainsNoReparsePoints(string path)
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
    private static void ThrowIfReparsePoint(FileSystemInfo entry)
    {
        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException(
                $"Symbolic links and junctions cannot be cleared: '{entry.FullName}'."
            );
        }
    }

    /// <summary>
    /// Gets the appropriate path-comparison behavior for the current operating system.
    /// </summary>
    /// <returns>A case-insensitive comparison on Windows and a case-sensitive comparison on other operating systems.</returns>
    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    #endregion

    #region Private Types

    /// <summary>
    /// Describes a validated target that is ready to be cleared.
    /// </summary>
    /// <param name="Target">The configured target being cleared.</param>
    /// <param name="SourcePath">The resolved absolute source path.</param>
    /// <param name="FileCount">The number of files that will be removed.</param>
    /// <param name="TotalBytes">The combined size, in bytes, of the files that will be removed.</param>
    private sealed record ClearPlanEntry(
        TargetPath Target,
        string SourcePath,
        long FileCount,
        long TotalBytes
    );

    /// <summary>
    /// Contains aggregate information about files present beneath a clear target.
    /// </summary>
    /// <param name="FileCount">The number of files present.</param>
    /// <param name="TotalBytes">The combined size, in bytes, of the files present.</param>
    private readonly record struct ClearStatistics(
        long FileCount,
        long TotalBytes
    );

    #endregion
}