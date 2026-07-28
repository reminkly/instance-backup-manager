using InstanceBackupManager.Console.Workflows;
using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Console.Commands;

/// <summary>
/// Executes the back up now workflow for a configured instance.
/// </summary>
internal sealed class BackupInstanceCommand(BackupWorkflow workflow) : IInstanceCommand
{
    #region Properties

    /// <inheritdoc/>
    public string Selection => "1";

    /// <inheritdoc/>
    public string Description => "Back up now";

    /// <summary>
    /// Gets the workflow executed by the command.
    /// </summary>
    private BackupWorkflow Workflow { get; } = workflow ?? throw new ArgumentNullException(nameof(workflow));

    #endregion

    #region Public Methods

    /// <inheritdoc/>
    public bool IsAvailable(InstanceContext instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        return true;
    }

    /// <inheritdoc/>
    public int Execute(InstanceContext instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        return Workflow.Run(instance);
    }

    #endregion
}
