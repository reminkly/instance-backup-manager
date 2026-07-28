using System.Text.Json;
using System.Text.Json.Serialization;
using InstanceBackupManager.Processing.Constants;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Backups;
using InstanceBackupManager.Processing.Models.Instances;
using InstanceBackupManager.Processing.Utilities;

namespace InstanceBackupManager.Processing.Catalogs;

/// <summary>
/// Provides access to the completed backups stored for an instance.
/// </summary>
/// <remarks>
/// The catalog owns the storage rules for locating backup directories, loading manifests, and validating stored backup metadata.
/// </remarks>
public sealed class BackupCatalog
{
    #region Properties

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

            // Temporary directories represent incomplete operations and must never be offered as completed backups.
            if (backupName.StartsWith(
                BackupStorageConstants.InProgressDirectoryPrefix,
                StringComparison.OrdinalIgnoreCase
            ))
            {
                continue;
            }

            var manifestPath = Path.Combine(
                backupPath,
                BackupStorageConstants.ManifestFileName
            );

            // A directory without a manifest is not considered a completed backup.
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            backups.Add(
                LoadCompletedBackup(
                    backupPath,
                    backupName
                )
            );
        }

        var orderedBackups = backups
            .OrderByDescending(backup => backup.Manifest.CreatedUtc)
            .ThenByDescending(backup => backup.BackupName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return orderedBackups.AsReadOnly();
    }

    /// <summary>
    /// Loads a completed backup by its directory name.
    /// </summary>
    /// <param name="instance">The loaded instance containing the backup.</param>
    /// <param name="backupName">The directory name of the backup to load.</param>
    /// <returns>The completed backup and its validated manifest.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="instance"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="backupName"/> is null, empty, or consists only of whitespace.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the selected backup directory does not exist.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the selected backup does not contain a manifest.</exception>
    /// <exception cref="InvalidDataException">Thrown when the backup name or manifest is invalid.</exception>
    /// <exception cref="IOException">Thrown when the backup directory is a symbolic link, junction, or another reparse point.</exception>
    public BackupDescriptor GetCompletedBackup(
        InstanceContext instance,
        string backupName
    )
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupName);

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

        var manifestPath = Path.Combine(
            backupPath,
            BackupStorageConstants.ManifestFileName
        );

        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException(
                "The selected backup does not contain a manifest.",
                manifestPath
            );
        }

        return LoadCompletedBackup(
            backupPath,
            backupName
        );
    }

    #endregion

    #region Backup Loading

    /// <summary>
    /// Loads and validates a completed backup from its resolved directory.
    /// </summary>
    /// <param name="backupPath">The absolute path of the completed backup directory.</param>
    /// <param name="backupName">The directory name assigned to the backup.</param>
    /// <returns>The completed backup and its validated manifest.</returns>
    private BackupDescriptor LoadCompletedBackup(
        string backupPath,
        string backupName
    )
    {
        var backupDirectory = new DirectoryInfo(backupPath);

        FileSystemSafety.ThrowIfReparsePoint(backupDirectory);

        var manifestPath = Path.Combine(
            backupPath,
            BackupStorageConstants.ManifestFileName
        );

        var manifest = LoadManifest(manifestPath);

        ValidateManifest(
            manifest,
            backupName,
            backupPath
        );

        return new BackupDescriptor
        {
            BackupName = backupName,
            BackupPath = Path.GetFullPath(backupPath),
            Manifest = manifest
        };
    }

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

    #endregion

    #region Manifest Validation

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
        if (manifest.SchemaVersion != BackupStorageConstants.SupportedManifestSchemaVersion)
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

        if (!string.Equals(
            manifest.BackupName,
            backupName,
            FileSystemSafety.GetPathComparison()
        ))
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

        var resolvedPayloadPaths = new List<ResolvedPayloadPath>();

        foreach (var entry in manifest.Entries)
        {
            ValidateManifestEntry(
                entry,
                backupName
            );

            var payloadPath = ResolvePayloadPath(
                backupPath,
                entry.BackupPath,
                entry.TargetId
            );

            resolvedPayloadPaths.Add(
                new ResolvedPayloadPath(
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
    /// <exception cref="InvalidDataException">Thrown when the manifest entry fails validation.</exception>
    private static void ValidateManifestEntry(
        BackupManifestEntry entry,
        string backupName
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
    }

    /// <summary>
    /// Verifies that manifest payload paths do not overlap.
    /// </summary>
    /// <param name="payloadPaths">The resolved payload paths and their target identifiers.</param>
    /// <param name="backupName">The name of the backup being validated.</param>
    /// <exception cref="InvalidDataException">Thrown when two payload paths overlap.</exception>
    private static void ValidatePayloadPathConflicts(
        IReadOnlyList<ResolvedPayloadPath> payloadPaths,
        string backupName
    )
    {
        for (var firstIndex = 0; firstIndex < payloadPaths.Count; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < payloadPaths.Count; secondIndex++)
            {
                var firstPayload = payloadPaths[firstIndex];
                var secondPayload = payloadPaths[secondIndex];

                if (FileSystemSafety.PathsOverlap(
                    firstPayload.PayloadPath,
                    secondPayload.PayloadPath
                ))
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

        FileSystemSafety.EnsurePathIsWithinDirectory(
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

        FileSystemSafety.EnsurePathIsWithinDirectory(
            payloadPath,
            backupPath,
            $"Backup target '{targetId}'"
        );

        return payloadPath;
    }

    #endregion

    #region Private Types

    /// <summary>
    /// Associates a manifest target identifier with its resolved payload path.
    /// </summary>
    /// <param name="TargetId">The identifier of the manifest target.</param>
    /// <param name="PayloadPath">The absolute path of the target's stored payload.</param>
    private sealed record ResolvedPayloadPath(
        string TargetId,
        string PayloadPath
    );

    #endregion
}