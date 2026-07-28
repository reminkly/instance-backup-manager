using InstanceBackupManager.Console.Menus;
using InstanceBackupManager.Console.Workflows;
using InstanceBackupManager.Processing;

namespace InstanceBackupManager.Console;

/// <summary>
/// Provides the console application entry point.
/// </summary>
internal static class Program
{
    #region Private Methods

    /// <summary>
    /// Creates and starts the console application.
    /// </summary>
    /// <returns>Zero when the application exits normally; otherwise, one.</returns>
    private static int Main()
    {
        var backupProcessor = new BackupProcessor();
        var restoreProcessor = new RestoreProcessor();

        var backupMaintenanceProcessor = new BackupMaintenanceProcessor(
            restoreProcessor,
            TimeProvider.System
        );

        var backupRetentionProcessor = new BackupRetentionProcessor(
            restoreProcessor,
            backupMaintenanceProcessor
        );

        var backupWorkflow = new BackupWorkflow(
            backupProcessor,
            backupRetentionProcessor
        );

        var restoreWorkflow = new RestoreWorkflow(
            restoreProcessor,
            backupProcessor,
            backupRetentionProcessor
        );

        var clearWorkflow = new ClearWorkflow(
            new ClearProcessor()
        );

        var backupMaintenanceWorkflow = new BackupMaintenanceWorkflow(
            restoreProcessor,
            backupMaintenanceProcessor
        );

        var instanceMenu = new InstanceMenu(
            backupWorkflow,
            restoreWorkflow,
            clearWorkflow,
            backupMaintenanceWorkflow
        );

        var application = new ConsoleApplication(
            new ConfigProcessor(),
            instanceMenu
        );

        return application.Run();
    }

    #endregion
}