using InstanceBackupManager.Console.Commands;
using InstanceBackupManager.Processing.Models.Instances;

namespace InstanceBackupManager.Console.Logging;

/// <summary>
/// Adds application logging around another instance command without changing the wrapped command.
/// </summary>
internal sealed class LoggingInstanceCommandDecorator(
    IInstanceCommand command,
    IApplicationLogger logger
) : IInstanceCommand
{
    #region Properties

    /// <summary>
    /// Gets the wrapped command.
    /// </summary>
    private IInstanceCommand Command { get; } = command ?? throw new ArgumentNullException(nameof(command));

    /// <summary>
    /// Gets the application logger.
    /// </summary>
    private IApplicationLogger Logger { get; } = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public string Selection => Command.Selection;

    /// <inheritdoc/>
    public string Description => Command.Description;

    #endregion

    #region Public Methods

    /// <inheritdoc/>
    public bool IsAvailable(InstanceContext instance)
    {
        return Command.IsAvailable(instance);
    }

    /// <inheritdoc/>
    public int Execute(InstanceContext instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        Logger.LogInformation(
            $"Command '{Description}' started for instance '{instance.Config.Name}'."
        );

        try
        {
            var result = Command.Execute(instance);

            if (result == 0)
            {
                Logger.LogInformation(
                    $"Command '{Description}' completed for instance '{instance.Config.Name}'."
                );
            }
            else
            {
                Logger.LogWarning(
                    $"Command '{Description}' returned result '{result}' for instance '{instance.Config.Name}'."
                );
            }

            return result;
        }
        catch (Exception exception)
        {
            Logger.LogError(
                $"Command '{Description}' failed for instance '{instance.Config.Name}'.",
                exception
            );

            throw;
        }
    }

    #endregion
}
