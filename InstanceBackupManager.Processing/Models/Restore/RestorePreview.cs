namespace InstanceBackupManager.Processing.Models.Restore;

/// <summary>
/// Describes every currently restorable target in a completed backup without changing destination files.
/// </summary>
public sealed class RestorePreview
{
    public required string BackupName { get; init; }

    public required IReadOnlyCollection<RestoreTargetPreview> Targets { get; init; }
}
