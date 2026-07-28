namespace InstanceBackupManager.Processing.Models.Clear;

/// <summary>
/// Describes the result of a completed clear operation.
/// </summary>
public sealed class ClearResult
{
    #region Properties

    /// <summary>
    /// Gets the UTC date and time at which the clear operation completed.
    /// </summary>
    public DateTimeOffset CompletedUtc { get; init; }

    /// <summary>
    /// Gets information about each target processed by the clear operation.
    /// </summary>
    public required IReadOnlyCollection<ClearResultEntry> Entries { get; init; }

    #endregion
}