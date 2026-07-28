using InstanceBackupManager.Console.Utilities;
using InstanceBackupManager.Processing;
using InstanceBackupManager.Processing.Models.Configuration;
using InstanceBackupManager.Processing.Models.Instances;
using SystemConsole = System.Console;

namespace InstanceBackupManager.Console.Workflows;

/// <summary>
/// Runs clear confirmation, target display, destructive processing, and result display.
/// </summary>
internal sealed class ClearWorkflow
{
    #region Properties

    /// <summary>
    /// Gets the processor used to clear configured instance targets.
    /// </summary>
    private ClearProcessor ClearProcessor { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new clear workflow.
    /// </summary>
    /// <param name="clearProcessor">The processor used to clear configured targets.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="clearProcessor"/> is null.</exception>
    internal ClearWorkflow(ClearProcessor clearProcessor)
    {
        ArgumentNullException.ThrowIfNull(clearProcessor);

        ClearProcessor = clearProcessor;
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Displays the targets eligible for clearing, requires confirmation, clears the targets, and displays the result.
    /// </summary>
    /// <param name="instance">The loaded instance whose eligible targets will be cleared.</param>
    /// <returns>Zero when the operation succeeds or is cancelled; otherwise, one.</returns>
    internal int Run(InstanceContext instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        try
        {
            var clearTargets = instance.Config.Targets
                .Where(target => target.Enabled && target.AllowClear)
                .ToList();

            if (clearTargets.Count == 0)
            {
                SystemConsole.WriteLine();
                SystemConsole.WriteLine("This instance does not contain any enabled targets that permit clearing.");

                ConsoleHelper.WaitForContinue();

                return 0;
            }

            if (!ConfirmClear(instance, clearTargets))
            {
                SystemConsole.WriteLine();
                SystemConsole.WriteLine("Clear cancelled.");

                ConsoleHelper.WaitForContinue();

                return 0;
            }

            SystemConsole.WriteLine();
            SystemConsole.WriteLine("Clearing instance data...");

            var result = ClearProcessor.ClearInstance(instance);
            var fileCount = result.Entries.Sum(entry => entry.FileCount);
            var totalBytes = result.Entries.Sum(entry => entry.TotalBytes);

            SystemConsole.WriteLine();
            SystemConsole.WriteLine("Clear completed successfully.");
            SystemConsole.WriteLine($"Targets: {result.Entries.Count}");
            SystemConsole.WriteLine($"Files:   {fileCount}");
            SystemConsole.WriteLine($"Bytes:   {totalBytes}");
            SystemConsole.WriteLine();

            foreach (var entry in result.Entries)
            {
                SystemConsole.WriteLine($"{entry.TargetName}: {entry.SourcePath}");
            }

            ConsoleHelper.WaitForContinue();

            return 0;
        }
        catch (Exception exception)
        {
            SystemConsole.WriteLine();
            SystemConsole.WriteLine("The clear operation could not be completed.");
            SystemConsole.WriteLine(exception.Message);

            ConsoleHelper.WaitForContinue();

            return 1;
        }
    }

    #endregion

    #region Clear Confirmation

    /// <summary>
    /// Displays the destructive-operation warning and requires the exact instance name as confirmation.
    /// </summary>
    /// <param name="instance">The loaded instance being cleared.</param>
    /// <param name="targets">The enabled targets that explicitly permit clearing.</param>
    /// <returns><see langword="true"/> when the user enters the exact instance name; otherwise, <see langword="false"/>.</returns>
    private static bool ConfirmClear(
        InstanceContext instance,
        IReadOnlyCollection<TargetPath> targets
    )
    {
        SystemConsole.WriteLine();
        SystemConsole.WriteLine("Clear Instance Data");
        SystemConsole.WriteLine("===================");
        SystemConsole.WriteLine();
        SystemConsole.WriteLine("The following targets will be cleared:");
        SystemConsole.WriteLine();

        foreach (var target in targets)
        {
            var resolvedSourcePath = ResolveSourcePath(
                target.Source,
                instance.InstancePath
            );

            SystemConsole.WriteLine($"- {target.Name} [{target.Type}]");
            SystemConsole.WriteLine($"  {resolvedSourcePath}");
        }

        SystemConsole.WriteLine();
        SystemConsole.WriteLine("WARNING: This operation cannot be undone.");
        SystemConsole.WriteLine("Configured files will be deleted.");
        SystemConsole.WriteLine("Configured directories will keep their root directory, but all contents will be deleted.");
        SystemConsole.WriteLine("No automatic backup will be created before clearing.");
        SystemConsole.WriteLine();
        SystemConsole.WriteLine($"To continue, type the exact instance name: {instance.Config.Name}");
        SystemConsole.Write("Confirmation: ");

        var confirmation = SystemConsole.ReadLine();

        return string.Equals(
            confirmation,
            instance.Config.Name,
            StringComparison.Ordinal
        );
    }

    #endregion

    #region Path Resolution

    /// <summary>
    /// Resolves a configured source path for display in the clear confirmation.
    /// </summary>
    /// <param name="source">The configured source path.</param>
    /// <param name="instancePath">The absolute instance directory used to resolve relative paths.</param>
    /// <returns>The expanded and normalized absolute source path.</returns>
    private static string ResolveSourcePath(
        string source,
        string instancePath
    )
    {
        var expandedSource = Environment.ExpandEnvironmentVariables(source);

        return Path.IsPathRooted(expandedSource)
            ? Path.GetFullPath(expandedSource)
            : Path.GetFullPath(expandedSource, instancePath);
    }

    #endregion
}