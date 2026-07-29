using InstanceBackupManager.Console.Menus;
using InstanceBackupManager.Console.Workflows;
using InstanceBackupManager.Console.Utilities;
using InstanceBackupManager.Processing;
using InstanceBackupManager.Processing.Constants;
using InstanceBackupManager.Processing.Exceptions;
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
    /// Gets the workflow used to explain unsupported configuration schemas.
    /// </summary>
    private ConfigurationUpdateWorkflow ConfigurationUpdateWorkflow { get; }

    /// <summary>
    /// Gets the workflow used to create a new instance and skeleton configuration.
    /// </summary>
    private InstanceCreationWorkflow InstanceCreationWorkflow { get; }

    /// <summary>
    /// Gets the menu displayed after a configured instance is loaded.
    /// </summary>
    private InstanceMenu InstanceMenu { get; }

    /// <summary>
    /// Gets the workflow used for startup and explicit GitHub release checks.
    /// </summary>
    private UpdateWorkflow UpdateWorkflow { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new console application.
    /// </summary>
    /// <param name="configProcessor">The processor used to manage instance configurations.</param>
    /// <param name="configurationUpdateWorkflow">The workflow used for out-of-date configurations.</param>
    /// <param name="instanceCreationWorkflow">The workflow used to create new instances.</param>
    /// <param name="instanceMenu">The menu displayed for a loaded instance.</param>
    /// <param name="updateWorkflow">The workflow used to check for published updates.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when a required dependency is null.
    /// </exception>
    internal ConsoleApplication(
        ConfigProcessor configProcessor,
        ConfigurationUpdateWorkflow configurationUpdateWorkflow,
        InstanceCreationWorkflow instanceCreationWorkflow,
        InstanceMenu instanceMenu,
        UpdateWorkflow updateWorkflow
    )
    {
        ArgumentNullException.ThrowIfNull(configProcessor);
        ArgumentNullException.ThrowIfNull(configurationUpdateWorkflow);
        ArgumentNullException.ThrowIfNull(instanceCreationWorkflow);
        ArgumentNullException.ThrowIfNull(instanceMenu);
        ArgumentNullException.ThrowIfNull(updateWorkflow);

        ConfigProcessor = configProcessor;
        ConfigurationUpdateWorkflow = configurationUpdateWorkflow;
        InstanceCreationWorkflow = instanceCreationWorkflow;
        InstanceMenu = instanceMenu;
        UpdateWorkflow = updateWorkflow;
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Performs the startup update check, then continues displaying instance selection until the user exits.
    /// </summary>
    /// <returns>Zero when the application exits normally; otherwise, one.</returns>
    internal async Task<int> RunAsync()
    {
        try
        {
            await UpdateWorkflow.CheckAtStartupAsync();

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

                var selectedAction = selection.Value
                    ?? throw new InvalidOperationException("The application menu returned no selection.");

                if (selectedAction.Action == ApplicationMenuAction.CreateInstance)
                {
                    var creationOutcome = InstanceCreationWorkflow.Run(instancesPath);

                    if (creationOutcome == InstanceCreationWorkflowOutcome.Created)
                    {
                        return 0;
                    }

                    continue;
                }

                if (selectedAction.Action == ApplicationMenuAction.CheckForUpdates)
                {
                    await UpdateWorkflow.RunAsync();
                    continue;
                }

                var selectedInstance = selectedAction.Instance
                    ?? throw new InvalidOperationException("The selected action does not identify an instance.");

                if (!selectedInstance.HasConfiguration)
                {
                    CreateSkeletonConfiguration(selectedInstance);
                    ConsoleHelper.WaitForContinue();

                    continue;
                }

                InstanceContext instance;

                try
                {
                    instance = ConfigProcessor.LoadInstance(selectedInstance.InstancePath);
                }
                catch (UnsupportedInstanceConfigurationSchemaException exception)
                {
                    var updateOutcome = ConfigurationUpdateWorkflow.Run(
                        selectedInstance.Name,
                        exception
                    );

                    if (updateOutcome == ConfigurationUpdateWorkflowOutcome.ExitApplication)
                    {
                        return 0;
                    }

                    continue;
                }

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
    private static ConsoleMenuResult<ApplicationMenuSelection?> PromptForInstance(IReadOnlyList<InstanceDescriptor> instances)
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