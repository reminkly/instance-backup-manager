using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Clear;
using InstanceBackupManager.Processing.Models.Configuration;
using InstanceBackupManager.Processing.Models.Instances;
using InstanceBackupManager.Processing.Strategies;
using InstanceBackupManager.Processing.Utilities;

namespace InstanceBackupManager.Processing;

/// <summary>
/// Coordinates validation and clearing of targets that explicitly permit destructive clearing.
/// </summary>
public sealed class ClearProcessor
{
    #region Properties

    /// <summary>
    /// Gets the strategies used to inspect and clear supported target types.
    /// </summary>
    private IReadOnlyCollection<IClearTargetStrategy> ClearStrategies { get; }

    /// <summary>
    /// Gets the time provider used to record clear completion times.
    /// </summary>
    private TimeProvider TimeProvider { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a clear processor using default strategies and the system time provider.
    /// </summary>
    public ClearProcessor()
        : this(TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a clear processor using default strategies and the specified time provider.
    /// </summary>
    /// <param name="timeProvider">The time provider used to record clear completion times.</param>
    public ClearProcessor(TimeProvider timeProvider)
        : this(
            timeProvider,
            CreateDefaultStrategies()
        )
    {
    }

    /// <summary>
    /// Initializes a clear processor using the specified time provider and strategies.
    /// </summary>
    /// <param name="timeProvider">The time provider used to record clear completion times.</param>
    /// <param name="clearStrategies">The strategies used to inspect and clear supported target types.</param>
    internal ClearProcessor(
        TimeProvider timeProvider,
        IReadOnlyCollection<IClearTargetStrategy> clearStrategies
    )
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(clearStrategies);

        TimeProvider = timeProvider;
        ClearStrategies = clearStrategies;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Clears every enabled target that explicitly permits clearing.
    /// </summary>
    /// <param name="instance">The instance containing the targets to clear.</param>
    /// <returns>A summary describing every cleared target.</returns>
    public ClearResult ClearInstance(InstanceContext instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (!instance.Config.Enabled)
        {
            throw new InvalidOperationException(
                $"Instance '{instance.Config.Name}' is disabled."
            );
        }

        /*
         * Resolve, validate, and inspect every target before deleting anything. This prevents an invalid later target
         * from leaving an earlier target partially cleared.
         */
        var clearPlan = CreateClearPlan(instance);

        if (clearPlan.Count == 0)
        {
            throw new InvalidOperationException(
                $"Instance '{instance.Config.Name}' does not contain any enabled targets that permit clearing."
            );
        }

        var resultEntries = clearPlan
            .Select(ClearTarget)
            .ToList()
            .AsReadOnly();

        return new ClearResult
        {
            CompletedUtc = TimeProvider.GetUtcNow(),
            Entries = resultEntries
        };
    }

    #endregion

    #region Clear Planning

    /// <summary>
    /// Creates and validates every target operation required to clear an instance.
    /// </summary>
    /// <param name="instance">The instance containing the configured targets.</param>
    /// <returns>A read-only collection of validated clear-plan entries.</returns>
    private IReadOnlyCollection<ClearPlanEntry> CreateClearPlan(InstanceContext instance)
    {
        var planEntries = new List<ClearPlanEntry>();

        foreach (var target in instance.Config.Targets)
        {
            if (!target.Enabled || !target.AllowClear)
            {
                continue;
            }

            var strategy = TargetPathStrategyResolver.Resolve(
                ClearStrategies,
                target.Type
            );

            var sourcePath = PathResolver.ResolveSourcePath(
                target.Source,
                instance.InstancePath
            );

            ValidateClearPath(
                target,
                sourcePath,
                instance
            );

            var statistics = strategy.Inspect(sourcePath);

            planEntries.Add(
                new ClearPlanEntry(
                    Target: target,
                    Strategy: strategy,
                    SourcePath: sourcePath,
                    FileCount: statistics.FileCount,
                    TotalBytes: statistics.TotalBytes
                )
            );
        }

        ValidateTargetsDoNotOverlap(planEntries);

        return planEntries.AsReadOnly();
    }

    /// <summary>
    /// Validates that a target path is safe for a destructive clear operation.
    /// </summary>
    /// <param name="target">The configured target being validated.</param>
    /// <param name="sourcePath">The resolved absolute source path.</param>
    /// <param name="instance">The current instance and its protected runtime paths.</param>
    private static void ValidateClearPath(
        TargetPath target,
        string sourcePath,
        InstanceContext instance
    )
    {
        if (IsRootPath(sourcePath))
        {
            throw new InvalidOperationException(
                $"Target '{target.Id}' resolves to filesystem root '{sourcePath}' and cannot be cleared."
            );
        }

        if (FileSystemSafety.PathsEqual(
            sourcePath,
            instance.InstancePath
        ))
        {
            throw new InvalidOperationException(
                $"Target '{target.Id}' resolves to the instance directory and cannot be cleared."
            );
        }

        if (FileSystemSafety.PathsOverlap(
            sourcePath,
            instance.BackupsPath
        ))
        {
            throw new InvalidOperationException(
                $"Target '{target.Id}' overlaps the instance backups directory and cannot be cleared."
            );
        }

        FileSystemSafety.EnsureExistingPathContainsNoReparsePoints(sourcePath);

        switch (target.Type)
        {
            case TargetPathType.File when Directory.Exists(sourcePath):
                throw new InvalidOperationException(
                    $"Target '{target.Id}' is configured as a file, but its source is an existing directory."
                );

            case TargetPathType.Directory when File.Exists(sourcePath):
                throw new InvalidOperationException(
                    $"Target '{target.Id}' is configured as a directory, but its source is an existing file."
                );
        }
    }

    /// <summary>
    /// Ensures that no planned clear targets overlap.
    /// </summary>
    /// <param name="planEntries">The validated clear targets.</param>
    private static void ValidateTargetsDoNotOverlap(IReadOnlyList<ClearPlanEntry> planEntries)
    {
        for (var firstIndex = 0; firstIndex < planEntries.Count; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < planEntries.Count; secondIndex++)
            {
                var firstEntry = planEntries[firstIndex];
                var secondEntry = planEntries[secondIndex];

                if (!FileSystemSafety.PathsOverlap(
                    firstEntry.SourcePath,
                    secondEntry.SourcePath
                ))
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"Clear targets '{firstEntry.Target.Id}' and '{secondEntry.Target.Id}' have overlapping source paths."
                );
            }
        }
    }

    #endregion

    #region Clear Execution

    /// <summary>
    /// Executes one validated clear-plan entry.
    /// </summary>
    /// <param name="planEntry">The validated target operation.</param>
    /// <returns>A result describing the cleared target.</returns>
    private static ClearResultEntry ClearTarget(ClearPlanEntry planEntry)
    {
        planEntry.Strategy.Clear(planEntry.SourcePath);

        return new ClearResultEntry
        {
            TargetId = planEntry.Target.Id,
            TargetName = planEntry.Target.Name,
            SourcePath = planEntry.SourcePath,
            Type = planEntry.Target.Type,
            FileCount = planEntry.FileCount,
            TotalBytes = planEntry.TotalBytes
        };
    }

    #endregion

    #region Path Interpretation

    /// <summary>
    /// Determines whether a path represents its filesystem root.
    /// </summary>
    /// <param name="path">The absolute path to inspect.</param>
    /// <returns><see langword="true"/> when the path is a filesystem root; otherwise, <see langword="false"/>.</returns>
    private static bool IsRootPath(string path)
    {
        var normalizedPath = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(path)
        );

        var rootPath = Path.GetPathRoot(normalizedPath);

        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return false;
        }

        var normalizedRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(rootPath)
        );

        return string.Equals(
            normalizedPath,
            normalizedRoot,
            FileSystemSafety.GetPathComparison()
        );
    }

    #endregion

    #region Strategy Creation

    /// <summary>
    /// Creates the default strategies used to clear supported target types.
    /// </summary>
    /// <returns>A read-only collection containing one strategy for each supported target type.</returns>
    private static IReadOnlyCollection<IClearTargetStrategy> CreateDefaultStrategies()
    {
        return
        [
            new FileTargetStrategy(),
            new DirectoryTargetStrategy()
        ];
    }

    #endregion

    #region Private Types

    /// <summary>
    /// Describes a validated target operation ready for clearing.
    /// </summary>
    /// <param name="Target">The configured target being cleared.</param>
    /// <param name="Strategy">The strategy used to clear the target.</param>
    /// <param name="SourcePath">The resolved absolute source path.</param>
    /// <param name="FileCount">The number of files that will be removed.</param>
    /// <param name="TotalBytes">The combined size, in bytes, of the files that will be removed.</param>
    private sealed record ClearPlanEntry(
        TargetPath Target,
        IClearTargetStrategy Strategy,
        string SourcePath,
        long FileCount,
        long TotalBytes
    );

    #endregion
}