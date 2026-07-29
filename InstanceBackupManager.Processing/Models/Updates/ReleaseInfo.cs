namespace InstanceBackupManager.Processing.Models.Updates;

/// <summary>
/// Describes the latest published release returned by an external release source.
/// </summary>
public sealed class ReleaseInfo
{
    #region Properties

    /// <summary>
    /// Gets the release tag used to identify the published version.
    /// </summary>
    public required string TagName { get; init; }

    /// <summary>
    /// Gets the user-facing release title.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the webpage where the release can be reviewed and downloaded.
    /// </summary>
    public required Uri PageUri { get; init; }

    /// <summary>
    /// Gets the optional release notes supplied by the publisher.
    /// </summary>
    public string? Notes { get; init; }

    #endregion
}
