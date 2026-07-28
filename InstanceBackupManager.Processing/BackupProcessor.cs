using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using InstanceBackupManager.Processing.Constants;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Backups;
using InstanceBackupManager.Processing.Models.Configuration;
using InstanceBackupManager.Processing.Models.Instances;
using InstanceBackupManager.Processing.Strategies;
using InstanceBackupManager.Processing.Utilities;

namespace InstanceBackupManager.Processing;

/// <summary>
/// Coordinates creation of complete, timestamped backups for loaded instances.
/// </summary>
public sealed class BackupProcessor
{
    #region Properties

    /// <summary>
    /// Gets the strategies used to back up configured target types.
    /// </summary>
    private IReadOnlyCollection<IBackupTargetStrategy> BackupStrategies { get; }

    /// <summary>
    /// Gets the time provider used to determine when backups are created.
    /// </summary>
    private TimeProvider TimeProvider { get; }

    /// <summary>
    /// Gets the serializer options used when writing backup manifests.
    /// </summary>
    private JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new backup processor using the default strategies and system time provider.
    /// </summary>
    public BackupProcessor()
        : this(TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a new backup processor using the default strategies and specified time provider.
    /// </summary>
    /// <param name="timeProvider">The time provider used when assigning backup timestamps.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="timeProvider"/> is null.</exception>
    public BackupProcessor(TimeProvider timeProvider)
        : this(
            timeProvider,
            CreateDefaultStrategies()
        )
    {
    }

    /// <summary>
    /// Initializes a new backup processor using the specified time provider and strategies.
    /// </summary>
    /// <param name="timeProvider">The time provider used when assigning backup timestamps.</param>
    /// <param name="backupStrategies">The strategies used to back up configured target types.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="timeProvider"/> or <paramref name="backupStrategies"/> is null.
    /// </exception>
    internal BackupProcessor(
        TimeProvider timeProvider,
        IReadOnlyCollection<IBackupTargetStrategy> backupStrategies
    )
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(backupStrategies);

        TimeProvider = timeProvider;
        BackupStrategies = backupStrategies;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Creates a timestamped backup containing every enabled target in the specified instance.
    /// </summary>
    /// <param name="instance">The loaded instance to back up.</param>
    /// <param name="kind">The reason the backup is being created.</param>
    /// <returns>A manifest describing the completed backup.</returns>
    public BackupManifest CreateBackup(
        InstanceContext instance,
        BackupKind kind = BackupKind.Manual
    )
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "The backup kind is not supported."
            );
        }

        if (!instance.Config.Enabled)
        {
            throw new InvalidOperationException(
                $"Instance '{instance.Config.Name}' is disabled."
            );
        }

        var enabledTargets = instance.Config.Targets
            .Where(target => target.Enabled)
            .ToList();

        if (enabledTargets.Count == 0)
        {
            throw new InvalidOperationException(
                $"Instance '{instance.Config.Name}' does not contain any enabled targets."
            );
        }

        Directory.CreateDirectory(instance.BackupsPath);

        var createdUtc = TimeProvider.GetUtcNow();
        var backupName = CreateUniqueBackupName(
            instance.BackupsPath,
            createdUtc
        );

        var completedBackupPath = Path.Combine(
            instance.BackupsPath,
            backupName
        );

        var temporaryBackupPath = Path.Combine(
            instance.BackupsPath,
            $"{BackupStorageConstants.InProgressDirectoryPrefix}{Guid.NewGuid():N}"
        );

        Directory.CreateDirectory(temporaryBackupPath);

        try
        {
            var entries = new List<BackupManifestEntry>();

            foreach (var target in enabledTargets)
            {
                var entry = BackupTarget(
                    target,
                    instance.InstancePath,
                    temporaryBackupPath
                );

                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }

            if (entries.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Instance '{instance.Config.Name}' does not contain any available targets to back up."
                );
            }

            var manifest = new BackupManifest
            {
                SchemaVersion = BackupStorageConstants.SupportedManifestSchemaVersion,
                InstanceName = instance.Config.Name,
                BackupName = backupName,
                Kind = kind,
                CreatedUtc = createdUtc,
                Entries = entries.AsReadOnly()
            };

            WriteManifest(
                temporaryBackupPath,
                manifest
            );

            /*
             * The temporary and completed directories share the same parent. Moving the temporary directory prevents an
             * incomplete operation from appearing as a completed timestamped backup.
             */
            Directory.Move(
                temporaryBackupPath,
                completedBackupPath
            );

            return manifest;
        }
        catch
        {
            TryDeleteDirectory(temporaryBackupPath);
            throw;
        }
    }

    #endregion

    #region Backup Coordination

    /// <summary>
    /// Resolves and executes the backup strategy for one configured target.
    /// </summary>
    /// <param name="target">The enabled target to back up.</param>
    /// <param name="instancePath">The instance directory used to resolve relative source paths.</param>
    /// <param name="temporaryBackupPath">The temporary directory containing the in-progress backup.</param>
    /// <returns>
    /// A manifest entry describing the copied target, or <see langword="null"/> when an optional source does not exist.
    /// </returns>
    private BackupManifestEntry? BackupTarget(
        TargetPath target,
        string instancePath,
        string temporaryBackupPath
    )
    {
        var strategy = TargetPathStrategyResolver.Resolve(
            BackupStrategies,
            target.Type
        );

        var sourcePath = PathResolver.ResolveSourcePath(
            target.Source,
            instancePath
        );

        if (!strategy.SourceExists(sourcePath))
        {
            if (!target.Required)
            {
                return null;
            }

            ThrowMissingSourceException(
                target,
                sourcePath
            );
        }

        var destinationPath = Path.GetFullPath(
            target.BackupPath,
            temporaryBackupPath
        );

        FileSystemSafety.EnsurePathIsWithinDirectory(
            destinationPath,
            temporaryBackupPath,
            $"Target '{target.Id}' backup path"
        );

        var statistics = strategy.Backup(
            sourcePath,
            destinationPath
        );

        return new BackupManifestEntry
        {
            TargetId = target.Id,
            TargetName = target.Name,
            Source = target.Source,
            Type = target.Type,
            BackupPath = target.BackupPath,
            FileCount = statistics.FileCount,
            TotalBytes = statistics.TotalBytes
        };
    }

    /// <summary>
    /// Throws the appropriate missing-source exception for a required target.
    /// </summary>
    /// <param name="target">The required target whose source does not exist.</param>
    /// <param name="sourcePath">The resolved absolute source path.</param>
    private static void ThrowMissingSourceException(
        TargetPath target,
        string sourcePath
    )
    {
        switch (target.Type)
        {
            case TargetPathType.File:
                throw new FileNotFoundException(
                    $"Required source file for target '{target.Id}' was not found.",
                    sourcePath
                );

            case TargetPathType.Directory:
                throw new DirectoryNotFoundException(
                    $"Required source directory for target '{target.Id}' was not found: '{sourcePath}'."
                );

            default:
                throw new InvalidOperationException(
                    $"Target '{target.Id}' has an unsupported target type '{target.Type}'."
                );
        }
    }

    #endregion

    #region Manifest Operations

    /// <summary>
    /// Writes the completed backup manifest into the temporary backup directory.
    /// </summary>
    /// <param name="temporaryBackupPath">The temporary directory containing the copied targets.</param>
    /// <param name="manifest">The completed manifest to serialize.</param>
    private void WriteManifest(
        string temporaryBackupPath,
        BackupManifest manifest
    )
    {
        var manifestPath = Path.Combine(
            temporaryBackupPath,
            BackupStorageConstants.ManifestFileName
        );

        var json = JsonSerializer.Serialize(
            manifest,
            JsonOptions
        );

        File.WriteAllText(
            manifestPath,
            json
        );
    }

    #endregion

    #region Backup Naming

    /// <summary>
    /// Creates a unique, sortable directory name for a backup.
    /// </summary>
    /// <param name="backupsPath">The directory containing completed backups.</param>
    /// <param name="createdUtc">The UTC date and time assigned to the backup.</param>
    /// <returns>A directory name that does not currently exist beneath <paramref name="backupsPath"/>.</returns>
    private static string CreateUniqueBackupName(
        string backupsPath,
        DateTimeOffset createdUtc
    )
    {
        var baseName = createdUtc.ToString(
            "yyyy-MM-dd_HH-mm-ss-fff'Z'",
            CultureInfo.InvariantCulture
        );

        var candidateName = baseName;
        var suffix = 1;

        while (Directory.Exists(Path.Combine(backupsPath, candidateName))
               || File.Exists(Path.Combine(backupsPath, candidateName)))
        {
            candidateName = $"{baseName}-{suffix:D2}";
            suffix++;
        }

        return candidateName;
    }

    #endregion

    #region Strategy Creation

    /// <summary>
    /// Creates the default strategies used to back up supported target types.
    /// </summary>
    /// <returns>A read-only collection containing one strategy for each supported target type.</returns>
    private static IReadOnlyCollection<IBackupTargetStrategy> CreateDefaultStrategies()
    {
        return
        [
            new FileTargetStrategy(),
            new DirectoryTargetStrategy()
        ];
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// Attempts to remove an incomplete temporary backup without masking the original failure.
    /// </summary>
    /// <param name="directoryPath">The temporary backup directory to remove.</param>
    private static void TryDeleteDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(
                    directoryPath,
                    recursive: true
                );
            }
        }
        catch
        {
            /*
             * Cleanup is best-effort. A cleanup failure must not replace the original exception, which contains more
             * useful information about why backup creation failed.
             */
        }
    }

    #endregion
}