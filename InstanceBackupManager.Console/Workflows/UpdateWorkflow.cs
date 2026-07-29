using System.Diagnostics;
using InstanceBackupManager.Console.Menus;
using InstanceBackupManager.Console.Utilities;
using InstanceBackupManager.Processing;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Updates;
using SystemConsole = System.Console;

namespace InstanceBackupManager.Console.Workflows;

/// <summary>
/// Checks for published updates, presents version information, and opens the selected GitHub release page.
/// </summary>
internal sealed class UpdateWorkflow
{
    #region Properties

    /// <summary>
    /// Gets the processor used to discover and compare published releases.
    /// </summary>
    private UpdateProcessor UpdateProcessor { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new update workflow.
    /// </summary>
    /// <param name="updateProcessor">The processor used to discover and compare published releases.</param>
    internal UpdateWorkflow(UpdateProcessor updateProcessor)
    {
        ArgumentNullException.ThrowIfNull(updateProcessor);

        UpdateProcessor = updateProcessor;
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Performs a best-effort startup check and prompts only when a newer release is available.
    /// </summary>
    internal async Task CheckAtStartupAsync()
    {
        try
        {
            var result = await UpdateProcessor.CheckForUpdatesAsync();

            if (result.Status == UpdateCheckStatus.UpdateAvailable)
            {
                PromptToOpenRelease(result);
            }
        }
        catch
        {
            // Startup update checks are best-effort and must never prevent access to local backup operations.
        }
    }

    /// <summary>
    /// Performs an explicit update check and displays the result or a recoverable error.
    /// </summary>
    internal async Task RunAsync()
    {
        try
        {
            SystemConsole.WriteLine();
            SystemConsole.WriteLine("Checking GitHub for updates...");

            var result = await UpdateProcessor.CheckForUpdatesAsync();

            if (result.Status == UpdateCheckStatus.UpdateAvailable)
            {
                PromptToOpenRelease(result);
            }
            else
            {
                SystemConsole.WriteLine();
                SystemConsole.WriteLine("Instance Backup Manager is up to date.");
                SystemConsole.WriteLine($"Installed version: {FormatVersion(result.InstalledVersion)}");
            }
        }
        catch (Exception exception)
        {
            SystemConsole.WriteLine();
            SystemConsole.WriteLine("The update check could not be completed.");
            SystemConsole.WriteLine(exception.Message);
        }

        ConsoleHelper.WaitForContinue();
    }

    #endregion

    #region Update Presentation

    /// <summary>
    /// Displays an available update and offers to open its GitHub release page.
    /// </summary>
    private static void PromptToOpenRelease(UpdateCheckResult result)
    {
        var details = string.Join(
            Environment.NewLine,
            $"Installed: {FormatVersion(result.InstalledVersion)}",
            $"Latest:    {FormatVersion(result.LatestVersion)}",
            $"Release:   {result.LatestRelease.Name}",
            string.Empty,
            "Open this release in your browser?"
        );

        var selection = ConsoleMenu.Select(
            "Update Available",
            new List<ConsoleMenuItem<bool>>
            {
                new("n", "No, continue using this version", false),
                new("y", "Yes, view and download the release", true)
            }.AsReadOnly(),
            details
        );

        if (!selection.IsCancelled && selection.Value)
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = result.LatestRelease.PageUri.AbsoluteUri,
                    UseShellExecute = true
                }
            );
        }
    }

    /// <summary>
    /// Formats a normalized application version without displaying the unused revision component.
    /// </summary>
    private static string FormatVersion(Version version)
    {
        return version.ToString(fieldCount: 3);
    }

    #endregion
}
