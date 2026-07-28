namespace InstanceBackupManager.Console.Constants;

/// <summary>
/// Defines recurring messages, prompts, and display labels used by the console application.
/// </summary>
internal static class ConsoleMessages
{
    #region Navigation

    /// <summary>
    /// Represents the prompt displayed when the user must select a menu option.
    /// </summary>
    public const string SelectionPrompt = "Selection: ";

    /// <summary>
    /// Represents the message displayed when the user enters an unsupported menu selection.
    /// </summary>
    public const string InvalidSelection = "Invalid selection. Please try again.";

    /// <summary>
    /// Represents the message displayed when the user may continue to the previous menu.
    /// </summary>
    public const string PressAnyKeyToContinue = "Press any key to continue...";

    /// <summary>
    /// Represents the message displayed before the application exits.
    /// </summary>
    public const string PressAnyKeyToExit = "Press any key to exit...";

    #endregion

    #region Backup Messages

    /// <summary>
    /// Represents the message displayed when an instance does not contain any completed backups.
    /// </summary>
    public const string NoCompletedBackups = "No completed backups are available for this instance.";

    #endregion

    #region Backup Kind Labels

    /// <summary>
    /// Represents the user-facing label for a manually created backup.
    /// </summary>
    public const string ManualBackupKind = "Manual";

    /// <summary>
    /// Represents the user-facing label for a backup created before a restore operation.
    /// </summary>
    public const string PreRestoreBackupKind = "Pre-restore";

    #endregion
}