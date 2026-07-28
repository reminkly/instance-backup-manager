using InstanceBackupManager.Console.Commands;
using InstanceBackupManager.Console.Logging;
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
        var applicationPath = AppContext.BaseDirectory;
        var logger = new FileApplicationLogger(
            Path.Combine(
                applicationPath,
                "logs"
            )
        );

        var configProcessor = new ConfigProcessor();
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

        var validationWorkflow = new ValidationWorkflow(
            new InstanceValidationProcessor(
                configProcessor,
                backupCatalog
            )
        );

        IReadOnlyCollection<IInstanceCommand> commands =
        [
            new BackupInstanceCommand(backupWorkflow),
            new RestoreInstanceCommand(restoreWorkflow),
            new ClearInstanceCommand(clearWorkflow),
            new ManageBackupsCommand(backupMaintenanceWorkflow),
            new ValidateInstanceCommand(validationWorkflow)
        ];

        IReadOnlyCollection<IInstanceCommand> loggedCommands = commands
            .Select(
                command => (IInstanceCommand)new LoggingInstanceCommandDecorator(
                    command,
                    logger
                )
            )
            .ToList()
            .AsReadOnly();

        var instanceMenu = new InstanceMenu(loggedCommands);

        var application = new ConsoleApplication(
            configProcessor,
            instanceMenu
        );

        return application.Run();
    }

    #endregion
}
