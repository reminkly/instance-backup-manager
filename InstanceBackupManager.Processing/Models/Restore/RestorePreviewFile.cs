using InstanceBackupManager.Processing.Enums;

namespace InstanceBackupManager.Processing.Models.Restore;

/// <summary>
/// Describes one file discovered while comparing a stored target with its current destination.
/// </summary>
public sealed class RestorePreviewFile
{
    public required string RelativePath { get; init; }

    public RestoreFileChangeKind ChangeKind { get; init; }

    public long BackupBytes { get; init; }

    public long? CurrentBytes { get; init; }
}
