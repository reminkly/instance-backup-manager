using InstanceBackupManager.Console.Workflows;
using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Console.Commands;

/// <summary>
/// Executes the restore from backup workflow for a configured instance.
/// </summary>
internal sealed class RestoreInstanceCommand(RestoreWorkflow workflow) : IInstanceCommand
{
    #region Properties

    /// <inheritdoc/>
    public string Selection => "2";

    /// <inheritdoc/>
    public string Description => "Restore from backup";

    /// <summary>
    /// Gets the workflow executed by the command.
    /// </summary>
    private RestoreWorkflow Workflow { get; } = workflow ?? throw new ArgumentNullException(nameof(workflow));

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
