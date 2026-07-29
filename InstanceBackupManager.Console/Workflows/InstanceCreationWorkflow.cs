using System.Diagnostics;
using InstanceBackupManager.Console.Constants;
using InstanceBackupManager.Console.Menus;
using InstanceBackupManager.Console.Utilities;
using InstanceBackupManager.Processing;
using SystemConsole = System.Console;

namespace InstanceBackupManager.Console.Workflows;

/// <summary>
/// Collects, confirms, and submits the values used to create a new instance and skeleton configuration.
/// </summary>
internal sealed class InstanceCreationWorkflow
{
    #region Properties

    /// <summary>
    /// Gets the processor used to validate and create new instances.
    /// </summary>
    private InstanceCreationProcessor InstanceCreationProcessor { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance-creation workflow.
    /// </summary>
    /// <param name="instanceCreationProcessor">The processor used to validate and create new instances.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="instanceCreationProcessor"/> is null.</exception>
    internal InstanceCreationWorkflow(InstanceCreationProcessor instanceCreationProcessor)
    {
        ArgumentNullException.ThrowIfNull(instanceCreationProcessor);

        InstanceCreationProcessor = instanceCreationProcessor;
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Prompts for instance values, confirms the operation, and creates the requested skeleton configuration.
    /// </summary>
    /// <param name="instancesPath">The directory containing individual instance directories.</param>
    /// <returns>An outcome indicating whether the application should exit or return to instance selection.</returns>
    internal InstanceCreationWorkflowOutcome Run(string instancesPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instancesPath);

        try
        {
            SystemConsole.WriteLine();
            SystemConsole.WriteLine("Create a New Instance");
            SystemConsole.WriteLine("=====================");
            SystemConsole.WriteLine();
            SystemConsole.Write("Instance name: ");

            var instanceName = SystemConsole.ReadLine();

            if (string.IsNullOrWhiteSpace(instanceName))
            {
                ShowCancelledMessage();
                return InstanceCreationWorkflowOutcome.Cancelled;
            }

            var suggestedFolderName = InstanceCreationProcessor.CreateSuggestedFolderName(instanceName);

            SystemConsole.Write($"Folder name [{suggestedFolderName}]: ");

            var requestedFolderName = SystemConsole.ReadLine();
            var folderName = string.IsNullOrWhiteSpace(requestedFolderName)
                ? suggestedFolderName
                : requestedFolderName;

            if (!ConfirmCreation(
                instancesPath,
                instanceName.Trim(),
                folderName.Trim()
            ))
            {
                ShowCancelledMessage();
                return InstanceCreationWorkflowOutcome.Cancelled;
            }

            var result = InstanceCreationProcessor.CreateInstance(
                instancesPath,
                instanceName,
                folderName
            );

            SystemConsole.WriteLine();
            SystemConsole.WriteLine("Instance created successfully.");
            SystemConsole.WriteLine($"Name:          {result.Name}");
            SystemConsole.WriteLine($"Folder:        {result.FolderName}");
            SystemConsole.WriteLine($"Configuration: {result.ConfigPath}");
            SystemConsole.WriteLine();
            SystemConsole.WriteLine("Update instance.json before restarting Instance Backup Manager.");

            SystemConsole.WriteLine();
            SystemConsole.WriteLine("Instance created successfully.");
            SystemConsole.WriteLine($"Name:          {result.Name}");
            SystemConsole.WriteLine($"Folder:        {result.FolderName}");
            SystemConsole.WriteLine($"Configuration: {result.ConfigPath}");
            SystemConsole.WriteLine();
            SystemConsole.WriteLine("Update instance.json before restarting Instance Backup Manager.");

            Process.Start(
                new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{result.ConfigPath}\"",
                    UseShellExecute = true
                }
            );

            ConsoleHelper.WaitForExit();

            return InstanceCreationWorkflowOutcome.Created;
        }
        catch (Exception exception)
        {
            SystemConsole.WriteLine();
            SystemConsole.WriteLine("The instance could not be created.");
            SystemConsole.WriteLine(exception.Message);

            ConsoleHelper.WaitForContinue();

            return InstanceCreationWorkflowOutcome.Failed;
        }
    }

    #endregion

    #region Confirmation

    /// <summary>
    /// Displays the proposed instance values and asks the user to confirm creation.
    /// </summary>
    private static bool ConfirmCreation(
        string instancesPath,
        string instanceName,
        string folderName
    )
    {
        var instancePath = Path.GetFullPath(
            folderName,
            Path.GetFullPath(instancesPath)
        );

        var details = string.Join(
            Environment.NewLine,
            $"Name:   {instanceName}",
            $"Folder: {folderName}",
            $"Path:   {instancePath}",
            string.Empty,
            "Create this instance?"
        );

        var result = ConsoleMenu.Select(
            "Confirm Instance Creation",
            new List<ConsoleMenuItem<bool>>
            {
                new("n", "No, cancel creation", false),
                new("y", "Yes, create the instance", true)
            }.AsReadOnly(),
            details
        );

        return !result.IsCancelled && result.Value;
    }

    #endregion

    #region Messages

    /// <summary>
    /// Displays a consistent cancellation message before returning to instance selection.
    /// </summary>
    private static void ShowCancelledMessage()
    {
        SystemConsole.WriteLine();
        SystemConsole.WriteLine(ConsoleMessages.InstanceCreationCancelled);

        ConsoleHelper.WaitForContinue();
    }

    #endregion
}
