using InstanceBackupManager.Processing.Enums;

namespace InstanceBackupManager.Processing.Models.Updates;

/// <summary>
/// Describes the result of comparing the installed application with the latest published release.
/// </summary>
public sealed class UpdateCheckResult
{
    #region Properties

    /// <summary>
    /// Gets the update comparison result.
    /// </summary>
    public UpdateCheckStatus Status { get; init; }

    /// <summary>
    /// Gets the installed application version.
    /// </summary>
    public required Version InstalledVersion { get; init; }

    /// <summary>
    /// Gets the parsed version of the latest published release.
    /// </summary>
    public required Version LatestVersion { get; init; }

    /// <summary>
    /// Gets the latest published release metadata.
    /// </summary>
    public required ReleaseInfo LatestRelease { get; init; }

    #endregion
}
