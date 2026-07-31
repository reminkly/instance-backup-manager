using InstanceBackupManager.Processing.Enums;

namespace InstanceBackupManager.Processing.Models.Restore;

/// <summary>
/// Describes the non-mutating restore comparison for one configured target.
/// </summary>
public sealed class RestoreTargetPreview
{
    public required string TargetId { get; init; }

    public required string TargetName { get; init; }

    public TargetPathType Type { get; init; }

    public required string DestinationPath { get; init; }

    public required IReadOnlyCollection<RestorePreviewFile> Files { get; init; }

    public long CreateCount => Files.LongCount(file => file.ChangeKind == RestoreFileChangeKind.Create);

    public long OverwriteCount => Files.LongCount(file => file.ChangeKind == RestoreFileChangeKind.Overwrite);

    public long UnchangedCount => Files.LongCount(file => file.ChangeKind == RestoreFileChangeKind.Unchanged);

    public long PreserveCount => Files.LongCount(file => file.ChangeKind == RestoreFileChangeKind.Preserve);
}
