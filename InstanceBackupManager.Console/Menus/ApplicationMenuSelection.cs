using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Console.Menus;

/// <summary>
/// Describes an action selected from the application-level instance menu and its optional instance.
/// </summary>
internal sealed record ApplicationMenuSelection(
    ApplicationMenuAction Action,
    InstanceDescriptor? Instance = null
);
