using InstanceBackupManager.Console.Constants;
using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Console.Menus;

/// <summary>
/// Displays discovered instance directories and application-level actions through the reusable keyboard selector.
/// </summary>
internal static class InstanceSelectionMenu
{
    #region Internal Methods

    /// <summary>
    /// Prompts the user to open an instance, create an instance, check for updates, or exit.
    /// </summary>
    /// <param name="instances">The discovered instances available for selection.</param>
    /// <returns>The selected application-level action or a cancelled result when Exit is selected.</returns>
    internal static ConsoleMenuResult<ApplicationMenuSelection?> Select(
        IReadOnlyList<InstanceDescriptor> instances
    )
    {
        ArgumentNullException.ThrowIfNull(instances);

        var items = instances
            .Select(
                (instance, index) => new ConsoleMenuItem<ApplicationMenuSelection?>(
                    CreateShortcut(index),
                    $"{instance.Name} [{GetStatus(instance)}]",
                    new ApplicationMenuSelection(
                        ApplicationMenuAction.OpenInstance,
                        instance
                    )
                )
            )
            .Append(
                new ConsoleMenuItem<ApplicationMenuSelection?>(
                    "n",
                    ConsoleMessages.CreateNewInstance,
                    new ApplicationMenuSelection(ApplicationMenuAction.CreateInstance)
                )
            )
            .Append(
                new ConsoleMenuItem<ApplicationMenuSelection?>(
                    "u",
                    ConsoleMessages.CheckForUpdates,
                    new ApplicationMenuSelection(ApplicationMenuAction.CheckForUpdates)
                )
            )
            .Append(
                new ConsoleMenuItem<ApplicationMenuSelection?>(
                    "0",
                    "Exit",
                    Value: null,
                    IsCancellation: true
                )
            )
            .ToList()
            .AsReadOnly();

        return ConsoleMenu.Select(
            "Instance Backup Manager",
            items,
            "Select an instance or application action:"
        );
    }

    #endregion

    #region Display Helpers

    /// <summary>
    /// Gets the display status for a discovered instance.
    /// </summary>
    private static string GetStatus(InstanceDescriptor instance)
    {
        return instance.HasConfiguration
            ? "Ready"
            : "Configuration required";
    }

    /// <summary>
    /// Creates a single-key shortcut for the first nine instances.
    /// </summary>
    private static string? CreateShortcut(int index)
    {
        var selectionNumber = index + 1;

        return selectionNumber <= 9
            ? selectionNumber.ToString()
            : null;
    }

    #endregion
}
