using InstanceBackupManager.Console.Constants;
using InstanceBackupManager.Console.Menus;
using InstanceBackupManager.Console.Utilities;
using InstanceBackupManager.Processing;
using InstanceBackupManager.Processing.Catalogs;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Backups;
using InstanceBackupManager.Processing.Models.Instances;
using InstanceBackupManager.Processing.Models.Restore;
using InstanceBackupManager.Processing.Policies;
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

    /// <summary>
    /// Gets the processor used to compare backup payloads with current destinations before restoration.
    /// </summary>
    private RestorePreviewProcessor RestorePreviewProcessor { get; }

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
        RestorePreviewProcessor = new RestorePreviewProcessor(backupCatalog);
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

            var preview = RestorePreviewProcessor.CreatePreview(
                instance,
                selectedBackup.BackupName
            );

            var selectedTargetIds = PromptForRestoreTargets(preview);

            if (selectedTargetIds is null)
            {
                ShowRestoreCancelledMessage();
                return 0;
            }

            ShowRestorePreview(
                selectedBackup,
                preview,
                selectedTargetIds
            );

            ConsoleHelper.WaitForContinue();

            if (!ConfirmRestore(
                instance,
                selectedBackup,
                selectedTargetIds
            ))
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
                CreatePreRestoreBackup(
                    instance,
                    selectedBackup
                );
                preRestoreBackupCreated = true;
            }

            SystemConsole.WriteLine();
            SystemConsole.WriteLine("Restoring backup...");

            var result = RestoreProcessor.RestoreBackup(
                instance,
                selectedBackup.BackupName,
                selectedTargetIds
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

    #region Target Selection and Preview

    /// <summary>
    /// Prompts the user to restore every available target or choose a subset.
    /// </summary>
    private static IReadOnlyCollection<string>? PromptForRestoreTargets(RestorePreview preview)
    {
        if (preview.Targets.Count == 1)
        {
            return preview.Targets
                .Select(target => target.TargetId)
                .ToList()
                .AsReadOnly();
        }

        var scopeResult = ConsoleMenu.Select(
            "Restore Scope",
            new List<ConsoleMenuItem<RestoreScopeChoice>>
            {
                new("a", "Restore all available targets", RestoreScopeChoice.All),
                new("s", "Select targets", RestoreScopeChoice.Select),
                new("c", "Cancel restore", RestoreScopeChoice.Cancel, IsCancellation: true)
            }.AsReadOnly()
        );

        if (scopeResult.IsCancelled || scopeResult.Value == RestoreScopeChoice.Cancel)
        {
            return null;
        }

        if (scopeResult.Value == RestoreScopeChoice.All)
        {
            return preview.Targets
                .Select(target => target.TargetId)
                .ToList()
                .AsReadOnly();
        }

        return PromptForSelectedTargets(preview.Targets.ToList());
    }

    /// <summary>
    /// Displays a toggle menu until the user accepts a nonempty target selection or cancels.
    /// </summary>
    private static IReadOnlyCollection<string>? PromptForSelectedTargets(
        IReadOnlyList<RestoreTargetPreview> targets
    )
    {
        const string continueValue = "__continue__";
        var selectedTargetIds = targets
            .Select(target => target.TargetId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        while (true)
        {
            var items = targets
                .Select(
                    (target, index) => new ConsoleMenuItem<string?>(
                        index < 9 ? (index + 1).ToString() : null,
                        $"[{(selectedTargetIds.Contains(target.TargetId) ? 'x' : ' ')}] {target.TargetName}",
                        target.TargetId
                    )
                )
                .Append(
                    new ConsoleMenuItem<string?>(
                        "c",
                        "Continue with selected targets",
                        continueValue,
                        IsEnabled: selectedTargetIds.Count > 0
                    )
                )
                .Append(
                    new ConsoleMenuItem<string?>(
                        "0",
                        "Cancel restore",
                        Value: null,
                        IsCancellation: true
                    )
                )
                .ToList()
                .AsReadOnly();

            var result = ConsoleMenu.Select(
                "Select Restore Targets",
                items,
                "Select a target to toggle it, then choose Continue."
            );

            if (result.IsCancelled || result.Value is null)
            {
                return null;
            }

            if (result.Value == continueValue)
            {
                return selectedTargetIds.ToList().AsReadOnly();
            }

            if (!selectedTargetIds.Remove(result.Value))
            {
                selectedTargetIds.Add(result.Value);
            }
        }
    }

    /// <summary>
    /// Displays file-level changes for every target selected for restoration.
    /// </summary>
    private static void ShowRestorePreview(
        BackupDescriptor backup,
        RestorePreview preview,
        IReadOnlyCollection<string> selectedTargetIds
    )
    {
        var selectedIds = selectedTargetIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        SystemConsole.Clear();
        SystemConsole.WriteLine("Restore Preview");
        SystemConsole.WriteLine("===============");
        SystemConsole.WriteLine();
        SystemConsole.WriteLine($"Backup: {BackupDisplayNamePolicy.GetDisplayName(backup.Manifest)}");

        if (!string.IsNullOrWhiteSpace(backup.Manifest.Notes))
        {
            SystemConsole.WriteLine($"Notes:  {backup.Manifest.Notes}");
        }

        if (backup.Manifest.Tags.Count > 0)
        {
            SystemConsole.WriteLine($"Tags:   {string.Join(", ", backup.Manifest.Tags)}");
        }

        foreach (var target in preview.Targets.Where(target => selectedIds.Contains(target.TargetId)))
        {
            SystemConsole.WriteLine();
            SystemConsole.WriteLine(target.TargetName);
            SystemConsole.WriteLine(new string('-', target.TargetName.Length));
            SystemConsole.WriteLine($"Destination: {target.DestinationPath}");
            SystemConsole.WriteLine($"Create: {target.CreateCount}, overwrite: {target.OverwriteCount}, unchanged: {target.UnchangedCount}, preserve: {target.PreserveCount}");

            foreach (var file in target.Files)
            {
                SystemConsole.WriteLine($"[{file.ChangeKind}] {file.RelativePath}");
            }
        }

        SystemConsole.WriteLine();
        SystemConsole.WriteLine("Preserved files exist only at the destination and will remain unchanged.");
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
        BackupDescriptor backup,
        IReadOnlyCollection<string> selectedTargetIds
    )
    {
        var details = new List<string>
        {
            $"Backup: {backup.BackupName}",
            string.Empty,
            "Files contained in this backup will overwrite matching files at the current destinations.",
            "Files not contained in the backup will remain unchanged."
        };

        var selectedIds = selectedTargetIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var changedDestinations = backup.Manifest.Entries
            .Where(entry => selectedIds.Contains(entry.TargetId))
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

    #region Private Types

    private enum RestoreScopeChoice
    {
        All,
        Select,
        Cancel
    }

    #endregion

    #region Pre-Restore Backup

    /// <summary>
    /// Creates and displays a safety backup containing the instance's current data before restoration.
    /// </summary>
    /// <param name="instance">The loaded instance whose current data will be backed up.</param>
    /// <param name="selectedBackup">The completed backup whose restoration triggered the safety backup.</param>
    private void CreatePreRestoreBackup(
        InstanceContext instance,
        BackupDescriptor selectedBackup
    )
    {
        SystemConsole.WriteLine();
        SystemConsole.WriteLine("Creating pre-restore backup...");

        var displayName = BackupDisplayNamePolicy.CreatePreRestoreDisplayName(selectedBackup);
        var manifest = BackupProcessor.CreateBackup(
            instance,
            BackupKind.PreRestore,
            displayName
        );
        var backupPath = Path.Combine(instance.BackupsPath, manifest.BackupName);
        var fileCount = manifest.Entries.Sum(entry => entry.FileCount);
        var totalBytes = manifest.Entries.Sum(entry => entry.TotalBytes);

        SystemConsole.WriteLine();
        SystemConsole.WriteLine("Pre-restore backup completed successfully.");
        SystemConsole.WriteLine($"Name:   {manifest.DisplayName}");
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

        var displayName = BackupDisplayNamePolicy.GetDisplayName(backup.Manifest);
        var tags = backup.Manifest.Tags.Count == 0
            ? string.Empty
            : $" <{string.Join(", ", backup.Manifest.Tags)}>";

        return $"{displayName}{tags} | {createdLocal:yyyy-MM-dd HH:mm:ss} [{GetBackupKindDisplayName(backup.Manifest.Kind)}] - " +
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
