using InstanceBackupManager.Console.Utilities;
using InstanceBackupManager.Processing;
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
    /// Gets the processor used to discover completed backups.
    /// </summary>
    private RestoreProcessor RestoreProcessor { get; }

    /// <summary>
    /// Gets the processor used to delete validated completed backups.
    /// </summary>
    private BackupMaintenanceProcessor BackupMaintenanceProcessor { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new backup-maintenance workflow.
    /// </summary>
    /// <param name="restoreProcessor">The processor used to discover completed backups.</param>
    /// <param name="backupMaintenanceProcessor">The processor used to delete completed backups.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="restoreProcessor"/> or <paramref name="backupMaintenanceProcessor"/> is null.
    /// </exception>
    internal BackupMaintenanceWorkflow(
        RestoreProcessor restoreProcessor,
        BackupMaintenanceProcessor backupMaintenanceProcessor
    )
    {
        ArgumentNullException.ThrowIfNull(restoreProcessor);
        ArgumentNullException.ThrowIfNull(backupMaintenanceProcessor);

        RestoreProcessor = restoreProcessor;
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
            while (true)
            {
                SystemConsole.WriteLine();
                SystemConsole.WriteLine("Backup Management");
                SystemConsole.WriteLine("=================");
                SystemConsole.WriteLine();
                SystemConsole.WriteLine("1. Delete one backup");
                SystemConsole.WriteLine("2. Delete all backups");
                SystemConsole.WriteLine("0. Return");
                SystemConsole.WriteLine();
                SystemConsole.Write("Selection: ");

                var input = SystemConsole.ReadLine();

                switch (input?.Trim())
                {
                    case "0":
                    case null:
                        return 0;

                    case "1":
                        return DeleteOneBackup(instance);

                    case "2":
                        return DeleteAllBackups(instance);

                    default:
                        ConsoleHelper.ShowInvalidSelectionMessage();
                        break;
                }
            }
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
        while (true)
        {
            SystemConsole.WriteLine();
            SystemConsole.WriteLine("Completed Backups");
            SystemConsole.WriteLine("=================");
            SystemConsole.WriteLine();

            for (var index = 0; index < backups.Count; index++)
            {
                DisplayBackupListEntry(
                    backups[index],
                    index + 1
                );
            }

            SystemConsole.WriteLine("0. Return");
            SystemConsole.WriteLine();
            SystemConsole.Write("Selection: ");

            var input = SystemConsole.ReadLine();

            if (input is null)
            {
                return null;
            }

            if (!int.TryParse(input, out var selection))
            {
                ConsoleHelper.ShowInvalidSelectionMessage();
                continue;
            }

            if (selection == 0)
            {
                return null;
            }

            if (selection < 1 || selection > backups.Count)
            {
                ConsoleHelper.ShowInvalidSelectionMessage();
                continue;
            }

            return backups[selection - 1];
        }
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

        SystemConsole.WriteLine();
        SystemConsole.WriteLine("Delete Backup");
        SystemConsole.WriteLine("=============");
        SystemConsole.WriteLine();
        SystemConsole.WriteLine($"Backup:  {backup.BackupName}");
        SystemConsole.WriteLine($"Kind:    {GetBackupKindDisplayName(backup.Manifest.Kind)}");
        SystemConsole.WriteLine($"Created: {createdLocal:yyyy-MM-dd HH:mm:ss}");
        SystemConsole.WriteLine($"Files:   {fileCount}");
        SystemConsole.WriteLine($"Bytes:   {totalBytes}");
        SystemConsole.WriteLine($"Path:    {backup.BackupPath}");
        SystemConsole.WriteLine();
        SystemConsole.Write("Delete this backup? [y/N]: ");

        var input = SystemConsole.ReadLine();

        return string.Equals(
            input?.Trim(),
            "y",
            StringComparison.OrdinalIgnoreCase
        );
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
        return RestoreProcessor
            .DiscoverBackups(instance)
            .ToList();
    }

    #endregion

    #region Display Helpers

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
            BackupKind.Manual => "Manual",
            BackupKind.PreRestore => "Pre-restore",
            _ => kind.ToString()
        };
    }

    /// <summary>
    /// Displays a message when no completed backups are available for deletion.
    /// </summary>
    private static void ShowNoBackupsMessage()
    {
        SystemConsole.WriteLine();
        SystemConsole.WriteLine("No completed backups are available for this instance.");

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