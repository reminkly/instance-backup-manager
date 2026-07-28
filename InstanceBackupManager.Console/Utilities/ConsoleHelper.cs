using SystemConsole = System.Console;

namespace InstanceBackupManager.Console.Utilities;

/// <summary>
/// Provides reusable console input, message, and platform-specific behavior.
/// </summary>
internal static class ConsoleHelper
{
    #region Internal Methods

    /// <summary>
    /// Displays a standard message indicating that the supplied menu selection is invalid.
    /// </summary>
    internal static void ShowInvalidSelectionMessage()
    {
        SystemConsole.WriteLine();
        SystemConsole.WriteLine("Invalid selection. Please try again.");
    }

    /// <summary>
    /// Waits for the user to press a key before returning to the previous menu.
    /// </summary>
    internal static void WaitForContinue()
    {
        WaitForKey("Press any key to continue...");
    }

    /// <summary>
    /// Waits for the user to press a key before the application exits.
    /// </summary>
    internal static void WaitForExit()
    {
        WaitForKey("Press any key to exit...");
    }

    /// <summary>
    /// Gets the appropriate path-comparison behavior for the current operating system.
    /// </summary>
    /// <returns>A case-insensitive comparison on Windows and a case-sensitive comparison on other operating systems.</returns>
    internal static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Displays a prompt and waits for a key when the process has an interactive console.
    /// </summary>
    /// <param name="message">The message displayed before waiting for input.</param>
    private static void WaitForKey(string message)
    {
        if (SystemConsole.IsInputRedirected)
        {
            return;
        }

        SystemConsole.WriteLine();
        SystemConsole.WriteLine(message);
        SystemConsole.ReadKey(intercept: true);
    }

    #endregion
}