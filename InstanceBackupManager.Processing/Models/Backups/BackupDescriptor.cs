namespace InstanceBackupManager.Processing.Models.Backups;

/// <summary>
/// Describes a completed backup discovered within an instance's backups directory.
/// </summary>
/// <remarks>
/// This model is used at runtime for backup selection and is not serialized into the backup manifest.
/// </remarks>
public sealed class BackupDescriptor
{
    #region Properties

    /// <summary>
    /// Gets the directory name assigned to the backup.
    /// </summary>
    public required string BackupName { get; init; }

    /// <summary>
    /// Gets the absolute path of the completed backup directory.
    /// </summary>
    public required string BackupPath { get; init; }

    /// <summary>
    /// Gets the manifest loaded from the backup directory.
    /// </summary>
    public required BackupManifest Manifest { get; init; }

    #endregion
}