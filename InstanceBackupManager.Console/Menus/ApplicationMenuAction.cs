namespace InstanceBackupManager.Console.Menus;

/// <summary>
/// Identifies an action selected from the application-level instance menu.
/// </summary>
internal enum ApplicationMenuAction
{
    /// <summary>
    /// Opens a discovered instance.
    /// </summary>
    OpenInstance,

    /// <summary>
    /// Starts the new-instance workflow.
    /// </summary>
    CreateInstance,

    /// <summary>
    /// Checks GitHub for a newer published release.
    /// </summary>
    CheckForUpdates
}
