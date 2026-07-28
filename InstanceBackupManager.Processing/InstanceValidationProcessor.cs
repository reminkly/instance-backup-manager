using InstanceBackupManager.Processing.Catalogs;
using InstanceBackupManager.Processing.Constants;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Configuration;
using InstanceBackupManager.Processing.Models.Instances;
using InstanceBackupManager.Processing.Models.Validation;
using InstanceBackupManager.Processing.Utilities;

namespace InstanceBackupManager.Processing;

/// <summary>
/// Performs a non-mutating validation of a loaded instance, its configured targets, and its stored backups.
/// </summary>
public sealed class InstanceValidationProcessor
{
    #region Properties

    /// <summary>
    /// Gets the configuration facade used to repeat configuration validation.
    /// </summary>
    private ConfigProcessor ConfigProcessor { get; }

    /// <summary>
    /// Gets the catalog used to validate and discover completed backups.
    /// </summary>
    private BackupCatalog BackupCatalog { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes an instance-validation processor using default dependencies.
    /// </summary>
    public InstanceValidationProcessor()
        : this(
            new ConfigProcessor(),
            new BackupCatalog()
        )
    {
    }

    /// <summary>
    /// Initializes an instance-validation processor using the specified dependencies.
    /// </summary>
    /// <param name="configProcessor">The configuration facade used to validate the loaded configuration.</param>
    /// <param name="backupCatalog">The catalog used to validate completed backups.</param>
    public InstanceValidationProcessor(
        ConfigProcessor configProcessor,
        BackupCatalog backupCatalog
    )
    {
        ArgumentNullException.ThrowIfNull(configProcessor);
        ArgumentNullException.ThrowIfNull(backupCatalog);

        ConfigProcessor = configProcessor;
        BackupCatalog = backupCatalog;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Validates an instance without modifying its configuration, sources, or backups.
    /// </summary>
    /// <param name="instance">The loaded instance to validate.</param>
    /// <returns>A structured report containing every validation finding.</returns>
    public InstanceValidationReport Validate(InstanceContext instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var findings = new List<ValidationFinding>();

        ValidateConfiguration(
            instance,
            findings
        );

        ValidateTargets(
            instance,
            findings
        );

        ValidateBackups(
            instance,
            findings
        );

        return new InstanceValidationReport
        {
            InstanceName = instance.Config.Name,
            Findings = findings.AsReadOnly()
        };
    }

    #endregion

    #region Configuration Validation

    /// <summary>
    /// Adds configuration findings for a loaded instance.
    /// </summary>
    private void ValidateConfiguration(
        InstanceContext instance,
        ICollection<ValidationFinding> findings
    )
    {
        var errors = ConfigProcessor.ValidateConfig(
            instance.Config,
            instance.InstancePath
        );

        if (errors.Count == 0)
        {
            AddFinding(
                findings,
                "Configuration",
                ValidationSeverity.Success,
                $"Schema version {instance.Config.SchemaVersion} and all configured paths are valid."
            );
        }
        else
        {
            foreach (var error in errors)
            {
                AddFinding(
                    findings,
                    "Configuration",
                    ValidationSeverity.Error,
                    error
                );
            }
        }

        AddFinding(
            findings,
            "Configuration",
            instance.Config.Enabled
                ? ValidationSeverity.Success
                : ValidationSeverity.Warning,
            instance.Config.Enabled
                ? "The instance is enabled."
                : "The instance is disabled; backup, restore, and clear operations are unavailable."
        );
    }

    #endregion

    #region Target Validation

    /// <summary>
    /// Adds existence and filesystem-safety findings for configured targets.
    /// </summary>
    private static void ValidateTargets(
        InstanceContext instance,
        ICollection<ValidationFinding> findings
    )
    {
        var enabledTargets = instance.Config.Targets
            .Where(target => target.Enabled)
            .ToList();

        AddFinding(
            findings,
            "Targets",
            enabledTargets.Count > 0
                ? ValidationSeverity.Success
                : ValidationSeverity.Warning,
            $"{enabledTargets.Count} enabled target{(enabledTargets.Count == 1 ? string.Empty : "s")} found."
        );

        foreach (var target in instance.Config.Targets)
        {
            ValidateTarget(
                instance,
                target,
                findings
            );
        }
    }

    /// <summary>
    /// Validates one configured target.
    /// </summary>
    private static void ValidateTarget(
        InstanceContext instance,
        TargetPath target,
        ICollection<ValidationFinding> findings
    )
    {
        if (!target.Enabled)
        {
            AddFinding(
                findings,
                "Targets",
                ValidationSeverity.Success,
                $"Target '{target.Name}' is disabled and will be skipped."
            );

            return;
        }

        try
        {
            var sourcePath = PathResolver.ResolveSourcePath(
                target.Source,
                instance.InstancePath
            );

            var sourceExists = target.Type switch
            {
                TargetPathType.File => File.Exists(sourcePath),
                TargetPathType.Directory => Directory.Exists(sourcePath),
                _ => false
            };

            if (!sourceExists)
            {
                AddFinding(
                    findings,
                    "Targets",
                    target.Required
                        ? ValidationSeverity.Error
                        : ValidationSeverity.Warning,
                    target.Required
                        ? $"Required target '{target.Name}' does not exist: {sourcePath}"
                        : $"Optional target '{target.Name}' does not exist and will be skipped: {sourcePath}"
                );

                return;
            }

            FileSystemSafety.EnsureExistingPathContainsNoReparsePoints(sourcePath);

            AddFinding(
                findings,
                "Targets",
                ValidationSeverity.Success,
                $"Target '{target.Name}' is available as a {target.Type.ToString().ToLowerInvariant()}: {sourcePath}"
            );
        }
        catch (Exception exception)
        {
            AddFinding(
                findings,
                "Targets",
                ValidationSeverity.Error,
                $"Target '{target.Name}' could not be validated: {exception.Message}"
            );
        }
    }

    #endregion

    #region Backup Validation

    /// <summary>
    /// Adds completed and in-progress backup findings.
    /// </summary>
    private void ValidateBackups(
        InstanceContext instance,
        ICollection<ValidationFinding> findings
    )
    {
        try
        {
            var backups = BackupCatalog.DiscoverBackups(instance);

            AddFinding(
                findings,
                "Backups",
                ValidationSeverity.Success,
                $"{backups.Count} completed backup{(backups.Count == 1 ? string.Empty : "s")} discovered with valid manifests."
            );

            var inProgressCount = Directory.Exists(instance.BackupsPath)
                ? Directory
                    .EnumerateDirectories(
                        instance.BackupsPath,
                        $"{BackupStorageConstants.InProgressDirectoryPrefix}*",
                        SearchOption.TopDirectoryOnly
                    )
                    .Count()
                : 0;

            if (inProgressCount > 0)
            {
                AddFinding(
                    findings,
                    "Backups",
                    ValidationSeverity.Warning,
                    $"{inProgressCount} incomplete backup director{(inProgressCount == 1 ? "y was" : "ies were")} found."
                );
            }
            else
            {
                AddFinding(
                    findings,
                    "Backups",
                    ValidationSeverity.Success,
                    "No incomplete backup directories were found."
                );
            }
        }
        catch (Exception exception)
        {
            AddFinding(
                findings,
                "Backups",
                ValidationSeverity.Error,
                $"Stored backups could not be validated: {exception.Message}"
            );
        }
    }

    #endregion

    #region Finding Creation

    /// <summary>
    /// Adds one finding to a validation report.
    /// </summary>
    private static void AddFinding(
        ICollection<ValidationFinding> findings,
        string category,
        ValidationSeverity severity,
        string message
    )
    {
        findings.Add(
            new ValidationFinding
            {
                Category = category,
                Severity = severity,
                Message = message
            }
        );
    }

    #endregion
}
