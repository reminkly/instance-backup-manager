using InstanceBackupManager.Console.Menus;
using InstanceBackupManager.Console.Utilities;
using InstanceBackupManager.Processing;
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
    /// Gets the menu displayed after a configured instance is loaded.
    /// </summary>
    private InstanceMenu InstanceMenu { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new console application.
    /// </summary>
    /// <param name="configProcessor">The processor used to manage instance configurations.</param>
    /// <param name="instanceMenu">The menu displayed for a loaded instance.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="configProcessor"/> or <paramref name="instanceMenu"/> is null.
    /// </exception>
    internal ConsoleApplication(
        ConfigProcessor configProcessor,
        InstanceMenu instanceMenu
    )
    {
        ArgumentNullException.ThrowIfNull(configProcessor);
        ArgumentNullException.ThrowIfNull(instanceMenu);

        ConfigProcessor = configProcessor;
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
            var instancesPath = Path.Combine(applicationPath, "Instances");

            while (true)
            {
                var discoveredInstances = ConfigProcessor
                    .DiscoverInstances(instancesPath)
                    .ToList();

                if (discoveredInstances.Count == 0)
                {
                    ShowNoInstancesMessage(instancesPath);
                    ConsoleHelper.WaitForExit();

                    return 0;
                }

                var selectedInstance = PromptForInstance(discoveredInstances);

                if (selectedInstance is null)
                {
                    return 0;
                }

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
    /// <returns>The selected instance, or <see langword="null"/> when the user chooses to exit.</returns>
    private static InstanceDescriptor? PromptForInstance(IReadOnlyList<InstanceDescriptor> instances)
    {
        while (true)
        {
            SystemConsole.WriteLine();
            SystemConsole.WriteLine("Instance Backup Manager");
            SystemConsole.WriteLine("=======================");
            SystemConsole.WriteLine();
            SystemConsole.WriteLine("Select an instance:");
            SystemConsole.WriteLine();

            for (var index = 0; index < instances.Count; index++)
            {
                var instance = instances[index];

                var status = instance.HasConfiguration
                    ? "Ready"
                    : "Configuration required";

                SystemConsole.WriteLine($"{index + 1}. {instance.Name} [{status}]");
            }

            SystemConsole.WriteLine("0. Exit");
            SystemConsole.WriteLine();
            SystemConsole.Write("Selection: ");

            var input = SystemConsole.ReadLine();

            if (input is null)
            {
                return null;
            }

            if (!int.TryParse(input, out var selection))
            {
                ConsoleHelper.ShowInvalidSelectionMessage();
                continue;
            }

            if (selection == 0)
            {
                return null;
            }

            if (selection < 1 || selection > instances.Count)
            {
                ConsoleHelper.ShowInvalidSelectionMessage();
                continue;
            }

            return instances[selection - 1];
        }
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

    #region Messages

    /// <summary>
    /// Displays a message explaining how to create the first instance directory.
    /// </summary>
    /// <param name="instancesPath">The absolute path of the empty instances directory.</param>
    private static void ShowNoInstancesMessage(string instancesPath)
    {
        SystemConsole.WriteLine("Instance Backup Manager");
        SystemConsole.WriteLine("=======================");
        SystemConsole.WriteLine();
        SystemConsole.WriteLine("No instance directories were found.");
        SystemConsole.WriteLine();
        SystemConsole.WriteLine("Create a subdirectory inside:");
        SystemConsole.WriteLine(instancesPath);
        SystemConsole.WriteLine();
        SystemConsole.WriteLine("Then restart the application.");
    }

    #endregion
}