using InstanceBackupManager.Processing.Enums;

namespace InstanceBackupManager.Processing.Strategies;

/// <summary>
/// Defines the common type identification required by target-path operation strategies.
/// </summary>
internal interface ITargetPathStrategy
{
    #region Properties

    /// <summary>
    /// Gets the target-path type handled by the strategy.
    /// </summary>
    TargetPathType Type { get; }

    #endregion
}