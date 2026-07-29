using InstanceBackupManager.Console.Menus;
using InstanceBackupManager.Console.Workflows;
using InstanceBackupManager.Console.Utilities;
using InstanceBackupManager.Processing;
using InstanceBackupManager.Processing.Constants;
using InstanceBackupManager.Processing.Models.Instances;
using SystemConsole = System.Console;

namespace InstanceBackupManager.Console;

/// <summary>
/// Coordinates application startup, instance discovery, instance selection, and configuration creation.
/// </summary>
internal sealed class ConsoleApplication
{
    #region Properties

    /// <summary>
    /// Gets the processor used to discover, create, and load instance configurations.
    /// </summary>
    private ConfigProcessor ConfigProcessor { get; }

    /// <summary>
    /// Gets the workflow used to create a new instance and skeleton configuration.
    /// </summary>
    private InstanceCreationWorkflow InstanceCreationWorkflow { get; }

    /// <summary>
    /// Gets the menu displayed after a configured instance is loaded.
    /// </summary>
    private InstanceMenu InstanceMenu { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new console application.
    /// </summary>
    /// <param name="configProcessor">The processor used to manage instance configurations.</param>
    /// <param name="instanceCreationWorkflow">The workflow used to create new instances.</param>
    /// <param name="instanceMenu">The menu displayed for a loaded instance.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when a required dependency is null.
    /// </exception>
    internal ConsoleApplication(
        ConfigProcessor configProcessor,
        InstanceCreationWorkflow instanceCreationWorkflow,
        InstanceMenu instanceMenu
    )
    {
        ArgumentNullException.ThrowIfNull(configProcessor);
        ArgumentNullException.ThrowIfNull(instanceCreationWorkflow);
        ArgumentNullException.ThrowIfNull(instanceMenu);

        ConfigProcessor = configProcessor;
        InstanceCreationWorkflow = instanceCreationWorkflow;
        InstanceMenu = instanceMenu;
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Starts the interactive application workflow and continues displaying instance selection until the user exits.
    /// </summary>
    /// <returns>Zero when the application exits normally; otherwise, one.</returns>
    internal int Run()
    {
        try
        {
            var applicationPath = AppContext.BaseDirectory;
            var instancesPath = Path.Combine(
                applicationPath,
                BackupStorageConstants.InstancesDirectoryName
            );

            while (true)
            {
                var discoveredInstances = ConfigProcessor
                    .DiscoverInstances(instancesPath)
                    .ToList();

                var selection = PromptForInstance(discoveredInstances);

                if (selection.IsCancelled)
                {
                    return 0;
                }

                if (selection.Value is null)
                {
                    var creationOutcome = InstanceCreationWorkflow.Run(instancesPath);

                    if (creationOutcome == InstanceCreationWorkflowOutcome.Created)
                    {
                        return 0;
                    }

                    continue;
                }

                var selectedInstance = selection.Value;

                if (!selectedInstance.HasConfiguration)
                {
                    CreateSkeletonConfiguration(selectedInstance);
                    ConsoleHelper.WaitForContinue();

                    continue;
                }

                var instance = ConfigProcessor.LoadInstance(selectedInstance.InstancePath);
                var result = InstanceMenu.Run(instance);

                if (result != 0)
                {
                    return result;
                }
            }
        }
        catch (Exception exception)
        {
            SystemConsole.WriteLine();
            SystemConsole.WriteLine("The application encountered an unexpected error:");
            SystemConsole.WriteLine(exception.Message);

            ConsoleHelper.WaitForExit();

            return 1;
        }
    }

    #endregion

    #region Instance Selection

    /// <summary>
    /// Displays discovered instances and prompts the user to select one.
    /// </summary>
    /// <param name="instances">The discovered instances available for selection.</param>
    /// <returns>The selected instance, creation request, or cancellation result.</returns>
    private static ConsoleMenuResult<InstanceDescriptor?> PromptForInstance(IReadOnlyList<InstanceDescriptor> instances)
    {
        return InstanceSelectionMenu.Select(instances);
    }

    #endregion

    #region Configuration Creation

    /// <summary>
    /// Creates a skeleton configuration for an unconfigured instance and displays its location.
    /// </summary>
    /// <param name="instance">The unconfigured instance selected by the user.</param>
    private void CreateSkeletonConfiguration(InstanceDescriptor instance)
    {
        var configPath = Path.Combine(
            instance.InstancePath,
            "instance.json"
        );

        SystemConsole.WriteLine();
        SystemConsole.WriteLine($"'{instance.Name}' does not contain an instance.json file.");
        SystemConsole.WriteLine();
        SystemConsole.WriteLine("A skeleton configuration will be created.");

        ConfigProcessor.CreateSkeletonConfig(instance.InstancePath);

        SystemConsole.WriteLine();
        SystemConsole.WriteLine("Configuration created:");
        SystemConsole.WriteLine(configPath);
        SystemConsole.WriteLine();
        SystemConsole.WriteLine("Update the configuration file before continuing.");
        SystemConsole.WriteLine("After you return to instance selection, the instance directories and configurations will be rediscovered.");
    }

    #endregion
}