using InstanceBackupManager.Console.Commands;
using InstanceBackupManager.Console.Menus;
using InstanceBackupManager.Console.Workflows;
using InstanceBackupManager.Processing;
using InstanceBackupManager.Processing.Catalogs;

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
        var backupCatalog = new BackupCatalog();
        var backupProcessor = new BackupProcessor();

        var restoreProcessor = new RestoreProcessor(
            backupCatalog,
            TimeProvider.System
        );

        var backupMaintenanceProcessor = new BackupMaintenanceProcessor(
            backupCatalog,
            TimeProvider.System
        );

        var backupRetentionProcessor = new BackupRetentionProcessor(
            backupCatalog,
            backupMaintenanceProcessor
        );

        var backupWorkflow = new BackupWorkflow(
            backupProcessor,
            backupRetentionProcessor
        );

        var restoreWorkflow = new RestoreWorkflow(
            backupCatalog,
            restoreProcessor,
            backupProcessor,
            backupRetentionProcessor
        );

        var clearWorkflow = new ClearWorkflow(
            new ClearProcessor()
        );

        var backupMaintenanceWorkflow = new BackupMaintenanceWorkflow(
            backupCatalog,
            backupMaintenanceProcessor
        );

        IReadOnlyCollection<IInstanceCommand> instanceCommands =
        [
            new BackupInstanceCommand(backupWorkflow),
            new RestoreInstanceCommand(restoreWorkflow),
            new ClearInstanceCommand(clearWorkflow),
            new ManageBackupsCommand(backupMaintenanceWorkflow)
        ];

        var instanceMenu = new InstanceMenu(instanceCommands);

        var application = new ConsoleApplication(
            new ConfigProcessor(),
            instanceMenu
        );

        return application.Run();
    }

    #endregion
}