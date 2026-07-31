using InstanceBackupManager.Console.Utilities;
using InstanceBackupManager.Processing;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Instances;
using InstanceBackupManager.Processing.Policies;
using SystemConsole = System.Console;

namespace InstanceBackupManager.Console.Workflows;

/// <summary>
/// Runs the interactive backup workflow, applies manual-backup retention, and displays the result.
/// </summary>
internal sealed class BackupWorkflow
{
    #region Properties

    /// <summary>
    /// Gets the processor used to create backups.
    /// </summary>
    private BackupProcessor BackupProcessor { get; }

    /// <summary>
    /// Gets the processor used to apply configured manual-backup retention.
    /// </summary>
    private BackupRetentionProcessor BackupRetentionProcessor { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new backup workflow.
    /// </summary>
    /// <param name="backupProcessor">The processor used to create backups.</param>
    /// <param name="backupRetentionProcessor">The processor used to apply configured retention.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="backupProcessor"/> or <paramref name="backupRetentionProcessor"/> is null.
    /// </exception>
    internal BackupWorkflow(
        BackupProcessor backupProcessor,
        BackupRetentionProcessor backupRetentionProcessor
    )
    {
        ArgumentNullException.ThrowIfNull(backupProcessor);
        ArgumentNullException.ThrowIfNull(backupRetentionProcessor);

        BackupProcessor = backupProcessor;
        BackupRetentionProcessor = backupRetentionProcessor;
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Creates a manual backup, applies manual-backup retention, and displays a summary of the completed operation.
    /// </summary>
    /// <param name="instance">The loaded instance to back up.</param>
    /// <returns>Zero when the backup succeeds; otherwise, one.</returns>
    internal int Run(InstanceContext instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        try
        {
            SystemConsole.WriteLine();
            SystemConsole.Write("Backup name (optional): ");

            var requestedDisplayName = SystemConsole.ReadLine();

            SystemConsole.Write("Backup notes (optional): ");
            var requestedNotes = SystemConsole.ReadLine();

            SystemConsole.Write("Tags (optional, comma-separated): ");
            var requestedTags = ParseTags(SystemConsole.ReadLine());

            SystemConsole.WriteLine();
            SystemConsole.WriteLine("Creating backup...");

            var manifest = BackupProcessor.CreateBackup(
                instance,
                BackupKind.Manual,
                requestedDisplayName,
                requestedNotes,
                requestedTags
            );

            var backupPath = Path.Combine(instance.BackupsPath, manifest.BackupName);
            var fileCount = manifest.Entries.Sum(entry => entry.FileCount);
            var totalBytes = manifest.Entries.Sum(entry => entry.TotalBytes);

            SystemConsole.WriteLine();
            SystemConsole.WriteLine("Backup completed successfully.");
            SystemConsole.WriteLine($"Name:   {manifest.DisplayName}");
            SystemConsole.WriteLine($"Backup: {manifest.BackupName}");
            SystemConsole.WriteLine($"Files:  {fileCount}");
            SystemConsole.WriteLine($"Bytes:  {totalBytes}");
            SystemConsole.WriteLine($"Path:   {backupPath}");

            if (!string.IsNullOrWhiteSpace(manifest.Notes))
            {
                SystemConsole.WriteLine($"Notes:  {manifest.Notes}");
            }

            if (manifest.Tags.Count > 0)
            {
                SystemConsole.WriteLine($"Tags:   {string.Join(", ", manifest.Tags)}");
            }

            ApplyRetention(
                instance,
                BackupKind.Manual
            );

            ConsoleHelper.WaitForContinue();

            return 0;
        }
        catch (Exception exception)
        {
            SystemConsole.WriteLine();
            SystemConsole.WriteLine("The backup could not be completed.");
            SystemConsole.WriteLine(exception.Message);

            ConsoleHelper.WaitForContinue();

            return 1;
        }
    }

    #endregion

    #region Metadata

    /// <summary>
    /// Converts comma-separated console input into values that will be normalized by the metadata policy.
    /// </summary>
    private static IReadOnlyCollection<string> ParseTags(string? input)
    {
        return string.IsNullOrWhiteSpace(input)
            ? Array.Empty<string>()
            : input.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    #endregion

    #region Retention

    /// <summary>
    /// Applies configured retention for the supplied backup kind without misrepresenting a completed backup as failed.
    /// </summary>
    /// <param name="instance">The loaded instance whose retention settings will be applied.</param>
    /// <param name="kind">The backup kind whose retention limit will be applied.</param>
    private void ApplyRetention(
        InstanceContext instance,
        BackupKind kind
    )
    {
        try
        {
            var result = BackupRetentionProcessor.ApplyRetention(
                instance,
                kind
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
                $"Retention removed {result.Entries.Count} older {backupLabel}, {fileCount} files, and {totalBytes} bytes."
            );
        }
        catch (Exception exception)
        {
            SystemConsole.WriteLine();
            SystemConsole.WriteLine("The backup completed, but retention could not be applied.");
            SystemConsole.WriteLine(exception.Message);
        }
    }

    #endregion
}
