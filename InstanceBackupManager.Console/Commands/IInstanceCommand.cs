using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Console.Commands;

/// <summary>
/// Defines an operation that can be displayed and executed from the configured-instance menu.
/// </summary>
internal interface IInstanceCommand
{
    #region Properties

    /// <summary>
    /// Gets the menu selection assigned to the command.
    /// </summary>
    string Selection { get; }

    /// <summary>
    /// Gets the user-facing description displayed by the menu.
    /// </summary>
    string Description { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Determines whether the command is available for the supplied instance.
    /// </summary>
    /// <param name="instance">The loaded instance being displayed.</param>
    /// <returns><see langword="true"/> when the command can be displayed and executed; otherwise, <see langword="false"/>.</returns>
    bool IsAvailable(InstanceContext instance);

    /// <summary>
    /// Executes the command for the supplied instance.
    /// </summary>
    /// <param name="instance">The loaded instance receiving the operation.</param>
    /// <returns>Zero when the operation succeeds or is cancelled; otherwise, a nonzero failure result.</returns>
    int Execute(InstanceContext instance);

    #endregion
}
