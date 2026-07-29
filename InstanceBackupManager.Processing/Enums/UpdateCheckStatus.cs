namespace InstanceBackupManager.Processing.Enums;

/// <summary>
/// Identifies whether the installed application is current or a newer release is available.
/// </summary>
public enum UpdateCheckStatus
{
    /// <summary>
    /// The installed version is the same as or newer than the latest published release.
    /// </summary>
    UpToDate,

    /// <summary>
    /// A newer published release is available.
    /// </summary>
    UpdateAvailable
}
