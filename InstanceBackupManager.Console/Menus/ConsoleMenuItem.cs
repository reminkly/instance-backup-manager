namespace InstanceBackupManager.Console.Menus;

/// <summary>
/// Associates a displayed menu label and optional keyboard shortcut with a returned value.
/// </summary>
/// <typeparam name="TValue">The type of value represented by the menu item.</typeparam>
/// <param name="Shortcut">The optional single-character shortcut displayed for the item.</param>
/// <param name="Label">The user-facing item label.</param>
/// <param name="Value">The value returned when the item is selected.</param>
/// <param name="IsCancellation">Indicates that selecting the item cancels the menu.</param>
/// <param name="IsEnabled">Indicates that the item can be highlighted and selected.</param>
internal sealed record ConsoleMenuItem<TValue>(
    string? Shortcut,
    string Label,
    TValue Value,
    bool IsCancellation = false,
    bool IsEnabled = true
);
