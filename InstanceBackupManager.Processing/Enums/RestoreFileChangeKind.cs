namespace InstanceBackupManager.Processing.Enums;

/// <summary>
/// Describes how a stored backup file compares with the current restore destination.
/// </summary>
public enum RestoreFileChangeKind
{
    Create,
    Overwrite,
    Unchanged,
    Preserve
}
