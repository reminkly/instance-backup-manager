using InstanceBackupManager.Processing.Enums;

namespace InstanceBackupManager.Processing.Models.Clear;

/// <summary>
/// Describes a configured target processed by a clear operation.
/// </summary>
public sealed class ClearResultEntry
{
    #region Properties

    /// <summary>
    /// Gets the stable identifier of the cleared target.
    /// </summary>
    public required string TargetId { get; init; }

    /// <summary>
    /// Gets the display name of the cleared target.
    /// </summary>
    public required string TargetName { get; init; }

    /// <summary>
    /// Gets the absolute source path processed by the clear operation.
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// Gets the type of filesystem entry represented by the target.
    /// </summary>
    public TargetPathType Type { get; init; }

    /// <summary>
    /// Gets the number of files removed from the target.
    /// </summary>
    public long FileCount { get; init; }

    /// <summary>
    /// Gets the combined size, in bytes, of the removed files.
    /// </summary>
    public long TotalBytes { get; init; }

    #endregion
}