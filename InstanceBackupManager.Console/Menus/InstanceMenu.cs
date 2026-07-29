using InstanceBackupManager.Console.Commands;
using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Console.Menus;

/// <summary>
/// Displays and dispatches registered commands for a configured instance.
/// </summary>
internal sealed class InstanceMenu
{
    #region Properties

    /// <summary>
    /// Gets the commands available through the configured-instance menu.
    /// </summary>
    private IReadOnlyList<IInstanceCommand> Commands { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance menu using the supplied commands.
    /// </summary>
    /// <param name="commands">The commands that can be displayed and executed by the menu.</param>
    internal InstanceMenu(IReadOnlyCollection<IInstanceCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);

        if (commands.Count == 0)
        {
            throw new ArgumentException(
                "At least one instance command must be registered.",
                nameof(commands)
            );
        }

        var duplicateSelection = commands
            .GroupBy(
                command => command.Selection,
                StringComparer.OrdinalIgnoreCase
            )
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateSelection is not null)
        {
            throw new ArgumentException(
                $"Instance command selection '{duplicateSelection.Key}' is registered more than once.",
                nameof(commands)
            );
        }

        Commands = commands
            .OrderBy(command => command.Selection, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Displays available commands and returns to instance selection when requested.
    /// </summary>
    /// <param name="instance">The loaded instance selected by the user.</param>
    /// <returns>Zero when the user returns normally; otherwise, the nonzero result returned by a failed command.</returns>
    internal int Run(InstanceContext instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        while (true)
        {
            var menuItems = Commands
                .Select(
                    command => new ConsoleMenuItem<IInstanceCommand?>(
                        command.Selection,
                        command.Description,
                        command,
                        IsEnabled: command.IsAvailable(instance)
                    )
                )
                .Append(
                    new ConsoleMenuItem<IInstanceCommand?>(
                        "0",
                        "Return to instances",
                        Value: null,
                        IsCancellation: true
                    )
                )
                .ToList()
                .AsReadOnly();

            var selection = ConsoleMenu.Select(
                instance.Config.Name,
                menuItems
            );

            if (selection.IsCancelled || selection.Value is null)
            {
                return 0;
            }

            var result = selection.Value.Execute(instance);

            if (result != 0)
            {
                return result;
            }
        }
    }

    #endregion
}
