using InstanceBackupManager.Console.Utilities;
using InstanceBackupManager.Console.Workflows;
using InstanceBackupManager.Processing.Models.Instances;
using SystemConsole = System.Console;

namespace InstanceBackupManager.Console.Menus;

/// <summary>
/// Displays and dispatches the operations available for a configured instance.
/// </summary>
internal sealed class InstanceMenu
{
    #region Properties

    /// <summary>
    /// Gets the workflow used to create backups.
    /// </summary>
    private BackupWorkflow BackupWorkflow { get; }

    /// <summary>
    /// Gets the workflow used to restore completed backups.
    /// </summary>
    private RestoreWorkflow RestoreWorkflow { get; }

    /// <summary>
    /// Gets the workflow used to clear configured instance data.
    /// </summary>
    private ClearWorkflow ClearWorkflow { get; }

    /// <summary>
    /// Gets the workflow used to delete completed backups.
    /// </summary>
    private BackupMaintenanceWorkflow BackupMaintenanceWorkflow { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance menu.
    /// </summary>
    /// <param name="backupWorkflow">The workflow used to create backups.</param>
    /// <param name="restoreWorkflow">The workflow used to restore completed backups.</param>
    /// <param name="clearWorkflow">The workflow used to clear configured instance data.</param>
    /// <param name="backupMaintenanceWorkflow">The workflow used to delete completed backups.</param>
    /// <exception cref="ArgumentNullException">Thrown when any supplied workflow is null.</exception>
    internal InstanceMenu(
        BackupWorkflow backupWorkflow,
        RestoreWorkflow restoreWorkflow,
        ClearWorkflow clearWorkflow,
        BackupMaintenanceWorkflow backupMaintenanceWorkflow
    )
    {
        ArgumentNullException.ThrowIfNull(backupWorkflow);
        ArgumentNullException.ThrowIfNull(restoreWorkflow);
        ArgumentNullException.ThrowIfNull(clearWorkflow);
        ArgumentNullException.ThrowIfNull(backupMaintenanceWorkflow);

        BackupWorkflow = backupWorkflow;
        RestoreWorkflow = restoreWorkflow;
        ClearWorkflow = clearWorkflow;
        BackupMaintenanceWorkflow = backupMaintenanceWorkflow;
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Displays the available operations for a configured instance and returns to instance selection when the user exits the menu.
    /// </summary>
    /// <param name="instance">The loaded instance selected by the user.</param>
    /// <returns>Zero when the user returns to instance selection; otherwise, the nonzero result returned by a failed workflow.</returns>
    internal int Run(InstanceContext instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var canClear = instance.Config.Targets.Any(
            target => target.Enabled && target.AllowClear
        );

        while (true)
        {
            SystemConsole.WriteLine();
            SystemConsole.WriteLine(instance.Config.Name);
            SystemConsole.WriteLine(new string('=', instance.Config.Name.Length));
            SystemConsole.WriteLine();
            SystemConsole.WriteLine("1. Back up now");
            SystemConsole.WriteLine("2. Restore from backup");

            if (canClear)
            {
                SystemConsole.WriteLine("3. Clear instance data");
            }

            SystemConsole.WriteLine("4. Manage backups");
            SystemConsole.WriteLine("0. Return to instances");
            SystemConsole.WriteLine();
            SystemConsole.Write("Selection: ");

            var input = SystemConsole.ReadLine();

            if (input is null)
            {
                return 0;
            }

            var result = input.Trim() switch
            {
                "0" => 0,
                "1" => BackupWorkflow.Run(instance),
                "2" => RestoreWorkflow.Run(instance),
                "3" when canClear => ClearWorkflow.Run(instance),
                "4" => BackupMaintenanceWorkflow.Run(instance),
                _ => ShowInvalidSelection()
            };

            if (input.Trim() == "0" || result != 0)
            {
                return result;
            }
        }
    }

    #endregion

    #region Menu Helpers

    /// <summary>
    /// Displays the invalid-selection message and returns a successful result so the instance menu remains active.
    /// </summary>
    /// <returns>Zero so the menu continues displaying.</returns>
    private static int ShowInvalidSelection()
    {
        ConsoleHelper.ShowInvalidSelectionMessage();

        return 0;
    }

    #endregion
}