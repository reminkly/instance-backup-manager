using System.Diagnostics;
using InstanceBackupManager.Console.Menus;
using InstanceBackupManager.Console.Utilities;
using InstanceBackupManager.Processing;
using InstanceBackupManager.Processing.Exceptions;
using SystemConsole = System.Console;

namespace InstanceBackupManager.Console.Workflows;

/// <summary>
/// Explains unsupported instance configuration schemas and helps the user locate the file that must be updated.
/// </summary>
internal sealed class ConfigurationUpdateWorkflow
{
    #region Properties

    /// <summary>
    /// Gets the processor used to validate and atomically upgrade instance configurations.
    /// </summary>
    private ConfigProcessor ConfigProcessor { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes the configuration-update workflow.
    /// </summary>
    /// <param name="configProcessor">The processor used to perform configuration upgrades.</param>
    internal ConfigurationUpdateWorkflow(ConfigProcessor configProcessor)
    {
        ArgumentNullException.ThrowIfNull(configProcessor);
        ConfigProcessor = configProcessor;
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Displays schema-version details and offers to reveal the configuration before exiting.
    /// </summary>
    internal ConfigurationUpdateWorkflowOutcome Run(
        string instanceName,
        UnsupportedInstanceConfigurationSchemaException exception
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        ArgumentNullException.ThrowIfNull(exception);

        var details = string.Join(
            Environment.NewLine,
            $"Instance:          {instanceName}",
            $"Current schema:    {exception.ConfiguredVersion}",
            $"Supported schema:  {exception.SupportedVersion}",
            string.Empty,
            "This configuration must be updated before the instance can be used.",
            string.Empty,
            $"Configuration: {exception.ConfigPath}"
        );

        var menuItems = new List<ConsoleMenuItem<ConfigurationUpdateWorkflowOutcome>>();

        if (ConfigProcessor.CanUpgradeConfig(exception.ConfiguredVersion))
        {
            menuItems.Add(
                new ConsoleMenuItem<ConfigurationUpdateWorkflowOutcome>(
                    "u",
                    "Back up and upgrade configuration",
                    ConfigurationUpdateWorkflowOutcome.UpgradeConfiguration
                )
            );
        }

        menuItems.AddRange(
            [
                new(
                    "r",
                    "Return to instances",
                    ConfigurationUpdateWorkflowOutcome.ReturnToInstances
                ),
                new(
                    "o",
                    "Open the configuration folder and exit",
                    ConfigurationUpdateWorkflowOutcome.ExitApplication
                )
            ]
        );

        var selection = ConsoleMenu.Select(
            "Configuration Update Required",
            menuItems.AsReadOnly(),
            details
        );

        if (selection.IsCancelled
            || selection.Value == ConfigurationUpdateWorkflowOutcome.ReturnToInstances)
        {
            return ConfigurationUpdateWorkflowOutcome.ReturnToInstances;
        }

        if (selection.Value == ConfigurationUpdateWorkflowOutcome.UpgradeConfiguration)
        {
            var result = ConfigProcessor.UpgradeConfig(exception.ConfigPath);

            SystemConsole.WriteLine();
            SystemConsole.WriteLine("Configuration upgraded successfully.");
            SystemConsole.WriteLine($"Schema:   {result.PreviousVersion} -> {result.CurrentVersion}");
            SystemConsole.WriteLine($"Config:   {result.ConfigPath}");
            SystemConsole.WriteLine($"Original: {result.BackupPath}");
            SystemConsole.WriteLine();
            SystemConsole.WriteLine("The unchanged original configuration was preserved at the path above.");

            ConsoleHelper.WaitForContinue();

            return ConfigurationUpdateWorkflowOutcome.ReturnToInstances;
        }

        Process.Start(
            new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "/select,\"" + exception.ConfigPath + "\"",
                UseShellExecute = true
            }
        );

        return ConfigurationUpdateWorkflowOutcome.ExitApplication;
    }

    #endregion
}
