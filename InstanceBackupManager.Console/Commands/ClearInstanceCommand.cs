using InstanceBackupManager.Console.Workflows;
using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Console.Commands;

/// <summary>
/// Executes the clear instance data workflow for a configured instance.
/// </summary>
internal sealed class ClearInstanceCommand(ClearWorkflow workflow) : IInstanceCommand
{
    #region Properties

    /// <inheritdoc/>
    public string Selection => "3";

    /// <inheritdoc/>
    public string Description => "Clear instance data";

    /// <summary>
    /// Gets the workflow executed by the command.
    /// </summary>
    private ClearWorkflow Workflow { get; } = workflow ?? throw new ArgumentNullException(nameof(workflow));

    #endregion

    #region Public Methods

    /// <inheritdoc/>
    public bool IsAvailable(InstanceContext instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        return instance.Config.Targets.Any(
            target => target.Enabled && target.AllowClear
        );
    }

    /// <inheritdoc/>
    public int Execute(InstanceContext instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        return Workflow.Run(instance);
    }

    #endregion
}
