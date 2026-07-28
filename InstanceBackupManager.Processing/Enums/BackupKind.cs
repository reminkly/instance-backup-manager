namespace InstanceBackupManager.Processing.Enums;

/// <summary>
/// Identifies the reason a backup was created.
/// </summary>
public enum BackupKind
{
    /// <summary>
    /// Indicates that the user explicitly requested the backup.
    /// </summary>
    Manual = 0,

    /// <summary>
    /// Indicates that the backup was created automatically before restoring another backup.
    /// </summary>
    PreRestore = 1
}