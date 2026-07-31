using InstanceBackupManager.Processing.Catalogs;
using InstanceBackupManager.Processing.Models.Backups;
using InstanceBackupManager.Processing.Models.Configuration;
using InstanceBackupManager.Processing.Models.Instances;
using InstanceBackupManager.Processing.Models.Restore;
using InstanceBackupManager.Processing.Strategies;
using InstanceBackupManager.Processing.Utilities;

namespace InstanceBackupManager.Processing;

/// <summary>
/// Coordinates restoration of completed backups to destinations defined by the current instance configuration.
/// </summary>
public sealed class RestoreProcessor
{
    #region Properties

    /// <summary>
    /// Gets the catalog used to locate and validate completed backups.
    /// </summary>
    private BackupCatalog BackupCatalog { get; }

    /// <summary>
    /// Gets the strategies used to restore supported target types.
    /// </summary>
    private IReadOnlyCollection<IRestoreTargetStrategy> RestoreStrategies { get; }

    /// <summary>
    /// Gets the time provider used to record restore completion times.
    /// </summary>
    private TimeProvider TimeProvider { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a restore processor using default dependencies.
    /// </summary>
    public RestoreProcessor()
        : this(
            new BackupCatalog(),
            TimeProvider.System
        )
    {
    }

    /// <summary>
    /// Initializes a restore processor using the specified time provider.
    /// </summary>
    /// <param name="timeProvider">The time provider used to record restore completion times.</param>
    public RestoreProcessor(TimeProvider timeProvider)
        : this(
            new BackupCatalog(),
            timeProvider
        )
    {
    }

    /// <summary>
    /// Initializes a restore processor using the specified catalog and time provider.
    /// </summary>
    /// <param name="backupCatalog">The catalog used to load completed backups.</param>
    /// <param name="timeProvider">The time provider used to record restore completion times.</param>
    public RestoreProcessor(
        BackupCatalog backupCatalog,
        TimeProvider timeProvider
    )
        : this(
            backupCatalog,
            timeProvider,
            CreateDefaultStrategies()
        )
    {
    }

    /// <summary>
    /// Initializes a restore processor using the specified dependencies and strategies.
    /// </summary>
    /// <param name="backupCatalog">The catalog used to load completed backups.</param>
    /// <param name="timeProvider">The time provider used to record restore completion times.</param>
    /// <param name="restoreStrategies">The strategies used to restore supported target types.</param>
    internal RestoreProcessor(
        BackupCatalog backupCatalog,
        TimeProvider timeProvider,
        IReadOnlyCollection<IRestoreTargetStrategy> restoreStrategies
    )
    {
        ArgumentNullException.ThrowIfNull(backupCatalog);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(restoreStrategies);

        BackupCatalog = backupCatalog;
        TimeProvider = timeProvider;
        RestoreStrategies = restoreStrategies;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Restores a completed backup using destinations from the current instance configuration.
    /// </summary>
    /// <param name="instance">The instance receiving the restored data.</param>
    /// <param name="backupName">The directory name of the backup to restore.</param>
    /// <returns>A summary of the completed restore operation.</returns>
    public RestoreResult RestoreBackup(
        InstanceContext instance,
        string backupName
    )
    {
        return RestoreBackup(
            instance,
            backupName,
            targetIds: null
        );
    }

    /// <summary>
    /// Restores selected targets from a completed backup using destinations from the current instance configuration.
    /// </summary>
    /// <param name="instance">The instance receiving the restored data.</param>
    /// <param name="backupName">The directory name of the backup to restore.</param>
    /// <param name="targetIds">The target identifiers to restore, or <see langword="null"/> to restore every enabled target.</param>
    /// <returns>A summary of the completed restore operation.</returns>
    public RestoreResult RestoreBackup(
        InstanceContext instance,
        string backupName,
        IReadOnlyCollection<string>? targetIds
    )
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupName);

        if (!instance.Config.Enabled)
        {
            throw new InvalidOperationException(
                $"Instance '{instance.Config.Name}' is disabled."
            );
        }

        var backup = BackupCatalog.GetCompletedBackup(
            instance,
            backupName
        );

        /*
         * Resolve and validate every strategy and payload before modifying any destination. This prevents a later invalid
         * target from leaving an earlier target partially restored.
         */
        var selectedTargetIds = targetIds?.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (selectedTargetIds is { Count: 0 })
        {
            throw new ArgumentException(
                "At least one target must be selected for restoration.",
                nameof(targetIds)
            );
        }

        var restorePlan = CreateRestorePlan(
            instance,
            backup.Manifest,
            backup.BackupPath,
            selectedTargetIds
        );

        if (restorePlan.Count == 0)
        {
            throw new InvalidOperationException(
                $"Backup '{backup.BackupName}' does not contain any targets that are currently enabled."
            );
        }

        var resultEntries = restorePlan
            .Select(RestoreTarget)
            .ToList()
            .AsReadOnly();

        return new RestoreResult
        {
            BackupName = backup.BackupName,
            CompletedUtc = TimeProvider.GetUtcNow(),
            Entries = resultEntries
        };
    }

    #endregion

    #region Restore Planning

    /// <summary>
    /// Creates and validates every target operation required to restore a backup.
    /// </summary>
    /// <param name="instance">The current instance configuration and runtime paths.</param>
    /// <param name="manifest">The manifest describing the selected backup.</param>
    /// <param name="backupPath">The absolute path of the selected backup.</param>
    /// <returns>A read-only collection of validated restore-plan entries.</returns>
    private IReadOnlyCollection<RestorePlanEntry> CreateRestorePlan(
        InstanceContext instance,
        BackupManifest manifest,
        string backupPath,
        IReadOnlySet<string>? selectedTargetIds
    )
    {
        var planEntries = new List<RestorePlanEntry>();

        foreach (var manifestEntry in manifest.Entries)
        {
            if (selectedTargetIds is not null
                && !selectedTargetIds.Contains(manifestEntry.TargetId))
            {
                continue;
            }

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

            var strategy = TargetPathStrategyResolver.Resolve(
                RestoreStrategies,
                manifestEntry.Type
            );

            var payloadPath = ResolvePayloadPath(
                backupPath,
                manifestEntry.BackupPath,
                manifestEntry.TargetId
            );

            strategy.ValidatePayload(payloadPath);

            var destinationPath = PathResolver.ResolveSourcePath(
                currentTarget.Source,
                instance.InstancePath
            );

            if (FileSystemSafety.PathsOverlap(
                destinationPath,
                instance.BackupsPath
            ))
            {
                throw new InvalidDataException(
                    $"Current destination for target '{currentTarget.Id}' overlaps the instance backups directory."
                );
            }

            planEntries.Add(
                new RestorePlanEntry(
                    CurrentTarget: currentTarget,
                    Strategy: strategy,
                    PayloadPath: payloadPath,
                    DestinationPath: destinationPath
                )
            );
        }

        if (selectedTargetIds is not null)
        {
            var plannedTargetIds = planEntries
                .Select(entry => entry.CurrentTarget.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missingTargetIds = selectedTargetIds
                .Where(targetId => !plannedTargetIds.Contains(targetId))
                .ToList();

            if (missingTargetIds.Count > 0)
            {
                throw new InvalidDataException(
                    "The following selected targets are not currently restorable: " + string.Join(", ", missingTargetIds)
                );
            }
        }

        return planEntries.AsReadOnly();
    }

    #endregion

    #region Restore Execution

    /// <summary>
    /// Executes one validated restore-plan entry.
    /// </summary>
    /// <param name="planEntry">The validated target operation.</param>
    /// <returns>A result describing the restored target.</returns>
    private static RestoreResultEntry RestoreTarget(RestorePlanEntry planEntry)
    {
        var statistics = planEntry.Strategy.Restore(
            planEntry.PayloadPath,
            planEntry.DestinationPath
        );

        return new RestoreResultEntry
        {
            TargetId = planEntry.CurrentTarget.Id,
            TargetName = planEntry.CurrentTarget.Name,
            DestinationPath = planEntry.DestinationPath,
            Type = planEntry.CurrentTarget.Type,
            FileCount = statistics.FileCount,
            TotalBytes = statistics.TotalBytes
        };
    }

    #endregion

    #region Path Resolution

    /// <summary>
    /// Resolves and validates a manifest payload path beneath its completed backup directory.
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

    #region Strategy Creation

    /// <summary>
    /// Creates the default strategies used to restore supported target types.
    /// </summary>
    /// <returns>A read-only collection containing one strategy for each supported target type.</returns>
    private static IReadOnlyCollection<IRestoreTargetStrategy> CreateDefaultStrategies()
    {
        return
        [
            new FileTargetStrategy(),
            new DirectoryTargetStrategy()
        ];
    }

    #endregion

    #region Private Types

    /// <summary>
    /// Describes a validated target operation ready for restoration.
    /// </summary>
    /// <param name="CurrentTarget">The target from the current instance configuration.</param>
    /// <param name="Strategy">The strategy used to restore the target.</param>
    /// <param name="PayloadPath">The absolute stored-payload path.</param>
    /// <param name="DestinationPath">The absolute current destination path.</param>
    private sealed record RestorePlanEntry(
        TargetPath CurrentTarget,
        IRestoreTargetStrategy Strategy,
        string PayloadPath,
        string DestinationPath
    );

    #endregion
}
