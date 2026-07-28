using System.Text.Json;
using System.Text.Json.Serialization;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Backups;
using InstanceBackupManager.Processing.Models.Configuration;
using InstanceBackupManager.Processing.Models.Instances;
using InstanceBackupManager.Processing.Models.Restore;
using InstanceBackupManager.Processing.Utilities;

namespace InstanceBackupManager.Processing;

/// <summary>
/// Discovers completed backups and restores their contents to the destinations defined by the current configuration.
/// </summary>
public sealed class RestoreProcessor
{
    #region Constants

    private const string ManifestFileName = "manifest.json";
    private const string InProgressDirectoryPrefix = ".in-progress-";
    private const int SupportedManifestSchemaVersion = 1;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the time provider used to record when restore operations complete.
    /// </summary>
    private TimeProvider TimeProvider { get; }

    /// <summary>
    /// Gets the serializer options used when reading backup manifests.
    /// </summary>
    private JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new restore processor using the system time provider.
    /// </summary>
    public RestoreProcessor()
        : this(TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a new restore processor using the specified time provider.
    /// </summary>
    /// <param name="timeProvider">The time provider used when recording restore completion times.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="timeProvider"/> is null.</exception>
    public RestoreProcessor(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        TimeProvider = timeProvider;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Discovers completed backups for an instance and loads their manifests.
    /// </summary>
    /// <param name="instance">The loaded instance whose backups will be discovered.</param>
    /// <returns>A read-only collection of completed backups ordered from newest to oldest.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="instance"/> is null.</exception>
    /// <exception cref="InvalidDataException">Thrown when a discovered backup contains an invalid manifest.</exception>
    /// <exception cref="IOException">Thrown when a filesystem error prevents a backup from being inspected.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the application does not have permission to inspect the backups directory.
    /// </exception>
    public IReadOnlyCollection<BackupDescriptor> DiscoverBackups(InstanceContext instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (!Directory.Exists(instance.BackupsPath))
        {
            return Array.Empty<BackupDescriptor>();
        }

        var backups = new List<BackupDescriptor>();

        foreach (var backupPath in Directory.EnumerateDirectories(instance.BackupsPath))
        {
            var backupName = Path.GetFileName(backupPath);

            // Temporary directories represent incomplete operations and must never be offered for restoration.
            if (backupName.StartsWith(InProgressDirectoryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var manifestPath = Path.Combine(backupPath, ManifestFileName);

            // A directory without a manifest is not considered a completed backup.
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            var backupDirectory = new DirectoryInfo(backupPath);

            ThrowIfReparsePoint(backupDirectory);

            var manifest = LoadManifest(manifestPath);

            ValidateManifest(
                manifest,
                backupName,
                backupPath
            );

            backups.Add(
                new BackupDescriptor
                {
                    BackupName = backupName,
                    BackupPath = Path.GetFullPath(backupPath),
                    Manifest = manifest
                }
            );
        }

        var orderedBackups = backups
            .OrderByDescending(backup => backup.Manifest.CreatedUtc)
            .ThenByDescending(backup => backup.BackupName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return orderedBackups.AsReadOnly();
    }

    /// <summary>
    /// Restores a completed backup to the destinations defined by the instance's current configuration.
    /// </summary>
    /// <param name="instance">The loaded instance receiving the restored data.</param>
    /// <param name="backupName">The directory name of the backup to restore.</param>
    /// <returns>A summary of the completed restore operation.</returns>
    /// <remarks>
    /// Manifest source paths are historical metadata only. Every restore destination is resolved from the current
    /// <c>instance.json</c> configuration.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="instance"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="backupName"/> is null, empty, or consists only of whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the instance is disabled or the backup does not contain any currently enabled targets.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the selected backup directory does not exist.</exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the selected backup does not contain a manifest or an expected file payload.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when the manifest is invalid, a target cannot be matched, target types differ, or an unsafe path is found.
    /// </exception>
    /// <exception cref="IOException">Thrown when a filesystem error prevents the restore from completing.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the application does not have permission to read the backup or write a destination.
    /// </exception>
    public RestoreResult RestoreBackup(
        InstanceContext instance,
        string backupName
    )
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupName);

        if (!instance.Config.Enabled)
        {
            throw new InvalidOperationException($"Instance '{instance.Config.Name}' is disabled.");
        }

        var backupPath = ResolveBackupPath(
            instance.BackupsPath,
            backupName
        );

        if (!Directory.Exists(backupPath))
        {
            throw new DirectoryNotFoundException(
                $"The selected backup directory '{backupPath}' was not found."
            );
        }

        var backupDirectory = new DirectoryInfo(backupPath);

        ThrowIfReparsePoint(backupDirectory);

        var manifestPath = Path.Combine(backupPath, ManifestFileName);

        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException(
                "The selected backup does not contain a manifest.",
                manifestPath
            );
        }

        var manifest = LoadManifest(manifestPath);

        ValidateManifest(
            manifest,
            backupName,
            backupPath
        );

        /*
         * Build and validate the complete restore plan before copying anything. This catches configuration and manifest
         * problems before a target can be partially restored.
         */
        var restorePlan = CreateRestorePlan(
            instance,
            manifest,
            backupPath
        );

        if (restorePlan.Count == 0)
        {
            throw new InvalidOperationException(
                $"Backup '{backupName}' does not contain any targets that are currently enabled."
            );
        }

        var resultEntries = new List<RestoreResultEntry>();

        foreach (var planEntry in restorePlan)
        {
            resultEntries.Add(RestoreTarget(planEntry));
        }

        return new RestoreResult
        {
            BackupName = backupName,
            CompletedUtc = TimeProvider.GetUtcNow(),
            Entries = resultEntries.AsReadOnly()
        };
    }

    #endregion

    #region Manifest Operations

    /// <summary>
    /// Reads and deserializes a backup manifest.
    /// </summary>
    /// <param name="manifestPath">The absolute path of the manifest file.</param>
    /// <returns>The deserialized backup manifest.</returns>
    /// <exception cref="InvalidDataException">Thrown when the manifest is empty or contains invalid JSON.</exception>
    private BackupManifest LoadManifest(string manifestPath)
    {
        try
        {
            var json = File.ReadAllText(manifestPath);

            return JsonSerializer.Deserialize<BackupManifest>(
                json,
                JsonOptions
            ) ?? throw new InvalidDataException(
                $"Backup manifest '{manifestPath}' contained no data."
            );
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Backup manifest '{manifestPath}' contains invalid JSON.",
                exception
            );
        }
    }

    /// <summary>
    /// Validates the structure and paths contained in a backup manifest.
    /// </summary>
    /// <param name="manifest">The manifest to validate.</param>
    /// <param name="backupName">The actual name of the containing backup directory.</param>
    /// <param name="backupPath">The absolute path of the containing backup directory.</param>
    /// <exception cref="InvalidDataException">Thrown when the manifest fails validation.</exception>
    private static void ValidateManifest(
        BackupManifest manifest,
        string backupName,
        string backupPath
    )
    {
        if (manifest.SchemaVersion != SupportedManifestSchemaVersion)
        {
            throw new InvalidDataException(
                $"Backup '{backupName}' uses unsupported manifest schema version '{manifest.SchemaVersion}'."
            );
        }

        if (!Enum.IsDefined(manifest.Kind))
        {
            throw new InvalidDataException(
                $"Backup '{backupName}' has unsupported backup kind '{manifest.Kind}'."
            );
        }

        if (string.IsNullOrWhiteSpace(manifest.InstanceName))
        {
            throw new InvalidDataException(
                $"Backup '{backupName}' does not identify the instance that created it."
            );
        }

        if (!string.Equals(manifest.BackupName, backupName, GetPathComparison()))
        {
            throw new InvalidDataException(
                $"Backup manifest name '{manifest.BackupName}' does not match directory name '{backupName}'."
            );
        }

        if (manifest.CreatedUtc == default)
        {
            throw new InvalidDataException(
                $"Backup '{backupName}' does not contain a valid creation timestamp."
            );
        }

        if (manifest.Entries is null)
        {
            throw new InvalidDataException(
                $"Backup '{backupName}' does not contain a manifest-entry collection."
            );
        }

        var duplicateTargetIds = manifest.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.TargetId))
            .GroupBy(entry => entry.TargetId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateTargetIds.Count > 0)
        {
            throw new InvalidDataException(
                $"Backup '{backupName}' contains duplicate target ID '{duplicateTargetIds[0]}'."
            );
        }

        var resolvedPayloadPaths = new List<(string TargetId, string PayloadPath)>();

        foreach (var entry in manifest.Entries)
        {
            ValidateManifestEntry(
                entry,
                backupName,
                backupPath
            );

            var payloadPath = ResolvePayloadPath(
                backupPath,
                entry.BackupPath,
                entry.TargetId
            );

            resolvedPayloadPaths.Add(
                (
                    entry.TargetId,
                    payloadPath
                )
            );
        }

        ValidatePayloadPathConflicts(
            resolvedPayloadPaths,
            backupName
        );
    }

    /// <summary>
    /// Validates an individual manifest entry.
    /// </summary>
    /// <param name="entry">The manifest entry to validate.</param>
    /// <param name="backupName">The name of the containing backup.</param>
    /// <param name="backupPath">The absolute path of the containing backup directory.</param>
    /// <exception cref="InvalidDataException">Thrown when the manifest entry fails validation.</exception>
    private static void ValidateManifestEntry(
        BackupManifestEntry entry,
        string backupName,
        string backupPath
    )
    {
        if (string.IsNullOrWhiteSpace(entry.TargetId))
        {
            throw new InvalidDataException(
                $"Backup '{backupName}' contains a manifest entry without a target ID."
            );
        }

        if (string.IsNullOrWhiteSpace(entry.TargetName))
        {
            throw new InvalidDataException(
                $"Backup '{backupName}' contains an unnamed entry for target '{entry.TargetId}'."
            );
        }

        if (entry.Type == TargetPathType.Unknown || !Enum.IsDefined(entry.Type))
        {
            throw new InvalidDataException(
                $"Backup target '{entry.TargetId}' has unsupported type '{entry.Type}'."
            );
        }

        if (string.IsNullOrWhiteSpace(entry.BackupPath))
        {
            throw new InvalidDataException(
                $"Backup target '{entry.TargetId}' does not identify its stored payload."
            );
        }

        if (entry.FileCount < 0 || entry.TotalBytes < 0)
        {
            throw new InvalidDataException(
                $"Backup target '{entry.TargetId}' contains invalid aggregate values."
            );
        }

        _ = ResolvePayloadPath(
            backupPath,
            entry.BackupPath,
            entry.TargetId
        );
    }

    /// <summary>
    /// Verifies that manifest payload paths do not overlap.
    /// </summary>
    /// <param name="payloadPaths">The resolved payload paths and their target identifiers.</param>
    /// <param name="backupName">The name of the backup being validated.</param>
    /// <exception cref="InvalidDataException">Thrown when two payload paths overlap.</exception>
    private static void ValidatePayloadPathConflicts(
        IReadOnlyList<(string TargetId, string PayloadPath)> payloadPaths,
        string backupName
    )
    {
        for (var firstIndex = 0; firstIndex < payloadPaths.Count; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < payloadPaths.Count; secondIndex++)
            {
                var firstPayload = payloadPaths[firstIndex];
                var secondPayload = payloadPaths[secondIndex];

                if (PathsOverlap(firstPayload.PayloadPath, secondPayload.PayloadPath))
                {
                    throw new InvalidDataException(
                        $"Backup '{backupName}' contains overlapping payload paths for targets " +
                        $"'{firstPayload.TargetId}' and '{secondPayload.TargetId}'."
                    );
                }
            }
        }
    }

    #endregion

    #region Restore Planning

    /// <summary>
    /// Builds and validates the target operations required to restore a backup.
    /// </summary>
    /// <param name="instance">The current instance configuration and runtime paths.</param>
    /// <param name="manifest">The manifest describing the selected backup.</param>
    /// <param name="backupPath">The absolute path of the selected backup.</param>
    /// <returns>A read-only collection of validated restore-plan entries.</returns>
    private static IReadOnlyCollection<RestorePlanEntry> CreateRestorePlan(
        InstanceContext instance,
        BackupManifest manifest,
        string backupPath
    )
    {
        var planEntries = new List<RestorePlanEntry>();

        foreach (var manifestEntry in manifest.Entries)
        {
            var currentTarget = instance.Config.Targets.SingleOrDefault(
                target => string.Equals(
                    target.Id,
                    manifestEntry.TargetId,
                    StringComparison.OrdinalIgnoreCase
                )
            );

            if (currentTarget is null)
            {
                throw new InvalidDataException(
                    $"Backup target '{manifestEntry.TargetId}' does not exist in the current instance configuration."
                );
            }

            // Disabled targets remain visible in the manifest but do not participate in the current restore operation.
            if (!currentTarget.Enabled)
            {
                continue;
            }

            if (currentTarget.Type != manifestEntry.Type)
            {
                throw new InvalidDataException(
                    $"Backup target '{manifestEntry.TargetId}' has type '{manifestEntry.Type}', but the current " +
                    $"configuration defines it as '{currentTarget.Type}'."
                );
            }

            var payloadPath = ResolvePayloadPath(
                backupPath,
                manifestEntry.BackupPath,
                manifestEntry.TargetId
            );

            ValidatePayloadExists(
                payloadPath,
                manifestEntry
            );

            var destinationPath = PathResolver.ResolveSourcePath(
                currentTarget.Source,
                instance.InstancePath
            );

            if (PathsOverlap(destinationPath, instance.BackupsPath))
            {
                throw new InvalidDataException(
                    $"Current destination for target '{currentTarget.Id}' overlaps the instance backups directory."
                );
            }

            planEntries.Add(
                new RestorePlanEntry(
                    CurrentTarget: currentTarget,
                    ManifestEntry: manifestEntry,
                    PayloadPath: payloadPath,
                    DestinationPath: destinationPath
                )
            );
        }

        return planEntries.AsReadOnly();
    }

    /// <summary>
    /// Verifies that a manifest payload exists and matches its declared target type.
    /// </summary>
    /// <param name="payloadPath">The absolute path of the stored payload.</param>
    /// <param name="manifestEntry">The manifest entry describing the payload.</param>
    private static void ValidatePayloadExists(
        string payloadPath,
        BackupManifestEntry manifestEntry
    )
    {
        switch (manifestEntry.Type)
        {
            case TargetPathType.File when !File.Exists(payloadPath):
                throw new FileNotFoundException(
                    $"Backup payload for target '{manifestEntry.TargetId}' was not found.",
                    payloadPath
                );

            case TargetPathType.Directory when !Directory.Exists(payloadPath):
                throw new DirectoryNotFoundException(
                    $"Backup payload directory for target '{manifestEntry.TargetId}' was not found: '{payloadPath}'."
                );
        }

        var payloadInfo = manifestEntry.Type == TargetPathType.File
            ? (FileSystemInfo)new FileInfo(payloadPath)
            : new DirectoryInfo(payloadPath);

        ThrowIfReparsePoint(payloadInfo);
    }

    #endregion

    #region Restore Operations

    /// <summary>
    /// Restores a validated target and returns its operation summary.
    /// </summary>
    /// <param name="planEntry">The validated restore-plan entry.</param>
    /// <returns>A result entry describing the restored target.</returns>
    private static RestoreResultEntry RestoreTarget(RestorePlanEntry planEntry)
    {
        var copyResult = planEntry.CurrentTarget.Type switch
        {
            TargetPathType.File => RestoreFile(
                planEntry.PayloadPath,
                planEntry.DestinationPath
            ),
            TargetPathType.Directory => RestoreDirectory(
                planEntry.PayloadPath,
                planEntry.DestinationPath
            ),
            _ => throw new InvalidOperationException(
                $"Target '{planEntry.CurrentTarget.Id}' has an unsupported target type '{planEntry.CurrentTarget.Type}'."
            )
        };

        return new RestoreResultEntry
        {
            TargetId = planEntry.CurrentTarget.Id,
            TargetName = planEntry.CurrentTarget.Name,
            DestinationPath = planEntry.DestinationPath,
            Type = planEntry.CurrentTarget.Type,
            FileCount = copyResult.FileCount,
            TotalBytes = copyResult.TotalBytes
        };
    }

    /// <summary>
    /// Restores a single file and overwrites an existing destination file.
    /// </summary>
    /// <param name="payloadPath">The absolute path of the backed-up file.</param>
    /// <param name="destinationPath">The current configured destination path.</param>
    /// <returns>The number of files and bytes restored.</returns>
    private static CopyResult RestoreFile(
        string payloadPath,
        string destinationPath
    )
    {
        var payloadFile = new FileInfo(payloadPath);

        ThrowIfReparsePoint(payloadFile);

        var destinationFile = new FileInfo(destinationPath);

        if (destinationFile.Exists)
        {
            ThrowIfReparsePoint(destinationFile);
        }

        var destinationDirectoryPath = Path.GetDirectoryName(destinationPath);

        if (!string.IsNullOrWhiteSpace(destinationDirectoryPath))
        {
            EnsureExistingPathContainsNoReparsePoints(destinationDirectoryPath);
            Directory.CreateDirectory(destinationDirectoryPath);
        }

        File.Copy(payloadPath, destinationPath, overwrite: true);

        return new CopyResult(
            FileCount: 1,
            TotalBytes: payloadFile.Length
        );
    }

    /// <summary>
    /// Restores a directory while preserving unrelated files currently present in the destination.
    /// </summary>
    /// <param name="payloadPath">The absolute path of the backed-up directory.</param>
    /// <param name="destinationPath">The current configured destination directory.</param>
    /// <returns>The number of files and bytes restored.</returns>
    private static CopyResult RestoreDirectory(
        string payloadPath,
        string destinationPath
    )
    {
        var payloadDirectory = new DirectoryInfo(payloadPath);

        ThrowIfReparsePoint(payloadDirectory);
        EnsureExistingPathContainsNoReparsePoints(destinationPath);

        Directory.CreateDirectory(destinationPath);

        long fileCount = 0;
        long totalBytes = 0;

        RestoreDirectoryContents(
            payloadDirectory,
            destinationPath,
            ref fileCount,
            ref totalBytes
        );

        return new CopyResult(fileCount, totalBytes);
    }

    /// <summary>
    /// Recursively restores directory contents and accumulates restored file-count and byte-count information.
    /// </summary>
    /// <param name="payloadDirectory">The backup directory currently being restored.</param>
    /// <param name="destinationPath">The corresponding current destination directory.</param>
    /// <param name="fileCount">The accumulated number of restored files.</param>
    /// <param name="totalBytes">The accumulated size, in bytes, of restored files.</param>
    private static void RestoreDirectoryContents(
        DirectoryInfo payloadDirectory,
        string destinationPath,
        ref long fileCount,
        ref long totalBytes
    )
    {
        foreach (var payloadFile in payloadDirectory.EnumerateFiles())
        {
            ThrowIfReparsePoint(payloadFile);

            var destinationFilePath = Path.Combine(destinationPath, payloadFile.Name);
            var destinationFile = new FileInfo(destinationFilePath);

            if (destinationFile.Exists)
            {
                ThrowIfReparsePoint(destinationFile);
            }

            payloadFile.CopyTo(destinationFilePath, overwrite: true);

            fileCount++;
            totalBytes += payloadFile.Length;
        }

        foreach (var childPayloadDirectory in payloadDirectory.EnumerateDirectories())
        {
            ThrowIfReparsePoint(childPayloadDirectory);

            var childDestinationPath = Path.Combine(destinationPath, childPayloadDirectory.Name);

            EnsureExistingPathContainsNoReparsePoints(childDestinationPath);
            Directory.CreateDirectory(childDestinationPath);

            RestoreDirectoryContents(
                childPayloadDirectory,
                childDestinationPath,
                ref fileCount,
                ref totalBytes
            );
        }
    }

    #endregion

    #region Path Safety

    /// <summary>
    /// Resolves and validates a selected backup name beneath an instance's backups directory.
    /// </summary>
    /// <param name="backupsPath">The absolute backups-directory path.</param>
    /// <param name="backupName">The selected backup directory name.</param>
    /// <returns>The validated absolute backup-directory path.</returns>
    private static string ResolveBackupPath(
        string backupsPath,
        string backupName
    )
    {
        if (Path.IsPathRooted(backupName) || Path.GetFileName(backupName) != backupName)
        {
            throw new InvalidDataException(
                $"Backup name '{backupName}' is not a valid backup directory name."
            );
        }

        var resolvedBackupPath = Path.GetFullPath(
            backupName,
            backupsPath
        );

        EnsurePathIsWithinDirectory(
            resolvedBackupPath,
            backupsPath,
            "Selected backup"
        );

        return resolvedBackupPath;
    }

    /// <summary>
    /// Resolves and validates a manifest payload path beneath a completed backup directory.
    /// </summary>
    /// <param name="backupPath">The absolute completed-backup path.</param>
    /// <param name="relativePayloadPath">The relative payload path recorded in the manifest.</param>
    /// <param name="targetId">The target identifier used in validation errors.</param>
    /// <returns>The validated absolute payload path.</returns>
    private static string ResolvePayloadPath(
        string backupPath,
        string relativePayloadPath,
        string targetId
    )
    {
        if (Path.IsPathRooted(relativePayloadPath))
        {
            throw new InvalidDataException(
                $"Backup target '{targetId}' contains a rooted payload path."
            );
        }

        var payloadPath = Path.GetFullPath(
            relativePayloadPath,
            backupPath
        );

        EnsurePathIsWithinDirectory(
            payloadPath,
            backupPath,
            $"Backup target '{targetId}'"
        );

        return payloadPath;
    }

    /// <summary>
    /// Ensures that a resolved path is located beneath a required parent directory.
    /// </summary>
    /// <param name="candidatePath">The resolved candidate path.</param>
    /// <param name="parentPath">The directory that must contain the candidate.</param>
    /// <param name="description">The description used when reporting an unsafe path.</param>
    private static void EnsurePathIsWithinDirectory(
        string candidatePath,
        string parentPath,
        string description
    )
    {
        var normalizedCandidate = Path.GetFullPath(candidatePath);
        var normalizedParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parentPath));
        var parentWithSeparator = normalizedParent + Path.DirectorySeparatorChar;

        if (!normalizedCandidate.StartsWith(parentWithSeparator, GetPathComparison()))
        {
            throw new InvalidDataException(
                $"{description} escapes its required parent directory."
            );
        }
    }

    /// <summary>
    /// Determines whether two filesystem paths are equal or whether either contains the other.
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
    /// <returns>
    /// <see langword="true"/> when the candidate is equal to or beneath the parent; otherwise,
    /// <see langword="false"/>.
    /// </returns>
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
    /// Normalizes a directory path and ensures that it ends with a directory separator.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized absolute directory path.</returns>
    private static string NormalizeDirectoryPath(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path))
               + Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// Ensures that every existing component of a destination path is a normal filesystem entry.
    /// </summary>
    /// <param name="path">The destination path whose existing components will be inspected.</param>
    /// <exception cref="IOException">Thrown when an existing component is a symbolic link or junction.</exception>
    private static void EnsureExistingPathContainsNoReparsePoints(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var existingPath = fullPath;

        while (!Path.Exists(existingPath))
        {
            var parentPath = Path.GetDirectoryName(existingPath);

            if (string.IsNullOrWhiteSpace(parentPath) || parentPath == existingPath)
            {
                return;
            }

            existingPath = parentPath;
        }

        var existingEntry = Directory.Exists(existingPath)
            ? (FileSystemInfo)new DirectoryInfo(existingPath)
            : new FileInfo(existingPath);

        ThrowIfReparsePoint(existingEntry);
    }

    /// <summary>
    /// Throws an exception when a filesystem entry is a symbolic link, junction, or another reparse-point type.
    /// </summary>
    /// <param name="entry">The filesystem entry to inspect.</param>
    /// <exception cref="IOException">Thrown when <paramref name="entry"/> is a reparse point.</exception>
    private static void ThrowIfReparsePoint(FileSystemInfo entry)
    {
        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException(
                $"Symbolic links and junctions are not currently supported: '{entry.FullName}'."
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
    /// Describes a validated target operation that is ready to be restored.
    /// </summary>
    /// <param name="CurrentTarget">The target from the current instance configuration.</param>
    /// <param name="ManifestEntry">The historical manifest entry describing the stored payload.</param>
    /// <param name="PayloadPath">The absolute path of the stored backup payload.</param>
    /// <param name="DestinationPath">The current absolute destination path.</param>
    private sealed record RestorePlanEntry(
        TargetPath CurrentTarget,
        BackupManifestEntry ManifestEntry,
        string PayloadPath,
        string DestinationPath
    );

    /// <summary>
    /// Contains aggregate information about files restored for a target.
    /// </summary>
    /// <param name="FileCount">The number of restored files.</param>
    /// <param name="TotalBytes">The combined size, in bytes, of the restored files.</param>
    private readonly record struct CopyResult(
        long FileCount,
        long TotalBytes
    );

    #endregion
}