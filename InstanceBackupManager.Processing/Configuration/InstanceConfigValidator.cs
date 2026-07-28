using InstanceBackupManager.Processing.Constants;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Configuration;
using InstanceBackupManager.Processing.Utilities;

namespace InstanceBackupManager.Processing.Configuration;

/// <summary>
/// Validates instance configuration values and the safety of their configured filesystem paths.
/// </summary>
internal sealed class InstanceConfigValidator
{
    #region Internal Methods

    /// <summary>
    /// Validates an instance configuration and returns every discovered validation error.
    /// </summary>
    /// <param name="config">The configuration to validate.</param>
    /// <param name="instancePath">The directory containing the instance configuration and backups directory.</param>
    /// <returns>A read-only collection of validation errors.</returns>
    internal IReadOnlyCollection<string> Validate(
        InstanceConfig config,
        string instancePath
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(instancePath);

        var errors = new List<string>();
        var fullInstancePath = Path.GetFullPath(instancePath);

        var backupsPath = Path.Combine(
            fullInstancePath,
            BackupStorageConstants.BackupsDirectoryName
        );

        if (config.SchemaVersion != BackupStorageConstants.SupportedInstanceConfigurationSchemaVersion)
        {
            errors.Add(
                $"Unsupported schema version '{config.SchemaVersion}'. Expected version " +
                $"'{BackupStorageConstants.SupportedInstanceConfigurationSchemaVersion}'."
            );
        }

        if (string.IsNullOrWhiteSpace(config.Name))
        {
            errors.Add("The instance name is required.");
        }

        ValidateRetentionSettings(
            config.Retention,
            errors
        );

        if (config.Targets is null)
        {
            errors.Add("The instance targets collection is required.");
            return errors.AsReadOnly();
        }

        ValidateDuplicateTargetIds(
            config.Targets,
            errors
        );

        foreach (var target in config.Targets)
        {
            ValidateTarget(
                target,
                fullInstancePath,
                backupsPath,
                errors
            );
        }

        ValidateBackupPathConflicts(
            config.Targets,
            errors
        );

        return errors.AsReadOnly();
    }

    #endregion

    #region Retention Validation

    /// <summary>
    /// Validates optional per-kind retention limits.
    /// </summary>
    /// <param name="retention">The retention settings to validate.</param>
    /// <param name="errors">The collection receiving validation errors.</param>
    private static void ValidateRetentionSettings(
        RetentionSettings? retention,
        ICollection<string> errors
    )
    {
        if (retention is null)
        {
            return;
        }

        if (retention.ManualBackupsToKeep is <= 0)
        {
            errors.Add(
                "ManualBackupsToKeep must be at least one when a retention limit is configured."
            );
        }

        if (retention.PreRestoreBackupsToKeep is <= 0)
        {
            errors.Add(
                "PreRestoreBackupsToKeep must be at least one when a retention limit is configured."
            );
        }
    }

    #endregion

    #region Target Validation

    /// <summary>
    /// Reports duplicate target identifiers using case-insensitive comparison.
    /// </summary>
    /// <param name="targets">The configured targets to inspect.</param>
    /// <param name="errors">The collection receiving validation errors.</param>
    private static void ValidateDuplicateTargetIds(
        IEnumerable<TargetPath> targets,
        ICollection<string> errors
    )
    {
        var duplicateIds = targets
            .Where(target => !string.IsNullOrWhiteSpace(target.Id))
            .GroupBy(
                target => target.Id,
                StringComparer.OrdinalIgnoreCase
            )
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        foreach (var duplicateId in duplicateIds)
        {
            errors.Add(
                $"Target ID '{duplicateId}' is duplicated."
            );
        }
    }

    /// <summary>
    /// Validates one configured target.
    /// </summary>
    /// <param name="target">The target to validate.</param>
    /// <param name="instancePath">The absolute instance path.</param>
    /// <param name="backupsPath">The absolute backups path.</param>
    /// <param name="errors">The collection receiving validation errors.</param>
    private static void ValidateTarget(
        TargetPath target,
        string instancePath,
        string backupsPath,
        ICollection<string> errors
    )
    {
        if (string.IsNullOrWhiteSpace(target.Id))
        {
            errors.Add("A target has no ID.");
        }

        if (string.IsNullOrWhiteSpace(target.Name))
        {
            errors.Add(
                $"Target '{target.Id}' has no name."
            );
        }

        if (target.Type == TargetPathType.Unknown || !Enum.IsDefined(target.Type))
        {
            errors.Add(
                $"Target '{target.Id}' has an unsupported target type '{target.Type}'."
            );
        }

        ValidateSourcePath(
            target,
            instancePath,
            backupsPath,
            errors
        );

        ValidateBackupPath(
            target,
            errors
        );
    }

    #endregion

    #region Source-Path Validation

    /// <summary>
    /// Validates a target source path and rejects overlap with the instance backups directory.
    /// </summary>
    /// <param name="target">The target whose source will be validated.</param>
    /// <param name="instancePath">The absolute instance path.</param>
    /// <param name="backupsPath">The absolute backups path.</param>
    /// <param name="errors">The collection receiving validation errors.</param>
    private static void ValidateSourcePath(
        TargetPath target,
        string instancePath,
        string backupsPath,
        ICollection<string> errors
    )
    {
        if (string.IsNullOrWhiteSpace(target.Source))
        {
            errors.Add(
                $"Target '{target.Id}' has no source path."
            );

            return;
        }

        try
        {
            var resolvedSourcePath = PathResolver.ResolveSourcePath(
                target.Source,
                instancePath
            );

            if (FileSystemSafety.PathsOverlap(
                resolvedSourcePath,
                backupsPath
            ))
            {
                errors.Add(
                    $"Target '{target.Id}' has a source path that overlaps the instance backups directory."
                );
            }
        }
        catch (Exception exception)
            when (exception is ArgumentException
                  or NotSupportedException
                  or PathTooLongException)
        {
            errors.Add(
                $"Target '{target.Id}' has an invalid source path: {exception.Message}"
            );
        }
    }

    #endregion

    #region Backup-Path Validation

    /// <summary>
    /// Validates the relative destination used to store a target inside a backup.
    /// </summary>
    /// <param name="target">The target whose backup path will be validated.</param>
    /// <param name="errors">The collection receiving validation errors.</param>
    private static void ValidateBackupPath(
        TargetPath target,
        ICollection<string> errors
    )
    {
        if (string.IsNullOrWhiteSpace(target.BackupPath))
        {
            errors.Add(
                $"Target '{target.Id}' has no backup path."
            );

            return;
        }

        if (Path.IsPathRooted(target.BackupPath))
        {
            errors.Add(
                $"Target '{target.Id}' has a rooted backup path. Backup paths must be relative."
            );

            return;
        }

        try
        {
            var validationRoot = GetBackupPathValidationRoot();

            var resolvedBackupPath = Path.GetFullPath(
                target.BackupPath,
                validationRoot
            );

            if (!FileSystemSafety.IsSamePathOrChildOf(
                    resolvedBackupPath,
                    validationRoot
                )
                || FileSystemSafety.PathsEqual(
                    resolvedBackupPath,
                    validationRoot
                ))
            {
                errors.Add(
                    $"Target '{target.Id}' has an unsafe backup path."
                );
            }
        }
        catch (Exception exception)
            when (exception is ArgumentException
                  or NotSupportedException
                  or PathTooLongException)
        {
            errors.Add(
                $"Target '{target.Id}' has an invalid backup path: {exception.Message}"
            );
        }
    }

    /// <summary>
    /// Reports equal or nested backup destinations belonging to different targets.
    /// </summary>
    /// <param name="targets">The configured targets to inspect.</param>
    /// <param name="errors">The collection receiving validation errors.</param>
    private static void ValidateBackupPathConflicts(
        IEnumerable<TargetPath> targets,
        ICollection<string> errors
    )
    {
        var comparableTargets = targets
            .Where(target => !string.IsNullOrWhiteSpace(target.BackupPath))
            .Where(target => !Path.IsPathRooted(target.BackupPath))
            .ToList();

        var validationRoot = GetBackupPathValidationRoot();

        for (var firstIndex = 0; firstIndex < comparableTargets.Count; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < comparableTargets.Count; secondIndex++)
            {
                var firstTarget = comparableTargets[firstIndex];
                var secondTarget = comparableTargets[secondIndex];

                try
                {
                    var firstBackupPath = Path.GetFullPath(
                        firstTarget.BackupPath,
                        validationRoot
                    );

                    var secondBackupPath = Path.GetFullPath(
                        secondTarget.BackupPath,
                        validationRoot
                    );

                    if (FileSystemSafety.PathsOverlap(
                        firstBackupPath,
                        secondBackupPath
                    ))
                    {
                        errors.Add(
                            $"Targets '{firstTarget.Id}' and '{secondTarget.Id}' have overlapping backup paths."
                        );
                    }
                }
                catch (Exception exception)
                    when (exception is ArgumentException
                          or NotSupportedException
                          or PathTooLongException)
                {
                    /*
                     * ValidateBackupPath reports malformed paths individually. Conflict comparison cannot provide
                     * additional useful information for this pair.
                     */
                }
            }
        }
    }

    #endregion

    #region Path Resolution

    /// <summary>
    /// Gets an arbitrary absolute root used only for validating relative backup paths.
    /// </summary>
    /// <returns>The absolute validation-root path.</returns>
    private static string GetBackupPathValidationRoot()
    {
        return Path.GetFullPath(
            Path.Combine(
                Path.GetTempPath(),
                "InstanceBackupManagerValidation"
            )
        );
    }

    #endregion
}