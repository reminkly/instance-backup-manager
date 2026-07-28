using InstanceBackupManager.Processing.Enums;

namespace InstanceBackupManager.Processing.Strategies;

/// <summary>
/// Resolves target-path operation strategies by their configured target type.
/// </summary>
internal static class TargetPathStrategyResolver
{
    #region Internal Methods

    /// <summary>
    /// Resolves the strategy matching a target-path type.
    /// </summary>
    /// <typeparam name="TStrategy">The operation-specific strategy contract.</typeparam>
    /// <param name="strategies">The available strategies.</param>
    /// <param name="type">The configured target-path type.</param>
    /// <returns>The single strategy registered for the supplied type.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no strategy or more than one strategy is registered for <paramref name="type"/>.
    /// </exception>
    internal static TStrategy Resolve<TStrategy>(
        IReadOnlyCollection<TStrategy> strategies,
        TargetPathType type
    )
        where TStrategy : ITargetPathStrategy
    {
        ArgumentNullException.ThrowIfNull(strategies);

        var matchingStrategies = strategies
            .Where(strategy => strategy.Type == type)
            .ToList();

        return matchingStrategies.Count switch
        {
            1 => matchingStrategies[0],
            0 => throw new InvalidOperationException(
                $"No {typeof(TStrategy).Name} is registered for target type '{type}'."
            ),
            _ => throw new InvalidOperationException(
                $"Multiple {typeof(TStrategy).Name} implementations are registered for target type '{type}'."
            )
        };
    }

    #endregion
}