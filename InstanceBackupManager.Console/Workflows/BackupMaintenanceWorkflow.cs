using InstanceBackupManager.Console.Constants;
using InstanceBackupManager.Console.Menus;
using InstanceBackupManager.Console.Utilities;
using InstanceBackupManager.Processing;
using InstanceBackupManager.Processing.Catalogs;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Backups;
using InstanceBackupManager.Processing.Models.Instances;
using SystemConsole = System.Console;

namespace InstanceBackupManager.Console.Workflows;

/// <summary>
/// Runs completed-backup selection, individual deletion, bulk deletion, confirmation, and result display.
/// </summary>
internal sealed class BackupMaintenanceWorkflow
{
    #region Properties

    /// <summary>
    /// Gets the catalog used to discover completed backups available for maintenance.
    /// </summary>
    private BackupCatalog BackupCatalog { get; }

    /// <summary>
    /// Gets the processor used to delete validated completed backups.
    /// </summary>
    private BackupMaintenanceProcessor BackupMaintenanceProcessor { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new backup-maintenance workflow.
    /// </summary>
    /// <param name="backupCatalog">The catalog used to discover completed backups available for maintenance.</param>
    /// <param name="backupMaintenanceProcessor">The processor used to delete completed backups.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="backupCatalog"/> or <paramref name="backupMaintenanceProcessor"/> is null.
    /// </exception>
    internal BackupMaintenanceWorkflow(
        BackupCatalog backupCatalog,
        BackupMaintenanceProcessor backupMaintenanceProcessor
    )
    {
        ArgumentNullException.ThrowIfNull(backupCatalog);
        ArgumentNullException.ThrowIfNull(backupMaintenanceProcessor);

        BackupCatalog = backupCatalog;
        BackupMaintenanceProcessor = backupMaintenanceProcessor;
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Displays the backup-maintenance menu and dispatches the selected deletion operation.
    /// </summary>
    /// <param name="instance">The loaded instance whose completed backups can be managed.</param>
    /// <returns>Zero when the operation succeeds or is cancelled; otherwise, one.</returns>
    internal int Run(InstanceContext instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        try
        {
            var selection = ConsoleMenu.Select(
                "Backup Management",
                new List<ConsoleMenuItem<int>>
                {
                    new("1", "Delete one backup", 1),
                    new("2", "Delete all backups", 2),
                    new("0", "Return", 0, IsCancellation: true)
                }.AsReadOnly()
            );

            if (selection.IsCancelled)
            {
                return 0;
            }

            return selection.Value switch
            {
                1 => DeleteOneBackup(instance),
                2 => DeleteAllBackups(instance),
                _ => 0
            };
        }
        catch (Exception exception)
        {
            SystemConsole.WriteLine();
            SystemConsole.WriteLine("Backup maintenance could not be completed.");
            SystemConsole.WriteLine(exception.Message);

            ConsoleHelper.WaitForContinue();

            return 1;
        }
    }

    #endregion

    #region Individual Deletion

    /// <summary>
    /// Prompts the user to select and confirm one completed backup, then deletes the selected backup.
    /// </summary>
    /// <param name="instance">The loaded instance that owns the completed backup.</param>
    /// <returns>Zero when the deletion succeeds or is cancelled; otherwise, one.</returns>
    private int DeleteOneBackup(InstanceContext instance)
    {
        var backups = DiscoverBackups(instance);

        if (backups.Count == 0)
        {
            ShowNoBackupsMessage();

            return 0;
        }

        var selectedBackup = PromptForBackup(backups);

        if (selectedBackup is null)
        {
            return 0;
        }

        if (!ConfirmSingleDeletion(selectedBackup))
        {
            ShowDeletionCancelledMessage();

            return 0;
        }

        SystemConsole.WriteLine();
        SystemConsole.WriteLine("Deleting backup...");

        var result = BackupMaintenanceProcessor.DeleteBackup(
            instance,
            selectedBackup.BackupName
        );

        var resultEntry = result.Entries.Single();

        SystemConsole.WriteLine();
        SystemConsole.WriteLine("Backup deleted successfully.");
        SystemConsole.WriteLine($"Backup: {resultEntry.BackupName}");
        SystemConsole.WriteLine($"Files:  {resultEntry.FileCount}");
        SystemConsole.WriteLine($"Bytes:  {resultEntry.TotalBytes}");

        ConsoleHelper.WaitForContinue();

        return 0;
    }

    /// <summary>
    /// Displays completed backups and prompts the user to select one for deletion.
    /// </summary>
    /// <param name="backups">The completed backups available for deletion.</param>
    /// <returns>The selected backup, or <see langword="null"/> when the user chooses to return.</returns>
    private static BackupDescriptor? PromptForBackup(IReadOnlyList<BackupDescriptor> backups)
    {
        var items = backups
            .Select(
                (backup, index) => new ConsoleMenuItem<BackupDescriptor?>(
                    index < 9
                        ? (index + 1).ToString()
                        : null,
                    CreateBackupDisplayLabel(backup),
                    backup
                )
            )
            .Append(
                new ConsoleMenuItem<BackupDescriptor?>(
                    "0",
                    "Return",
                    Value: null,
                    IsCancellation: true
                )
            )
            .ToList()
            .AsReadOnly();

        var result = ConsoleMenu.Select(
            "Completed Backups",
            items
        );

        return result.IsCancelled
            ? null
            : result.Value;
    }

    /// <summary>
    /// Displays information about one selected backup and asks the user to confirm deletion.
    /// </summary>
    /// <param name="backup">The selected completed backup.</param>
    /// <returns><see langword="true"/> when the user confirms deletion; otherwise, <see langword="false"/>.</returns>
    private static bool ConfirmSingleDeletion(BackupDescriptor backup)
    {
        var createdLocal = backup.Manifest.CreatedUtc.ToLocalTime();
        var fileCount = backup.Manifest.Entries.Sum(entry => entry.FileCount);
        var totalBytes = backup.Manifest.Entries.Sum(entry => entry.TotalBytes);
        var details = string.Join(
            Environment.NewLine,
            $"Backup:  {backup.BackupName}",
            $"Kind:    {GetBackupKindDisplayName(backup.Manifest.Kind)}",
            $"Created: {createdLocal:yyyy-MM-dd HH:mm:ss}",
            $"Files:   {fileCount}",
            $"Bytes:   {totalBytes}",
            $"Path:    {backup.BackupPath}",
            string.Empty,
            "Delete this backup?"
        );

        var result = ConsoleMenu.Select(
            "Delete Backup",
            new List<ConsoleMenuItem<bool>>
            {
                new("n", "No, keep this backup", false),
                new("y", "Yes, permanently delete it", true)
            }.AsReadOnly(),
            details
        );

        return !result.IsCancelled && result.Value;
    }

    #endregion

    #region Bulk Deletion

    /// <summary>
    /// Displays a bulk-deletion summary, requires two confirmations, and deletes all completed backups.
    /// </summary>
    /// <param name="instance">The loaded instance whose completed backups will be deleted.</param>
    /// <returns>Zero when the deletion succeeds or is cancelled; otherwise, one.</returns>
    private int DeleteAllBackups(InstanceContext instance)
    {
        var backups = DiscoverBackups(instance);

        if (backups.Count == 0)
        {
            ShowNoBackupsMessage();

            return 0;
        }

        if (!ConfirmAllDeletion(instance, backups))
        {
            ShowDeletionCancelledMessage();

            return 0;
        }

        SystemConsole.WriteLine();
        SystemConsole.WriteLine("Deleting completed backups...");

        var result = BackupMaintenanceProcessor.DeleteAllBackups(instance);
        var fileCount = result.Entries.Sum(entry => entry.FileCount);
        var totalBytes = result.Entries.Sum(entry => entry.TotalBytes);

        SystemConsole.WriteLine();
        SystemConsole.WriteLine("All completed backups were deleted successfully.");
        SystemConsole.WriteLine($"Backups: {result.Entries.Count}");
        SystemConsole.WriteLine($"Files:   {fileCount}");
        SystemConsole.WriteLine($"Bytes:   {totalBytes}");

        ConsoleHelper.WaitForContinue();

        return 0;
    }

    /// <summary>
    /// Displays a bulk-deletion warning and requires both the exact instance name and the phrase DELETE ALL.
    /// </summary>
    /// <param name="instance">The loaded instance whose completed backups will be deleted.</param>
    /// <param name="backups">The validated completed backups included in the operation.</param>
    /// <returns><see langword="true"/> when both confirmation values match; otherwise, <see langword="false"/>.</returns>
    private static bool ConfirmAllDeletion(
        InstanceContext instance,
        IReadOnlyCollection<BackupDescriptor> backups
    )
    {
        var fileCount = backups.Sum(
            backup => backup.Manifest.Entries.Sum(entry => entry.FileCount)
        );

        var totalBytes = backups.Sum(
            backup => backup.Manifest.Entries.Sum(entry => entry.TotalBytes)
        );

        SystemConsole.WriteLine();
        SystemConsole.WriteLine("Delete All Backups");
        SystemConsole.WriteLine("==================");
        SystemConsole.WriteLine();
        SystemConsole.WriteLine("Every completed backup for this instance will be permanently deleted.");
        SystemConsole.WriteLine("In-progress and unrelated directories will remain unchanged.");
        SystemConsole.WriteLine();
        SystemConsole.WriteLine($"Instance: {instance.Config.Name}");
        SystemConsole.WriteLine($"Backups:  {backups.Count}");
        SystemConsole.WriteLine($"Files:    {fileCount}");
        SystemConsole.WriteLine($"Bytes:    {totalBytes}");
        SystemConsole.WriteLine();
        SystemConsole.Write($"Type the exact instance name '{instance.Config.Name}': ");

        var instanceConfirmation = SystemConsole.ReadLine();

        if (!string.Equals(instanceConfirmation, instance.Config.Name, StringComparison.Ordinal))
        {
            return false;
        }

        SystemConsole.Write("Type DELETE ALL to confirm: ");

        var deletionConfirmation = SystemConsole.ReadLine();

        return string.Equals(
            deletionConfirmation,
            "DELETE ALL",
            StringComparison.Ordinal
        );
    }

    #endregion

    #region Backup Discovery

    /// <summary>
    /// Discovers and materializes the completed backups available for maintenance.
    /// </summary>
    /// <param name="instance">The loaded instance whose backups will be discovered.</param>
    /// <returns>A list of completed backups ordered from newest to oldest.</returns>
    private IReadOnlyList<BackupDescriptor> DiscoverBackups(InstanceContext instance)
    {
        return BackupCatalog
            .DiscoverBackups(instance)
            .ToList();
    }

    #endregion

    #region Display Helpers

    /// <summary>
    /// Creates the compact label displayed for one completed backup.
    /// </summary>
    /// <param name="backup">The completed backup to describe.</param>
    /// <returns>A user-facing backup label.</returns>
    private static string CreateBackupDisplayLabel(BackupDescriptor backup)
    {
        var createdLocal = backup.Manifest.CreatedUtc.ToLocalTime();
        var fileCount = backup.Manifest.Entries.Sum(entry => entry.FileCount);
        var totalBytes = backup.Manifest.Entries.Sum(entry => entry.TotalBytes);
        var fileLabel = fileCount == 1
            ? "file"
            : "files";

        return $"{createdLocal:yyyy-MM-dd HH:mm:ss} [{GetBackupKindDisplayName(backup.Manifest.Kind)}] - " +
               $"{fileCount} {fileLabel}, {totalBytes} bytes";
    }

    /// <summary>
    /// Displays one numbered completed-backup entry.
    /// </summary>
    /// <param name="backup">The completed backup to display.</param>
    /// <param name="selectionNumber">The menu number assigned to the backup.</param>
    private static void DisplayBackupListEntry(
        BackupDescriptor backup,
        int selectionNumber
    )
    {
        var createdLocal = backup.Manifest.CreatedUtc.ToLocalTime();
        var fileCount = backup.Manifest.Entries.Sum(entry => entry.FileCount);
        var totalBytes = backup.Manifest.Entries.Sum(entry => entry.TotalBytes);
        var fileLabel = fileCount == 1
            ? "file"
            : "files";

        SystemConsole.WriteLine(
            $"{selectionNumber}. {createdLocal:yyyy-MM-dd HH:mm:ss} " +
            $"[{GetBackupKindDisplayName(backup.Manifest.Kind)}] - {fileCount} {fileLabel}, {totalBytes} bytes"
        );
    }

    /// <summary>
    /// Gets the user-facing label for a backup kind.
    /// </summary>
    /// <param name="kind">The backup kind to display.</param>
    /// <returns>The user-facing backup-kind label.</returns>
    private static string GetBackupKindDisplayName(BackupKind kind)
    {
        return kind switch
        {
            BackupKind.Manual => ConsoleMessages.ManualBackupKind,
            BackupKind.PreRestore => ConsoleMessages.PreRestoreBackupKind,
            _ => kind.ToString()
        };
    }

    /// <summary>
    /// Displays a message when no completed backups are available for deletion.
    /// </summary>
    private static void ShowNoBackupsMessage()
    {
        SystemConsole.WriteLine();
        SystemConsole.WriteLine(ConsoleMessages.NoCompletedBackups);

        ConsoleHelper.WaitForContinue();
    }

    /// <summary>
    /// Displays a consistent backup-deletion cancellation message.
    /// </summary>
    private static void ShowDeletionCancelledMessage()
    {
        SystemConsole.WriteLine();
        SystemConsole.WriteLine("Backup deletion cancelled.");

        ConsoleHelper.WaitForContinue();
    }

    #endregion
}