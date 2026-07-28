using InstanceBackupManager.Processing.Enums;

namespace InstanceBackupManager.Processing.Models.Backups;

/// <summary>
/// Describes a completed backup and the targets stored within it.
/// </summary>
public sealed class BackupManifest
{
    #region Properties

    /// <summary>
    /// Gets the version of the backup-manifest schema.
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Gets the display name of the instance at the time the backup was created.
    /// </summary>
    public required string InstanceName { get; init; }

    /// <summary>
    /// Gets the optional user-facing name assigned to the backup.
    /// </summary>
    /// <remarks>
    /// A null value identifies a manifest created before display names were introduced.
    /// </remarks>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the directory name assigned to the backup.
    /// </summary>
    public required string BackupName { get; init; }

    /// <summary>
    /// Gets the reason the backup was created.
    /// </summary>
    /// <remarks>
    /// Manifests created before this property was introduced are treated as manual backups.
    /// </remarks>
    public BackupKind Kind { get; init; } = BackupKind.Manual;

    /// <summary>
    /// Gets the UTC date and time at which the backup operation began.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; init; }

    /// <summary>
    /// Gets information about each target included in the backup.
    /// </summary>
    public required IReadOnlyCollection<BackupManifestEntry> Entries { get; init; }

    #endregion
}