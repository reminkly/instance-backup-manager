using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.BackupMaintenance;
using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Processing;

/// <summary>
/// Applies configured per-kind retention limits to validated, completed backups.
/// </summary>
public sealed class BackupRetentionProcessor
{
    #region Properties

    /// <summary>
    /// Gets the processor used to discover completed backups.
    /// </summary>
    private RestoreProcessor RestoreProcessor { get; }

    /// <summary>
    /// Gets the processor used to batch-delete completed backups that exceed a retention limit.
    /// </summary>
    private BackupMaintenanceProcessor BackupMaintenanceProcessor { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new backup-retention processor using default processing dependencies.
    /// </summary>
    public BackupRetentionProcessor()
        : this(
            new RestoreProcessor()
        )
    {
    }

    /// <summary>
    /// Initializes a new backup-retention processor using the specified restore processor.
    /// </summary>
    /// <param name="restoreProcessor">The processor used to discover completed backups.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="restoreProcessor"/> is null.</exception>
    public BackupRetentionProcessor(RestoreProcessor restoreProcessor)
        : this(
            restoreProcessor,
            new BackupMaintenanceProcessor(
                restoreProcessor,
                TimeProvider.System
            )
        )
    {
    }

    /// <summary>
    /// Initializes a new backup-retention processor using the specified processing dependencies.
    /// </summary>
    /// <param name="restoreProcessor">The processor used to discover completed backups.</param>
    /// <param name="backupMaintenanceProcessor">The processor used to delete backups that exceed a retention limit.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="restoreProcessor"/> or <paramref name="backupMaintenanceProcessor"/> is null.
    /// </exception>
    public BackupRetentionProcessor(
        RestoreProcessor restoreProcessor,
        BackupMaintenanceProcessor backupMaintenanceProcessor
    )
    {
        ArgumentNullException.ThrowIfNull(restoreProcessor);
        ArgumentNullException.ThrowIfNull(backupMaintenanceProcessor);

        RestoreProcessor = restoreProcessor;
        BackupMaintenanceProcessor = backupMaintenanceProcessor;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Applies the configured retention limit for one backup kind and deletes completed backups beyond that limit.
    /// </summary>
    /// <param name="instance">The loaded instance whose completed backups will be evaluated.</param>
    /// <param name="kind">The backup kind whose retention limit will be applied.</param>
    /// <returns>A deletion result describing backups removed by retention.</returns>
    public BackupDeletionResult ApplyRetention(
        InstanceContext instance,
        BackupKind kind
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

        var retentionLimit = GetRetentionLimit(
            instance,
            kind
        );

        if (retentionLimit is null)
        {
            return BackupMaintenanceProcessor.DeleteBackups(
                instance,
                []
            );
        }

        if (retentionLimit <= 0)
        {
            throw new InvalidOperationException(
                $"The configured retention limit for backup kind '{kind}' must be at least one."
            );
        }

        var backupNamesToDelete = RestoreProcessor
            .DiscoverBackups(instance)
            .Where(backup => backup.Manifest.Kind == kind)
            .OrderByDescending(backup => backup.Manifest.CreatedUtc)
            .ThenByDescending(
                backup => backup.BackupName,
                GetPathComparer()
            )
            .Skip(retentionLimit.Value)
            .Select(backup => backup.BackupName)
            .ToList()
            .AsReadOnly();

        return BackupMaintenanceProcessor.DeleteBackups(
            instance,
            backupNamesToDelete
        );
    }

    #endregion

    #region Retention Settings

    /// <summary>
    /// Gets the configured retention limit for the specified backup kind.
    /// </summary>
    /// <param name="instance">The loaded instance containing the retention settings.</param>
    /// <param name="kind">The backup kind whose limit will be returned.</param>
    /// <returns>The configured limit, or <see langword="null"/> when retention is unlimited.</returns>
    private static int? GetRetentionLimit(
        InstanceContext instance,
        BackupKind kind
    )
    {
        var retention = instance.Config.Retention;

        if (retention is null)
        {
            return null;
        }

        return kind switch
        {
            BackupKind.Manual => retention.ManualBackupsToKeep,
            BackupKind.PreRestore => retention.PreRestoreBackupsToKeep,
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "The backup kind is not supported."
            )
        };
    }

    #endregion

    #region Path Behavior

    /// <summary>
    /// Gets the appropriate path comparer for deterministic backup-name ordering.
    /// </summary>
    /// <returns>A case-insensitive comparer on Windows and a case-sensitive comparer on other operating systems.</returns>
    private static StringComparer GetPathComparer()
    {
        return OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }

    #endregion
}