using InstanceBackupManager.Processing.Constants;
using InstanceBackupManager.Processing.Models.Instances;
using InstanceBackupManager.Processing.Utilities;

namespace InstanceBackupManager.Processing;

/// <summary>
/// Creates new instance directories and skeleton configurations beneath an application's instances directory.
/// </summary>
public sealed class InstanceCreationProcessor
{
    #region Constants

    /// <summary>
    /// Defines the maximum supported length for instance display names and directory names.
    /// </summary>
    public const int MaximumNameLength = 100;

    #endregion

    #region Fields

    private static readonly HashSet<string> ReservedWindowsNames = new(
        [
            "CON",
            "PRN",
            "AUX",
            "NUL",
            "COM1",
            "COM2",
            "COM3",
            "COM4",
            "COM5",
            "COM6",
            "COM7",
            "COM8",
            "COM9",
            "LPT1",
            "LPT2",
            "LPT3",
            "LPT4",
            "LPT5",
            "LPT6",
            "LPT7",
            "LPT8",
            "LPT9"
        ],
        StringComparer.OrdinalIgnoreCase
    );

    #endregion

    #region Properties

    /// <summary>
    /// Gets the configuration facade used to write skeleton configurations.
    /// </summary>
    private ConfigProcessor ConfigProcessor { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance-creation processor.
    /// </summary>
    /// <param name="configProcessor">The configuration facade used to write skeleton configurations.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configProcessor"/> is null.</exception>
    public InstanceCreationProcessor(ConfigProcessor configProcessor)
    {
        ArgumentNullException.ThrowIfNull(configProcessor);

        ConfigProcessor = configProcessor;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Creates a new instance directory and skeleton configuration beneath the supplied instances directory.
    /// </summary>
    /// <param name="instancesPath">The root directory containing individual instances.</param>
    /// <param name="instanceName">The user-facing name written to the configuration.</param>
    /// <param name="folderName">The directory name assigned to the new instance.</param>
    /// <returns>Information about the newly created instance and configuration.</returns>
    /// <exception cref="ArgumentException">Thrown when a supplied name is invalid.</exception>
    /// <exception cref="IOException">Thrown when the requested directory already exists or creation fails.</exception>
    public InstanceCreationResult CreateInstance(
        string instancesPath,
        string instanceName,
        string folderName
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instancesPath);

        var normalizedInstanceName = NormalizeInstanceName(instanceName);
        var normalizedFolderName = NormalizeFolderName(folderName);
        var fullInstancesPath = Path.GetFullPath(instancesPath);
        var fullInstancePath = Path.GetFullPath(
            normalizedFolderName,
            fullInstancesPath
        );

        FileSystemSafety.EnsurePathIsWithinDirectory(
            fullInstancePath,
            fullInstancesPath,
            "Instance directory"
        );

        Directory.CreateDirectory(fullInstancesPath);
        FileSystemSafety.EnsureExistingPathContainsNoReparsePoints(fullInstancesPath);

        if (Directory.Exists(fullInstancePath) || File.Exists(fullInstancePath))
        {
            throw new IOException(
                $"An instance directory or file named '{normalizedFolderName}' already exists."
            );
        }

        try
        {
            ConfigProcessor.CreateSkeletonConfig(
                fullInstancePath,
                normalizedInstanceName
            );

            return new InstanceCreationResult
            {
                Name = normalizedInstanceName,
                FolderName = normalizedFolderName,
                InstancePath = fullInstancePath,
                ConfigPath = Path.Combine(
                    fullInstancePath,
                    BackupStorageConstants.InstanceConfigurationFileName
                )
            };
        }
        catch
        {
            TryDeleteCreatedDirectory(fullInstancePath);
            throw;
        }
    }

    /// <summary>
    /// Creates a filesystem-safe suggested directory name from a user-facing instance name.
    /// </summary>
    /// <param name="instanceName">The user-facing instance name.</param>
    /// <returns>A directory name suitable for display as the workflow's default value.</returns>
    public string CreateSuggestedFolderName(string instanceName)
    {
        var normalizedInstanceName = NormalizeInstanceName(instanceName);
        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        var characters = normalizedInstanceName
            .Select(character => invalidCharacters.Contains(character) ? '-' : character)
            .ToArray();

        var suggestion = new string(characters).Trim().TrimEnd('.', ' ');

        if (string.IsNullOrWhiteSpace(suggestion))
        {
            suggestion = "New Instance";
        }

        if (IsReservedWindowsName(suggestion))
        {
            suggestion += " Instance";
        }

        return suggestion.Length <= MaximumNameLength
            ? suggestion
            : suggestion[..MaximumNameLength].TrimEnd('.', ' ');
    }

    #endregion

    #region Name Validation

    /// <summary>
    /// Normalizes and validates a user-facing instance name.
    /// </summary>
    private static string NormalizeInstanceName(string instanceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);

        var normalizedName = instanceName.Trim();

        if (normalizedName.Length > MaximumNameLength)
        {
            throw new ArgumentException(
                $"Instance names cannot exceed {MaximumNameLength} characters.",
                nameof(instanceName)
            );
        }

        if (normalizedName.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Instance names cannot contain line breaks or other control characters.",
                nameof(instanceName)
            );
        }

        return normalizedName;
    }

    /// <summary>
    /// Normalizes and validates a directory name used directly beneath the instances root.
    /// </summary>
    private static string NormalizeFolderName(string folderName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderName);

        var normalizedName = folderName.Trim();

        if (normalizedName.Length > MaximumNameLength)
        {
            throw new ArgumentException(
                $"Instance folder names cannot exceed {MaximumNameLength} characters.",
                nameof(folderName)
            );
        }

        if (normalizedName.EndsWith('.') || normalizedName.EndsWith(' '))
        {
            throw new ArgumentException(
                "Instance folder names cannot end with a period or space.",
                nameof(folderName)
            );
        }

        if (normalizedName.Any(character => Path.GetInvalidFileNameChars().Contains(character)))
        {
            throw new ArgumentException(
                "The instance folder name contains a character that Windows does not permit in directory names.",
                nameof(folderName)
            );
        }

        if (normalizedName.Contains(Path.DirectorySeparatorChar)
            || normalizedName.Contains(Path.AltDirectorySeparatorChar)
            || normalizedName is "." or "..")
        {
            throw new ArgumentException(
                "The instance folder name must be a single directory name.",
                nameof(folderName)
            );
        }

        if (IsReservedWindowsName(normalizedName))
        {
            throw new ArgumentException(
                $"'{normalizedName}' is reserved by Windows and cannot be used as an instance folder name.",
                nameof(folderName)
            );
        }

        return normalizedName;
    }

    /// <summary>
    /// Determines whether a directory name uses a Windows-reserved device name, including names with extensions.
    /// </summary>
    private static bool IsReservedWindowsName(string folderName)
    {
        var baseName = folderName.Split('.')[0];

        return ReservedWindowsNames.Contains(baseName);
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// Attempts to remove a newly created instance directory when skeleton creation fails.
    /// </summary>
    private static void TryDeleteCreatedDirectory(string instancePath)
    {
        try
        {
            if (Directory.Exists(instancePath))
            {
                Directory.Delete(
                    instancePath,
                    recursive: true
                );
            }
        }
        catch
        {
            // Cleanup is best-effort and must not replace the original creation failure.
        }
    }

    #endregion
}
