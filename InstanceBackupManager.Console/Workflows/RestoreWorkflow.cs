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
/// Runs backup selection, restore confirmation, optional pre-restore backup creation, restoration, and result display.
/// </summary>
internal sealed class RestoreWorkflow
{
    #region Properties

    /// <summary>
    /// Gets the catalog used to discover completed backups available for restoration.
    /// </summary>
    private BackupCatalog BackupCatalog { get; }

    /// <summary>
    /// Gets the processor used to restore completed backups.
    /// </summary>
    private RestoreProcessor RestoreProcessor { get; }

    /// <summary>
    /// Gets the processor used to create an optional safety backup before restoration.
    /// </summary>
    private BackupProcessor BackupProcessor { get; }

    /// <summary>
    /// Gets the processor used to apply configured pre-restore backup retention.
    /// </summary>
    private BackupRetentionProcessor BackupRetentionProcessor { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new restore workflow.
    /// </summary>
    /// <param name="backupCatalog">The catalog used to discover completed backups available for restoration.</param>
    /// <param name="restoreProcessor">The processor used to restore completed backups.</param>
    /// <param name="backupProcessor">The processor used to create an optional pre-restore backup.</param>
    /// <param name="backupRetentionProcessor">The processor used to apply configured pre-restore retention.</param>
    /// <exception cref="ArgumentNullException">Thrown when any supplied dependency is null.</exception>
    internal RestoreWorkflow(
        BackupCatalog backupCatalog,
        RestoreProcessor restoreProcessor,
        BackupProcessor backupProcessor,
        BackupRetentionProcessor backupRetentionProcessor
    )
    {
        ArgumentNullException.ThrowIfNull(backupCatalog);
        ArgumentNullException.ThrowIfNull(restoreProcessor);
        ArgumentNullException.ThrowIfNull(backupProcessor);
        ArgumentNullException.ThrowIfNull(backupRetentionProcessor);

        BackupCatalog = backupCatalog;
        RestoreProcessor = restoreProcessor;
        BackupProcessor = backupProcessor;
        BackupRetentionProcessor = backupRetentionProcessor;
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Prompts the user to select and confirm a completed backup, optionally creates a safety backup, and restores the selected backup.
    /// </summary>
    /// <param name="instance">The loaded instance receiving the restored data.</param>
    /// <returns>Zero when the operation succeeds or is cancelled; otherwise, one.</returns>
    internal int Run(InstanceContext instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        try
        {
            var backups = BackupCatalog
                .DiscoverBackups(instance)
                .ToList();

            if (backups.Count == 0)
            {
                SystemConsole.WriteLine();
                SystemConsole.WriteLine(ConsoleMessages.NoCompletedBackups);

                ConsoleHelper.WaitForContinue();

                return 0;
            }

            var selectedBackup = PromptForBackup(backups);

            if (selectedBackup is null)
            {
                return 0;
            }

            if (!ConfirmRestore(instance, selectedBackup))
            {
                ShowRestoreCancelledMessage();

                return 0;
            }

            var preRestoreBackupChoice = PromptForPreRestoreBackup();

            if (preRestoreBackupChoice == PreRestoreBackupChoice.Cancel)
            {
                ShowRestoreCancelledMessage();

                return 0;
            }

            var preRestoreBackupCreated = false;

            if (preRestoreBackupChoice == PreRestoreBackupChoice.Create)
            {
                CreatePreRestoreBackup(instance);
                preRestoreBackupCreated = true;
            }

            SystemConsole.WriteLine();
            SystemConsole.WriteLine("Restoring backup...");

            var result = RestoreProcessor.RestoreBackup(
                instance,
                selectedBackup.BackupName
            );

            var fileCount = result.Entries.Sum(entry => entry.FileCount);
            var totalBytes = result.Entries.Sum(entry => entry.TotalBytes);

            SystemConsole.WriteLine();
            SystemConsole.WriteLine("Restore completed successfully.");
            SystemConsole.WriteLine($"Backup: {result.BackupName}");
            SystemConsole.WriteLine($"Files:  {fileCount}");
            SystemConsole.WriteLine($"Bytes:  {totalBytes}");
            SystemConsole.WriteLine();

            foreach (var entry in result.Entries)
            {
                SystemConsole.WriteLine($"{entry.TargetName}: {entry.DestinationPath}");
            }

            if (preRestoreBackupCreated)
            {
                ApplyPreRestoreRetention(instance);
            }

            ConsoleHelper.WaitForContinue();

            return 0;
        }
        catch (Exception exception)
        {
            SystemConsole.WriteLine();
            SystemConsole.WriteLine("The restore could not be completed.");
            SystemConsole.WriteLine(exception.Message);

            ConsoleHelper.WaitForContinue();

            return 1;
        }
    }

    #endregion

    #region Backup Selection

    /// <summary>
    /// Displays completed backups and prompts the user to select one.
    /// </summary>
    /// <param name="backups">The completed backups available for selection.</param>
    /// <returns>The selected backup, or <see langword="null"/> when the user chooses to exit.</returns>
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
            "Available Backups",
            items
        );

        return result.IsCancelled
            ? null
            : result.Value;
    }

    #endregion

    #region Restore Confirmation

    /// <summary>
    /// Displays restore warnings and asks the user to confirm the operation.
    /// </summary>
    /// <param name="instance">The current instance configuration.</param>
    /// <param name="backup">The selected backup and its historical manifest.</param>
    /// <returns><see langword="true"/> when the user confirms the restore; otherwise, <see langword="false"/>.</returns>
    private static bool ConfirmRestore(
        InstanceContext instance,
        BackupDescriptor backup
    )
    {
        var details = new List<string>
        {
            $"Backup: {backup.BackupName}",
            string.Empty,
            "Files contained in this backup will overwrite matching files at the current destinations.",
            "Files not contained in the backup will remain unchanged."
        };

        var changedDestinations = backup.Manifest.Entries
            .Select(
                manifestEntry => new
                {
                    ManifestEntry = manifestEntry,
                    CurrentTarget = instance.Config.Targets.SingleOrDefault(
                        target => string.Equals(
                            target.Id,
                            manifestEntry.TargetId,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                }
            )
            .Where(item => item.CurrentTarget is not null && item.CurrentTarget.Enabled)
            .Where(
                item => !string.Equals(
                    item.ManifestEntry.Source,
                    item.CurrentTarget!.Source,
                    ConsoleHelper.GetPathComparison()
                )
            )
            .ToList();

        if (changedDestinations.Count > 0)
        {
            details.Add(string.Empty);
            details.Add("The following destinations have changed since this backup was created:");

            foreach (var item in changedDestinations)
            {
                details.Add(string.Empty);
                details.Add($"Target:   {item.CurrentTarget!.Name}");
                details.Add($"Previous: {item.ManifestEntry.Source}");
                details.Add($"Current:  {item.CurrentTarget.Source}");
            }
        }

        details.Add(string.Empty);
        details.Add("Continue with the restore?");

        var result = ConsoleMenu.Select(
            "Restore Confirmation",
            new List<ConsoleMenuItem<bool>>
            {
                new("n", "No, cancel the restore", false),
                new("y", "Yes, continue with the restore", true)
            }.AsReadOnly(),
            string.Join(
                Environment.NewLine,
                details
            )
        );

        return !result.IsCancelled && result.Value;
    }

    /// <summary>
    /// Prompts the user to create, skip, or cancel the optional safety backup taken before restoration.
    /// </summary>
    /// <returns>The user's selected pre-restore backup action.</returns>
    private static PreRestoreBackupChoice PromptForPreRestoreBackup()
    {
        var result = ConsoleMenu.Select(
            "Pre-restore Safety Backup",
            new List<ConsoleMenuItem<PreRestoreBackupChoice>>
            {
                new("y", "Create a safety backup before restoring", PreRestoreBackupChoice.Create),
                new("n", "Continue without creating a safety backup", PreRestoreBackupChoice.Skip),
                new("c", "Cancel the restore", PreRestoreBackupChoice.Cancel, IsCancellation: true)
            }.AsReadOnly(),
            "A safety backup can preserve the current data before it is overwritten."
        );

        return result.IsCancelled
            ? PreRestoreBackupChoice.Cancel
            : result.Value;
    }

    /// <summary>
    /// Displays a consistent restore-cancellation message and waits for the user before exiting.
    /// </summary>
    private static void ShowRestoreCancelledMessage()
    {
        SystemConsole.WriteLine();
        SystemConsole.WriteLine("Restore cancelled.");

        ConsoleHelper.WaitForContinue();
    }

    #endregion

    #region Pre-Restore Backup

    /// <summary>
    /// Creates and displays a safety backup containing the instance's current data before restoration.
    /// </summary>
    /// <param name="instance">The loaded instance whose current data will be backed up.</param>
    private void CreatePreRestoreBackup(InstanceContext instance)
    {
        SystemConsole.WriteLine();
        SystemConsole.WriteLine("Creating pre-restore backup...");

        var manifest = BackupProcessor.CreateBackup(
            instance,
            BackupKind.PreRestore
        );
        var backupPath = Path.Combine(instance.BackupsPath, manifest.BackupName);
        var fileCount = manifest.Entries.Sum(entry => entry.FileCount);
        var totalBytes = manifest.Entries.Sum(entry => entry.TotalBytes);

        SystemConsole.WriteLine();
        SystemConsole.WriteLine("Pre-restore backup completed successfully.");
        SystemConsole.WriteLine($"Backup: {manifest.BackupName}");
        SystemConsole.WriteLine($"Files:  {fileCount}");
        SystemConsole.WriteLine($"Bytes:  {totalBytes}");
        SystemConsole.WriteLine($"Path:   {backupPath}");
    }

    #endregion

    #region Display Helpers

    /// <summary>
    /// Creates the compact label displayed for one completed backup.
    /// </summary>
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

    #endregion

    #region Retention

    /// <summary>
    /// Applies configured pre-restore retention after the selected backup has been restored successfully.
    /// </summary>
    /// <param name="instance">The loaded instance whose pre-restore retention settings will be applied.</param>
    private void ApplyPreRestoreRetention(InstanceContext instance)
    {
        try
        {
            var result = BackupRetentionProcessor.ApplyRetention(
                instance,
                BackupKind.PreRestore
            );

            if (result.Entries.Count == 0)
            {
                return;
            }

            var fileCount = result.Entries.Sum(entry => entry.FileCount);
            var totalBytes = result.Entries.Sum(entry => entry.TotalBytes);
            var backupLabel = result.Entries.Count == 1
                ? "backup"
                : "backups";

            SystemConsole.WriteLine();
            SystemConsole.WriteLine(
                $"Retention removed {result.Entries.Count} older pre-restore {backupLabel}, " +
                $"{fileCount} files, and {totalBytes} bytes."
            );
        }
        catch (Exception exception)
        {
            SystemConsole.WriteLine();
            SystemConsole.WriteLine("The restore completed, but pre-restore retention could not be applied.");
            SystemConsole.WriteLine(exception.Message);
        }
    }

    #endregion

    #region Private Types

    /// <summary>
    /// Defines the actions available when deciding whether to create a pre-restore backup.
    /// </summary>
    private enum PreRestoreBackupChoice
    {
        /// <summary>
        /// Creates a backup before continuing with the restore.
        /// </summary>
        Create,

        /// <summary>
        /// Continues with the restore without creating a backup.
        /// </summary>
        Skip,

        /// <summary>
        /// Cancels the restore without making any changes.
        /// </summary>
        Cancel
    }

    #endregion
}