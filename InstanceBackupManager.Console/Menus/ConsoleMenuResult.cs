namespace InstanceBackupManager.Console.Menus;

/// <summary>
/// Represents the value selected from a console menu or cancellation of that menu.
/// </summary>
/// <typeparam name="TValue">The type of value represented by menu items.</typeparam>
/// <param name="IsCancelled">Indicates that the menu was cancelled.</param>
/// <param name="Value">The selected value, or the default value when cancelled.</param>
internal sealed record ConsoleMenuResult<TValue>(
    bool IsCancelled,
    TValue? Value
);
