using InstanceBackupManager.Console.Commands;
using InstanceBackupManager.Console.Constants;
using InstanceBackupManager.Console.Utilities;
using InstanceBackupManager.Processing.Models.Instances;
using SystemConsole = System.Console;

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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="commands"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when no commands are supplied or command selections are duplicated.</exception>
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
            var availableCommands = Commands
                .Where(command => command.IsAvailable(instance))
                .ToList();

            DisplayMenu(
                instance,
                availableCommands
            );

            var input = SystemConsole.ReadLine();

            if (input is null)
            {
                return 0;
            }

            var selection = input.Trim();

            if (selection == "0")
            {
                return 0;
            }

            var selectedCommand = availableCommands.SingleOrDefault(
                command => string.Equals(
                    command.Selection,
                    selection,
                    StringComparison.OrdinalIgnoreCase
                )
            );

            if (selectedCommand is null)
            {
                ConsoleHelper.ShowInvalidSelectionMessage();
                continue;
            }

            var result = selectedCommand.Execute(instance);

            if (result != 0)
            {
                return result;
            }
        }
    }

    #endregion

    #region Display Helpers

    /// <summary>
    /// Displays the instance heading and commands currently available for execution.
    /// </summary>
    /// <param name="instance">The selected instance.</param>
    /// <param name="commands">The commands available for the selected instance.</param>
    private static void DisplayMenu(
        InstanceContext instance,
        IReadOnlyCollection<IInstanceCommand> commands
    )
    {
        SystemConsole.WriteLine();
        SystemConsole.WriteLine(instance.Config.Name);
        SystemConsole.WriteLine(new string('=', instance.Config.Name.Length));
        SystemConsole.WriteLine();

        foreach (var command in commands)
        {
            SystemConsole.WriteLine($"{command.Selection}. {command.Description}");
        }

        SystemConsole.WriteLine("0. Return to instances");
        SystemConsole.WriteLine();
        SystemConsole.Write(ConsoleMessages.SelectionPrompt);
    }

    #endregion
}
