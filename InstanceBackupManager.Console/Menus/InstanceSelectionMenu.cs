using InstanceBackupManager.Console.Constants;
using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Console.Menus;

/// <summary>
/// Displays discovered instance directories through the reusable keyboard selector.
/// </summary>
internal static class InstanceSelectionMenu
{
    #region Internal Methods

    /// <summary>
    /// Prompts the user to select a discovered instance.
    /// </summary>
    /// <param name="instances">The discovered instances available for selection.</param>
    /// <returns>
    /// A menu result containing the selected instance, a non-cancelled null value when creation is selected, or a cancelled result when Exit is selected.
    /// </returns>
    internal static ConsoleMenuResult<InstanceDescriptor?> Select(
        IReadOnlyList<InstanceDescriptor> instances
    )
    {
        ArgumentNullException.ThrowIfNull(instances);

        var items = instances
            .Select(
                (instance, index) => new ConsoleMenuItem<InstanceDescriptor?>(
                    CreateShortcut(index),
                    $"{instance.Name} [{GetStatus(instance)}]",
                    instance
                )
            )
            .Append(
                new ConsoleMenuItem<InstanceDescriptor?>(
                    "n",
                    ConsoleMessages.CreateNewInstance,
                    Value: null
                )
            )
            .Append(
                new ConsoleMenuItem<InstanceDescriptor?>(
                    "0",
                    "Exit",
                    Value: null,
                    IsCancellation: true
                )
            )
            .ToList()
            .AsReadOnly();

        var result = ConsoleMenu.Select(
            "Instance Backup Manager",
            items,
            "Select an instance:"
        );

        return result;
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
