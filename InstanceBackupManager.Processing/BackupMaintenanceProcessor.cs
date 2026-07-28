using InstanceBackupManager.Processing.Models.BackupMaintenance;
using InstanceBackupManager.Processing.Models.Backups;
using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Processing;

/// <summary>
/// Discovers, validates, and deletes completed backups without affecting unrelated backup-directory contents.
/// </summary>
public sealed class BackupMaintenanceProcessor
{
    #region Properties

    /// <summary>
    /// Gets the processor used to discover and validate completed backups.
    /// </summary>
    private RestoreProcessor RestoreProcessor { get; }

    /// <summary>
    /// Gets the time provider used to determine when deletion operations complete.
    /// </summary>
    private TimeProvider TimeProvider { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new backup-maintenance processor using default processors and the system time provider.
    /// </summary>
    public BackupMaintenanceProcessor()
        : this(
            new RestoreProcessor(),
            TimeProvider.System
        )
    {
    }

    /// <summary>
    /// Initializes a new backup-maintenance processor using the specified restore processor and time provider.
    /// </summary>
    /// <param name="restoreProcessor">The processor used to discover and validate completed backups.</param>
    /// <param name="timeProvider">The time provider used when assigning deletion completion timestamps.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="restoreProcessor"/> or <paramref name="timeProvider"/> is null.
    /// </exception>
    public BackupMaintenanceProcessor(
        RestoreProcessor restoreProcessor,
        TimeProvider timeProvider
    )
    {
        ArgumentNullException.ThrowIfNull(restoreProcessor);
        ArgumentNullException.ThrowIfNull(timeProvider);

        RestoreProcessor = restoreProcessor;
        TimeProvider = timeProvider;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Deletes one validated, completed backup from an instance.
    /// </summary>
    /// <param name="instance">The loaded instance that owns the backup.</param>
    /// <param name="backupName">The directory name assigned to the backup.</param>
    /// <returns>A result describing the deleted backup.</returns>
    public BackupDeletionResult DeleteBackup(
        InstanceContext instance,
        string backupName
    )
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupName);

        return DeleteBackups(
            instance,
            [backupName]
        );
    }

    /// <summary>
    /// Deletes a specified collection of validated, completed backups from an instance.
    /// </summary>
    /// <param name="instance">The loaded instance that owns the backups.</param>
    /// <param name="backupNames">The directory names assigned to the backups being deleted.</param>
    /// <returns>A result describing every deleted backup.</returns>
    /// <remarks>
    /// Every selected backup is discovered and validated before any directory is removed.
    /// </remarks>
    public BackupDeletionResult DeleteBackups(
        InstanceContext instance,
        IReadOnlyCollection<string> backupNames
    )
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(backupNames);

        ValidateRequestedBackupNames(backupNames);

        if (backupNames.Count == 0)
        {
            return CreateResult([]);
        }

        var discoveredBackups = RestoreProcessor
            .DiscoverBackups(instance)
            .ToList();

        var selectedBackups = new List<BackupDescriptor>();

        foreach (var backupName in backupNames)
        {
            var selectedBackup = discoveredBackups.SingleOrDefault(
                backup => string.Equals(
                    backup.BackupName,
                    backupName,
                    GetPathComparison()
                )
            );

            if (selectedBackup is null)
            {
                throw new DirectoryNotFoundException(
                    $"Completed backup '{backupName}' was not found for instance '{instance.Config.Name}'."
                );
            }

            selectedBackups.Add(selectedBackup);
        }

        return DeleteDiscoveredBackups(
            selectedBackups,
            instance.BackupsPath
        );
    }

    /// <summary>
    /// Deletes every validated, completed backup belonging to an instance.
    /// </summary>
    /// <param name="instance">The loaded instance whose completed backups will be deleted.</param>
    /// <returns>A result describing every deleted backup.</returns>
    /// <remarks>
    /// All completed backups are validated before any directory is removed. In-progress and unrelated directories are
    /// not returned by backup discovery and therefore do not participate in this operation.
    /// </remarks>
    public BackupDeletionResult DeleteAllBackups(InstanceContext instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var backups = RestoreProcessor
            .DiscoverBackups(instance)
            .ToList();

        return DeleteDiscoveredBackups(
            backups,
            instance.BackupsPath
        );
    }

    #endregion

    #region Request Validation

    /// <summary>
    /// Validates that requested backup names are nonempty and unique for the current operating system.
    /// </summary>
    /// <param name="backupNames">The requested completed-backup directory names.</param>
    private static void ValidateRequestedBackupNames(IReadOnlyCollection<string> backupNames)
    {
        var encounteredNames = new HashSet<string>(
            GetPathComparer()
        );

        foreach (var backupName in backupNames)
        {
            if (string.IsNullOrWhiteSpace(backupName))
            {
                throw new ArgumentException(
                    "Backup names cannot contain null, empty, or whitespace values.",
                    nameof(backupNames)
                );
            }

            if (!encounteredNames.Add(backupName))
            {
                throw new ArgumentException(
                    $"Backup name '{backupName}' was requested more than once.",
                    nameof(backupNames)
                );
            }
        }
    }

    #endregion

    #region Deletion Planning

    /// <summary>
    /// Creates and validates every deletion plan before removing any completed-backup directory.
    /// </summary>
    /// <param name="backups">The discovered backups selected for deletion.</param>
    /// <param name="backupsPath">The absolute directory containing completed backups for the instance.</param>
    /// <returns>A result describing every deleted backup.</returns>
    private BackupDeletionResult DeleteDiscoveredBackups(
        IReadOnlyCollection<BackupDescriptor> backups,
        string backupsPath
    )
    {
        var planEntries = new List<BackupDeletionPlanEntry>();

        foreach (var backup in backups)
        {
            planEntries.Add(
                CreateDeletionPlanEntry(
                    backup,
                    backupsPath
                )
            );
        }

        foreach (var planEntry in planEntries)
        {
            DeleteBackupDirectory(planEntry);
        }

        return CreateResult(planEntries);
    }

    /// <summary>
    /// Creates and validates a deletion plan for one completed backup.
    /// </summary>
    /// <param name="backup">The discovered backup being prepared for deletion.</param>
    /// <param name="backupsPath">The absolute directory containing completed backups for the instance.</param>
    /// <returns>A validated deletion-plan entry.</returns>
    private static BackupDeletionPlanEntry CreateDeletionPlanEntry(
        BackupDescriptor backup,
        string backupsPath
    )
    {
        ValidateBackupPath(
            backup,
            backupsPath
        );

        var statistics = InspectBackupDirectory(
            new DirectoryInfo(backup.BackupPath)
        );

        return new BackupDeletionPlanEntry(
            Backup: backup,
            FileCount: statistics.FileCount,
            TotalBytes: statistics.TotalBytes
        );
    }

    /// <summary>
    /// Validates that a discovered backup is an existing direct child of the instance backups directory.
    /// </summary>
    /// <param name="backup">The discovered backup being validated.</param>
    /// <param name="backupsPath">The absolute directory that must directly contain the backup.</param>
    private static void ValidateBackupPath(
        BackupDescriptor backup,
        string backupsPath
    )
    {
        var normalizedBackupPath = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(backup.BackupPath)
        );

        var normalizedBackupsPath = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(backupsPath)
        );

        var parentPath = Directory.GetParent(normalizedBackupPath)?.FullName;

        if (string.IsNullOrWhiteSpace(parentPath) || !PathsEqual(parentPath, normalizedBackupsPath))
        {
            throw new InvalidDataException(
                $"Backup '{backup.BackupName}' is not a direct child of the instance backups directory."
            );
        }

        if (!Directory.Exists(normalizedBackupPath))
        {
            throw new DirectoryNotFoundException(
                $"Completed backup directory '{normalizedBackupPath}' was not found."
            );
        }

        ThrowIfReparsePoint(
            new DirectoryInfo(normalizedBackupPath)
        );
    }

    #endregion

    #region Directory Inspection

    /// <summary>
    /// Recursively validates a backup directory and calculates the files and bytes that will be removed.
    /// </summary>
    /// <param name="directory">The backup directory currently being inspected.</param>
    /// <returns>The number of files and combined bytes contained by the directory.</returns>
    private static BackupDirectoryStatistics InspectBackupDirectory(DirectoryInfo directory)
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
            var childStatistics = InspectBackupDirectory(childDirectory);

            fileCount += childStatistics.FileCount;
            totalBytes += childStatistics.TotalBytes;
        }

        return new BackupDirectoryStatistics(
            FileCount: fileCount,
            TotalBytes: totalBytes
        );
    }

    #endregion

    #region Deletion Operations

    /// <summary>
    /// Deletes a previously validated completed-backup directory.
    /// </summary>
    /// <param name="planEntry">The validated backup deletion plan.</param>
    private static void DeleteBackupDirectory(BackupDeletionPlanEntry planEntry)
    {
        Directory.Delete(
            planEntry.Backup.BackupPath,
            recursive: true
        );
    }

    /// <summary>
    /// Creates a completed deletion result from the supplied plan entries.
    /// </summary>
    /// <param name="planEntries">The validated backup deletion plans that were executed.</param>
    /// <returns>A result describing every deleted backup.</returns>
    private BackupDeletionResult CreateResult(IReadOnlyCollection<BackupDeletionPlanEntry> planEntries)
    {
        var resultEntries = planEntries
            .Select(
                planEntry => new BackupDeletionResultEntry
                {
                    BackupName = planEntry.Backup.BackupName,
                    BackupPath = planEntry.Backup.BackupPath,
                    Kind = planEntry.Backup.Manifest.Kind,
                    CreatedUtc = planEntry.Backup.Manifest.CreatedUtc,
                    FileCount = planEntry.FileCount,
                    TotalBytes = planEntry.TotalBytes
                }
            )
            .ToList()
            .AsReadOnly();

        return new BackupDeletionResult
        {
            CompletedUtc = TimeProvider.GetUtcNow(),
            Entries = resultEntries
        };
    }

    #endregion

    #region Path Safety

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
    /// Throws an exception when a filesystem entry is a symbolic link, junction, or another reparse-point type.
    /// </summary>
    /// <param name="entry">The filesystem entry to inspect.</param>
    private static void ThrowIfReparsePoint(FileSystemInfo entry)
    {
        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException(
                $"Symbolic links and junctions cannot be deleted through backup maintenance: '{entry.FullName}'."
            );
        }
    }

    /// <summary>
    /// Gets the appropriate path comparer for the current operating system.
    /// </summary>
    /// <returns>A case-insensitive comparer on Windows and a case-sensitive comparer on other operating systems.</returns>
    private static StringComparer GetPathComparer()
    {
        return OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
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
    /// Describes a validated completed backup that is ready to be deleted.
    /// </summary>
    /// <param name="Backup">The discovered backup being deleted.</param>
    /// <param name="FileCount">The number of files contained by the backup directory.</param>
    /// <param name="TotalBytes">The combined size, in bytes, of files contained by the backup directory.</param>
    private sealed record BackupDeletionPlanEntry(
        BackupDescriptor Backup,
        long FileCount,
        long TotalBytes
    );

    /// <summary>
    /// Contains aggregate information about files present within a completed-backup directory.
    /// </summary>
    /// <param name="FileCount">The number of files present.</param>
    /// <param name="TotalBytes">The combined size, in bytes, of the files present.</param>
    private readonly record struct BackupDirectoryStatistics(
        long FileCount,
        long TotalBytes
    );

    #endregion
}