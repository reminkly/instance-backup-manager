using InstanceBackupManager.Processing.Constants;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Configuration;
using InstanceBackupManager.Processing.Utilities;

namespace InstanceBackupManager.Processing.Configuration;

/// <summary>
/// Validates instance configuration values and the safety of configured source and backup-root paths.
/// </summary>
internal sealed class InstanceConfigValidator
{
    #region Internal Methods

    /// <summary>
    /// Validates an instance configuration and returns every discovered validation error.
    /// </summary>
    internal IReadOnlyCollection<string> Validate(
        InstanceConfig config,
        string instancePath
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(instancePath);

        var errors = new List<string>();
        var fullInstancePath = Path.GetFullPath(instancePath);
        var backupsPath = ValidateBackupRoot(
            config.BackupRoot,
            fullInstancePath,
            errors
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

        ValidateRetentionSettings(config.Retention, errors);

        if (config.Targets is null)
        {
            errors.Add("The instance targets collection is required.");
            return errors.AsReadOnly();
        }

        ValidateDuplicateTargetIds(config.Targets, errors);

        foreach (var target in config.Targets)
        {
            ValidateTarget(
                target,
                fullInstancePath,
                backupsPath,
                errors
            );
        }

        return errors.AsReadOnly();
    }

    #endregion

    #region Backup-Root Validation

    /// <summary>
    /// Validates and resolves the configured root containing timestamped backups.
    /// </summary>
    private static string? ValidateBackupRoot(
        string backupRoot,
        string instancePath,
        ICollection<string> errors
    )
    {
        if (string.IsNullOrWhiteSpace(backupRoot))
        {
            errors.Add("The backup root is required.");
            return null;
        }

        try
        {
            var resolvedPath = PathResolver.ResolveConfiguredPath(
                backupRoot,
                instancePath
            );

            if (!Path.IsPathRooted(Environment.ExpandEnvironmentVariables(backupRoot))
                && (!FileSystemSafety.IsSamePathOrChildOf(resolvedPath, instancePath)
                    || FileSystemSafety.PathsEqual(resolvedPath, instancePath)))
            {
                errors.Add("A relative backup root must remain inside the instance directory. Use an absolute path for external storage.");
            }

            if (FileSystemSafety.PathsEqual(resolvedPath, Path.GetPathRoot(resolvedPath)!))
            {
                errors.Add("The backup root cannot be a filesystem root.");
            }

            if (Directory.Exists(resolvedPath))
            {
                FileSystemSafety.EnsureExistingPathContainsNoReparsePoints(resolvedPath);
            }

            return resolvedPath;
        }
        catch (Exception exception)
            when (exception is ArgumentException
                  or IOException
                  or NotSupportedException
                  or PathTooLongException
                  or UnauthorizedAccessException)
        {
            errors.Add($"The backup root is invalid: {exception.Message}");
            return null;
        }
    }

    #endregion

    #region Retention Validation

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
            errors.Add("ManualBackupsToKeep must be at least one when a retention limit is configured.");
        }

        if (retention.PreRestoreBackupsToKeep is <= 0)
        {
            errors.Add("PreRestoreBackupsToKeep must be at least one when a retention limit is configured.");
        }
    }

    #endregion

    #region Target Validation

    private static void ValidateDuplicateTargetIds(
        IEnumerable<TargetPath> targets,
        ICollection<string> errors
    )
    {
        var duplicateIds = targets
            .Where(target => !string.IsNullOrWhiteSpace(target.Id))
            .GroupBy(target => target.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        foreach (var duplicateId in duplicateIds)
        {
            errors.Add($"Target ID '{duplicateId}' is duplicated.");
        }
    }

    private static void ValidateTarget(
        TargetPath target,
        string instancePath,
        string? backupsPath,
        ICollection<string> errors
    )
    {
        if (string.IsNullOrWhiteSpace(target.Id))
        {
            errors.Add("A target has no ID.");
        }
        else if (target.Id is "." or ".."
                 || Path.IsPathRooted(target.Id)
                 || target.Id.Any(character => Path.GetInvalidFileNameChars().Contains(character)))
        {
            errors.Add($"Target ID '{target.Id}' cannot be used as a backup payload directory name.");
        }

        if (string.IsNullOrWhiteSpace(target.Name))
        {
            errors.Add($"Target '{target.Id}' has no name.");
        }

        if (target.Type == TargetPathType.Unknown || !Enum.IsDefined(target.Type))
        {
            errors.Add($"Target '{target.Id}' has an unsupported target type '{target.Type}'.");
        }

        ValidateSourcePath(
            target,
            instancePath,
            backupsPath,
            errors
        );
    }

    private static void ValidateSourcePath(
        TargetPath target,
        string instancePath,
        string? backupsPath,
        ICollection<string> errors
    )
    {
        if (string.IsNullOrWhiteSpace(target.Source))
        {
            errors.Add($"Target '{target.Id}' has no source path.");
            return;
        }

        try
        {
            var resolvedSourcePath = PathResolver.ResolveSourcePath(
                target.Source,
                instancePath
            );

            if (backupsPath is not null
                && FileSystemSafety.PathsOverlap(resolvedSourcePath, backupsPath))
            {
                errors.Add($"Target '{target.Id}' has a source path that overlaps the configured backup root.");
            }
        }
        catch (Exception exception)
            when (exception is ArgumentException
                  or NotSupportedException
                  or PathTooLongException)
        {
            errors.Add($"Target '{target.Id}' has an invalid source path: {exception.Message}");
        }
    }

    #endregion
}
