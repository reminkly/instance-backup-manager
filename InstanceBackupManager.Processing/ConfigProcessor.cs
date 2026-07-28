using System.Text.Json;
using System.Text.Json.Serialization;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Configuration;
using InstanceBackupManager.Processing.Models.Instances;
using InstanceBackupManager.Processing.Utilities;

namespace InstanceBackupManager.Processing;

/// <summary>
/// Discovers instance directories and handles the creation, loading, and validation of instance configuration files.
/// </summary>
public sealed class ConfigProcessor
{
    #region Constants

    private const string ConfigFileName = "instance.json";
    private const string BackupsDirectoryName = "backups";
    private const int SupportedSchemaVersion = 1;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the serializer options used when reading and writing instance configuration files.
    /// </summary>
    private JsonSerializerOptions JsonOptions { get; } = new()
    {
        // Allow JSON property casing to differ from the corresponding C# property casing.
        PropertyNameCaseInsensitive = true,

        // Permit comments and trailing commas to make manually edited configuration files more forgiving.
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,

        // Produce human-readable configuration files when creating skeleton configurations.
        WriteIndented = true,

        Converters =
        {
            // Serialize enum values as strings such as "file" and "directory" instead of their numeric values.
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    #endregion

    #region Public Methods

    /// <summary>
    /// Discovers and loads configured instances from an instances directory.
    /// </summary>
    /// <param name="instancesPath">The directory containing the individual instance directories.</param>
    /// <returns>A read-only collection containing each successfully loaded instance.</returns>
    /// <remarks>
    /// Directories without an <c>instance.json</c> file are skipped. The console application can handle those directories
    /// separately when implementing the configuration-creation workflow.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="instancesPath"/> is null, empty, or consists only of whitespace.
    /// </exception>
    /// <exception cref="InvalidDataException">Thrown when a discovered instance contains an invalid configuration.</exception>
    /// <exception cref="IOException">Thrown when a filesystem error prevents an instance from being loaded.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the application does not have permission to access one of the required directories or files.
    /// </exception>
    public IReadOnlyCollection<InstanceContext> LoadInstances(string instancesPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instancesPath);

        // Directory.CreateDirectory is idempotent, so no separate Directory.Exists check is necessary.
        Directory.CreateDirectory(instancesPath);

        var instances = new List<InstanceContext>();

        // Only immediate child directories represent instances.
        foreach (var instancePath in Directory.EnumerateDirectories(instancesPath))
        {
            var configPath = Path.Combine(instancePath, ConfigFileName);

            // An unconfigured instance will eventually be presented to the user by the console setup workflow.
            if (!File.Exists(configPath))
            {
                continue;
            }

            instances.Add(LoadInstance(instancePath));
        }

        return instances.AsReadOnly();
    }

    /// <summary>
    /// Loads and validates a single instance from its directory.
    /// </summary>
    /// <param name="instancePath">The instance directory containing the <c>instance.json</c> configuration file.</param>
    /// <returns>An instance context containing the loaded configuration and its resolved runtime paths.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="instancePath"/> is null, empty, or consists only of whitespace.
    /// </exception>
    /// <exception cref="FileNotFoundException">Thrown when the instance configuration file does not exist.</exception>
    /// <exception cref="InvalidDataException">Thrown when the configuration is empty or fails validation.</exception>
    /// <exception cref="JsonException">
    /// Thrown when the configuration contains invalid JSON or cannot be deserialized.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown when a filesystem error prevents the configuration from being read or the backups directory from being created.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the application does not have permission to access the instance.
    /// </exception>
    public InstanceContext LoadInstance(string instancePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instancePath);

        // Normalize the instance path so the runtime context consistently contains an absolute path.
        var fullInstancePath = Path.GetFullPath(instancePath);
        var configPath = Path.Combine(fullInstancePath, ConfigFileName);
        var backupsPath = Path.Combine(fullInstancePath, BackupsDirectoryName);

        var config = LoadConfig(configPath);
        var validationErrors = ValidateConfig(config, fullInstancePath);

        if (validationErrors.Count > 0)
        {
            throw new InvalidDataException(
                $"Invalid configuration '{configPath}':{Environment.NewLine}" +
                string.Join(Environment.NewLine, validationErrors)
            );
        }

        // CreateDirectory does not recreate or clear the directory when it already exists.
        Directory.CreateDirectory(backupsPath);

        return new InstanceContext
        {
            InstancePath = fullInstancePath,
            ConfigPath = configPath,
            BackupsPath = backupsPath,
            Config = config
        };
    }

    /// <summary>
    /// Reads and deserializes an instance configuration file.
    /// </summary>
    /// <param name="configPath">The path of the JSON configuration file.</param>
    /// <returns>The deserialized instance configuration.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="configPath"/> is null, empty, or consists only of whitespace.
    /// </exception>
    /// <exception cref="FileNotFoundException">Thrown when the specified configuration file does not exist.</exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when the configuration file does not contain a deserializable value.
    /// </exception>
    /// <exception cref="JsonException">
    /// Thrown when the configuration contains invalid JSON or cannot be deserialized into an
    /// <see cref="InstanceConfig"/>.
    /// </exception>
    /// <exception cref="IOException">Thrown when a filesystem error prevents the file from being read.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the application does not have permission to read the file.
    /// </exception>
    public InstanceConfig LoadConfig(string configPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException(
                "The instance configuration file was not found.",
                configPath
            );
        }

        var json = File.ReadAllText(configPath);

        return JsonSerializer.Deserialize<InstanceConfig>(
            json,
            JsonOptions
        ) ?? throw new InvalidDataException(
            $"Configuration '{configPath}' contained no data."
        );
    }

    /// <summary>
    /// Creates a skeleton configuration file for an instance.
    /// </summary>
    /// <param name="instancePath">The instance directory in which the configuration file will be created.</param>
    /// <remarks>
    /// The generated configuration uses the instance directory name as its initial display name and contains no targets.
    /// An existing configuration is never overwritten.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="instancePath"/> is null, empty, or consists only of whitespace.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown when the configuration already exists or a filesystem error prevents the directory or file from being created.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the application does not have permission to create the directory or configuration file.
    /// </exception>
    public void CreateSkeletonConfig(string instancePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instancePath);

        var fullInstancePath = Path.GetFullPath(instancePath);
        var configPath = Path.Combine(fullInstancePath, ConfigFileName);

        // This call also creates any missing parent directories and is safe when the instance directory already exists.
        Directory.CreateDirectory(fullInstancePath);

        // Do not overwrite an existing configuration because it may contain user-authored settings.
        if (File.Exists(configPath))
        {
            throw new IOException(
                $"Configuration already exists at '{configPath}'."
            );
        }

        var config = new InstanceConfig
        {
            Name = Path.GetFileName(fullInstancePath),
            Retention = new RetentionSettings
            {
                ManualBackupsToKeep = null,
                PreRestoreBackupsToKeep = null
            },
            Targets =
            [
                new TargetPath
                {
                    Id = "example-target",
                    Name = "Example Target - Update or Remove",
                    Enabled = false,
                    Required = true,
                    AllowClear = false,
                    Source = "replace-with-source-path",
                    Type = TargetPathType.File,
                    BackupPath = "files/replace-with-file-name"
                }
            ]
        };

        var json = JsonSerializer.Serialize(config, JsonOptions);

        File.WriteAllText(configPath, json);
    }

    /// <summary>
    /// Validates an instance configuration, including the safety of its source and backup paths.
    /// </summary>
    /// <param name="config">The instance configuration to validate.</param>
    /// <param name="instancePath">The directory containing the instance configuration and backups directory.</param>
    /// <returns>
    /// A read-only collection of validation errors. An empty collection indicates that the configuration passed validation.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="instancePath"/> is null, empty, or consists only of whitespace.
    /// </exception>
    public IReadOnlyCollection<string> ValidateConfig(
        InstanceConfig config,
        string instancePath
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(instancePath);

        var errors = new List<string>();
        var fullInstancePath = Path.GetFullPath(instancePath);
        var backupsPath = Path.Combine(fullInstancePath, BackupsDirectoryName);

        if (config.SchemaVersion != SupportedSchemaVersion)
        {
            errors.Add(
                $"Unsupported schema version '{config.SchemaVersion}'. Expected version '{SupportedSchemaVersion}'."
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

        ValidateBackupPathConflicts(config.Targets, errors);

        return errors.AsReadOnly();
    }

    #endregion

    #region Configuration Validation

    /// <summary>
    /// Validates the optional per-kind backup-retention limits.
    /// </summary>
    /// <param name="retention">The optional retention settings to validate.</param>
    /// <param name="errors">The collection to which validation errors will be added.</param>
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

    /// <summary>
    /// Checks the configuration for duplicate target identifiers.
    /// </summary>
    /// <param name="targets">The configured targets to inspect.</param>
    /// <param name="errors">The collection to which validation errors will be added.</param>
    private static void ValidateDuplicateTargetIds(
        IEnumerable<TargetPath> targets,
        ICollection<string> errors
    )
    {
        // IDs are case-insensitive because values such as "saves" and "Saves" should represent the same logical target.
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
            errors.Add($"Target ID '{duplicateId}' is duplicated.");
        }
    }

    /// <summary>
    /// Validates a single configured backup target.
    /// </summary>
    /// <param name="target">The target to validate.</param>
    /// <param name="instancePath">The absolute path of the containing instance directory.</param>
    /// <param name="backupsPath">The absolute path of the instance's backups directory.</param>
    /// <param name="errors">The collection to which validation errors will be added.</param>
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

        ValidateBackupPath(target, errors);
    }

    /// <summary>
    /// Validates the source path of a configured target.
    /// </summary>
    /// <param name="target">The target whose source path will be validated.</param>
    /// <param name="instancePath">The absolute path of the containing instance directory.</param>
    /// <param name="backupsPath">The absolute path of the instance's backups directory.</param>
    /// <param name="errors">The collection to which validation errors will be added.</param>
    private static void ValidateSourcePath(
        TargetPath target,
        string instancePath,
        string backupsPath,
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

            /*
             * Reject any overlap with the backups directory. This covers a source equal to the backups directory,
             * a source located inside the backups directory, and a source that contains the backups directory.
             *
             * The final case is especially important because backing up that source would recursively copy previous
             * backups into each newly created backup.
             */
            if (PathsOverlap(resolvedSourcePath, backupsPath))
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

    /// <summary>
    /// Validates the relative destination used to store a target inside a timestamped backup.
    /// </summary>
    /// <param name="target">The target whose backup path will be validated.</param>
    /// <param name="errors">The collection to which validation errors will be added.</param>
    private static void ValidateBackupPath(
        TargetPath target,
        ICollection<string> errors
    )
    {
        if (string.IsNullOrWhiteSpace(target.BackupPath))
        {
            errors.Add($"Target '{target.Id}' has no backup path.");
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
            /*
             * Resolve the backup path beneath an arbitrary validation root. If the result is not a child of that root,
             * the configured path escaped through parent traversal such as "..".
             *
             * This only performs path calculations. It does not create the validation directory.
             */
            var validationRoot = GetBackupPathValidationRoot();

            var resolvedBackupPath = Path.GetFullPath(
                target.BackupPath,
                validationRoot
            );

            if (!IsSamePathOrChildOf(resolvedBackupPath, validationRoot)
                || PathsEqual(resolvedBackupPath, validationRoot))
            {
                errors.Add($"Target '{target.Id}' has an unsafe backup path.");
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
    /// Checks whether any configured targets use equal or overlapping backup destinations.
    /// </summary>
    /// <param name="targets">The configured targets to inspect.</param>
    /// <param name="errors">The collection to which validation errors will be added.</param>
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
                    /*
                     * Resolving both destinations against the same arbitrary root makes comparisons reliable when the
                     * configured paths contain "." segments or a mixture of supported directory separators.
                     */
                    var firstBackupPath = Path.GetFullPath(
                        firstTarget.BackupPath,
                        validationRoot
                    );

                    var secondBackupPath = Path.GetFullPath(
                        secondTarget.BackupPath,
                        validationRoot
                    );

                    if (PathsOverlap(firstBackupPath, secondBackupPath))
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
                     * Invalid paths are reported by ValidateBackupPath. No useful conflict comparison can be performed
                     * for this pair, so validation continues with the remaining targets.
                     */
                }
            }
        }
    }

    /// <summary>
    /// Discovers every immediate instance directory without requiring it to contain a configuration file.
    /// </summary>
    /// <param name="instancesPath">The directory containing the individual instance directories.</param>
    /// <returns>A read-only collection describing each discovered instance directory.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="instancesPath"/> is null, empty, or consists only of whitespace.
    /// </exception>
    /// <exception cref="IOException">Thrown when a filesystem error prevents the directory from being inspected.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the application does not have permission to inspect the directory.
    /// </exception>
    public IReadOnlyCollection<InstanceDescriptor> DiscoverInstances(string instancesPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instancesPath);

        Directory.CreateDirectory(instancesPath);

        var instances = Directory
            .EnumerateDirectories(instancesPath)
            .Select(instancePath => new InstanceDescriptor
            {
                Name = Path.GetFileName(instancePath),
                InstancePath = Path.GetFullPath(instancePath),
                HasConfiguration = File.Exists(Path.Combine(instancePath, ConfigFileName))
            })
            .OrderBy(instance => instance.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return instances.AsReadOnly();
    }

    #endregion

    #region Path Resolution

    /// <summary>
    /// Gets an arbitrary absolute directory used exclusively for validating relative backup paths.
    /// </summary>
    /// <returns>The absolute validation-root path.</returns>
    /// <remarks>
    /// The returned directory is used only for path calculations and is not created on the filesystem.
    /// </remarks>
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

    #region Path Comparison

    /// <summary>
    /// Determines whether two filesystem paths are equal or whether either path contains the other.
    /// </summary>
    /// <param name="firstPath">The first absolute path.</param>
    /// <param name="secondPath">The second absolute path.</param>
    /// <returns><see langword="true"/> when the paths overlap; otherwise, <see langword="false"/>.</returns>
    private static bool PathsOverlap(
        string firstPath,
        string secondPath
    )
    {
        return IsSamePathOrChildOf(firstPath, secondPath)
               || IsSamePathOrChildOf(secondPath, firstPath);
    }

    /// <summary>
    /// Determines whether a path is equal to or contained beneath another path.
    /// </summary>
    /// <param name="candidatePath">The candidate absolute path.</param>
    /// <param name="parentPath">The possible parent absolute path.</param>
    /// <returns>
    /// <see langword="true"/> when the candidate is equal to or contained beneath the parent; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    private static bool IsSamePathOrChildOf(
        string candidatePath,
        string parentPath
    )
    {
        var normalizedCandidate = NormalizeDirectoryPath(candidatePath);
        var normalizedParent = NormalizeDirectoryPath(parentPath);

        return normalizedCandidate.StartsWith(
            normalizedParent,
            GetPathComparison()
        );
    }

    /// <summary>
    /// Determines whether two filesystem paths resolve to the same location.
    /// </summary>
    /// <param name="firstPath">The first absolute path.</param>
    /// <param name="secondPath">The second absolute path.</param>
    /// <returns><see langword="true"/> when the paths are equal; otherwise, <see langword="false"/>.</returns>
    private static bool PathsEqual(
        string firstPath,
        string secondPath
    )
    {
        var normalizedFirst = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(firstPath)
        );

        var normalizedSecond = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(secondPath)
        );

        return string.Equals(
            normalizedFirst,
            normalizedSecond,
            GetPathComparison()
        );
    }

    /// <summary>
    /// Normalizes a directory path and ensures that it ends with a directory separator.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized absolute directory path.</returns>
    private static string NormalizeDirectoryPath(string path)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(path)
        );

        return fullPath + Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// Gets the appropriate path-comparison behavior for the current operating system.
    /// </summary>
    /// <returns>A case-insensitive comparison on Windows and a case-sensitive comparison on other operating systems.</returns>
    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    #endregion
}