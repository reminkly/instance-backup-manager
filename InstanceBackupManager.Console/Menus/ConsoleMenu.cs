using SystemConsole = System.Console;

namespace InstanceBackupManager.Console.Menus;

/// <summary>
/// Displays a reusable keyboard-driven console selector with redirected-input support for tests and automation.
/// </summary>
internal static class ConsoleMenu
{
    #region Internal Methods

    /// <summary>
    /// Displays a menu and returns the selected value.
    /// </summary>
    /// <typeparam name="TValue">The type of value represented by the menu items.</typeparam>
    /// <param name="title">The heading displayed above the menu.</param>
    /// <param name="items">The available menu items.</param>
    /// <param name="instructions">Optional instructions displayed above the items.</param>
    /// <returns>The selected value or a cancelled result.</returns>
    internal static ConsoleMenuResult<TValue> Select<TValue>(
        string title,
        IReadOnlyList<ConsoleMenuItem<TValue>> items,
        string? instructions = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            throw new ArgumentException(
                "At least one console menu item must be supplied.",
                nameof(items)
            );
        }

        ValidateShortcuts(items);

        return SystemConsole.IsInputRedirected || SystemConsole.In is StringReader
            ? SelectUsingRedirectedInput(
                title,
                items,
                instructions
            )
            : SelectUsingKeyboard(
                title,
                items,
                instructions
            );
    }

    #endregion

    #region Interactive Selection

    /// <summary>
    /// Displays an interactive menu controlled by individual key presses.
    /// </summary>
    private static ConsoleMenuResult<TValue> SelectUsingKeyboard<TValue>(
        string title,
        IReadOnlyList<ConsoleMenuItem<TValue>> items,
        string? instructions
    )
    {
        var selectedIndex = 0;

        while (true)
        {
            DrawInteractiveMenu(
                title,
                items,
                instructions,
                selectedIndex
            );

            var key = SystemConsole.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    selectedIndex = selectedIndex == 0
                        ? items.Count - 1
                        : selectedIndex - 1;
                    break;

                case ConsoleKey.DownArrow:
                    selectedIndex = selectedIndex == items.Count - 1
                        ? 0
                        : selectedIndex + 1;
                    break;

                case ConsoleKey.Home:
                    selectedIndex = 0;
                    break;

                case ConsoleKey.End:
                    selectedIndex = items.Count - 1;
                    break;

                case ConsoleKey.Enter:
                    return CreateResult(items[selectedIndex]);

                case ConsoleKey.Escape:
                    return new ConsoleMenuResult<TValue>(
                        IsCancelled: true,
                        Value: default
                    );

                default:
                    var shortcutIndex = FindShortcutIndex(
                        items,
                        key.KeyChar
                    );

                    if (shortcutIndex >= 0)
                    {
                        return CreateResult(items[shortcutIndex]);
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Redraws the interactive menu using the current selected index.
    /// </summary>
    private static void DrawInteractiveMenu<TValue>(
        string title,
        IReadOnlyList<ConsoleMenuItem<TValue>> items,
        string? instructions,
        int selectedIndex
    )
    {
        SystemConsole.Clear();
        SystemConsole.WriteLine(title);
        SystemConsole.WriteLine(new string('=', title.Length));
        SystemConsole.WriteLine();

        if (!string.IsNullOrWhiteSpace(instructions))
        {
            SystemConsole.WriteLine(instructions);
            SystemConsole.WriteLine();
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var shortcut = string.IsNullOrWhiteSpace(item.Shortcut)
                ? string.Empty
                : $"{item.Shortcut}. ";

            WriteMenuLine(
                $"{shortcut}{item.Label}",
                isSelected: index == selectedIndex
            );
        }

        SystemConsole.WriteLine();
        SystemConsole.WriteLine("Use Up/Down to move, Enter to select, or Escape to return.");
    }

    /// <summary>
    /// Writes one menu row and highlights the complete selected row using colors derived from the current console theme.
    /// </summary>
    private static void WriteMenuLine(
        string text,
        bool isSelected
    )
    {
        if (!isSelected)
        {
            SystemConsole.WriteLine($"  {text}");
            return;
        }

        var originalForeground = SystemConsole.ForegroundColor;
        var originalBackground = SystemConsole.BackgroundColor;

        try
        {
            SystemConsole.ForegroundColor = originalBackground;
            SystemConsole.BackgroundColor = originalForeground;

            var highlightedText = $"  {text}";
            var availableWidth = Math.Max(
                highlightedText.Length,
                SystemConsole.WindowWidth - 1
            );

            SystemConsole.WriteLine(
                highlightedText.PadRight(availableWidth)
            );
        }
        finally
        {
            SystemConsole.ForegroundColor = originalForeground;
            SystemConsole.BackgroundColor = originalBackground;
        }
    }

    #endregion

    #region Redirected Selection

    /// <summary>
    /// Uses line-based selection when console input is redirected, preserving automated test and scripting support.
    /// </summary>
    private static ConsoleMenuResult<TValue> SelectUsingRedirectedInput<TValue>(
        string title,
        IReadOnlyList<ConsoleMenuItem<TValue>> items,
        string? instructions
    )
    {
        while (true)
        {
            SystemConsole.WriteLine();
            SystemConsole.WriteLine(title);
            SystemConsole.WriteLine(new string('=', title.Length));
            SystemConsole.WriteLine();

            if (!string.IsNullOrWhiteSpace(instructions))
            {
                SystemConsole.WriteLine(instructions);
                SystemConsole.WriteLine();
            }

            foreach (var item in items)
            {
                var shortcut = string.IsNullOrWhiteSpace(item.Shortcut)
                    ? "-"
                    : item.Shortcut;

                SystemConsole.WriteLine(
                    $"{shortcut}. {item.Label}"
                );
            }

            SystemConsole.WriteLine();
            SystemConsole.Write("Selection: ");

            var input = SystemConsole.ReadLine();

            if (input is null)
            {
                return new ConsoleMenuResult<TValue>(
                    IsCancelled: true,
                    Value: default
                );
            }

            var selectedItem = items.SingleOrDefault(
                item => string.Equals(
                    item.Shortcut,
                    input.Trim(),
                    StringComparison.OrdinalIgnoreCase
                )
            );

            if (selectedItem is not null)
            {
                return CreateResult(selectedItem);
            }

            SystemConsole.WriteLine();
            SystemConsole.WriteLine("Invalid selection. Please try again.");
        }
    }

    #endregion

    #region Selection Helpers

    /// <summary>
    /// Finds an item matching a single-character keyboard shortcut.
    /// </summary>
    private static int FindShortcutIndex<TValue>(
        IReadOnlyList<ConsoleMenuItem<TValue>> items,
        char keyCharacter
    )
    {
        if (char.IsControl(keyCharacter))
        {
            return -1;
        }

        var shortcut = keyCharacter.ToString();

        for (var index = 0; index < items.Count; index++)
        {
            if (string.Equals(
                items[index].Shortcut,
                shortcut,
                StringComparison.OrdinalIgnoreCase
            ))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Creates a menu result from the selected item.
    /// </summary>
    private static ConsoleMenuResult<TValue> CreateResult<TValue>(
        ConsoleMenuItem<TValue> item
    )
    {
        return new ConsoleMenuResult<TValue>(
            IsCancelled: item.IsCancellation,
            Value: item.IsCancellation
                ? default
                : item.Value
        );
    }

    /// <summary>
    /// Ensures that shortcuts are unique and contain at most one character.
    /// </summary>
    private static void ValidateShortcuts<TValue>(
        IReadOnlyCollection<ConsoleMenuItem<TValue>> items
    )
    {
        var shortcuts = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase
        );

        foreach (var item in items)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(item.Label);

            if (string.IsNullOrWhiteSpace(item.Shortcut))
            {
                continue;
            }

            if (item.Shortcut.Length != 1)
            {
                throw new ArgumentException(
                    $"Menu shortcut '{item.Shortcut}' must contain exactly one character.",
                    nameof(items)
                );
            }

            if (!shortcuts.Add(item.Shortcut))
            {
                throw new ArgumentException(
                    $"Menu shortcut '{item.Shortcut}' is registered more than once.",
                    nameof(items)
                );
            }
        }
    }

    #endregion
}
