using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Backups;
using InstanceBackupManager.Processing.Models.Configuration;
using InstanceBackupManager.Processing.Models.Instances;
using InstanceBackupManager.Processing.Utilities;

namespace InstanceBackupManager.Processing;

/// <summary>
/// Creates complete, timestamped backups for loaded instances.
/// </summary>
public sealed class BackupProcessor
{
    #region Constants

    private const string ManifestFileName = "manifest.json";
    private const int ManifestSchemaVersion = 1;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the time provider used to determine when backups are created.
    /// </summary>
    private TimeProvider TimeProvider { get; }

    /// <summary>
    /// Gets the serializer options used when writing backup manifests.
    /// </summary>
    private JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new backup processor using the system time provider.
    /// </summary>
    public BackupProcessor()
        : this(TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a new backup processor using the specified time provider.
    /// </summary>
    /// <param name="timeProvider">The time provider used when assigning backup timestamps.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="timeProvider"/> is null.</exception>
    public BackupProcessor(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        TimeProvider = timeProvider;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Creates a timestamped backup containing every enabled target in the specified instance.
    /// </summary>
    /// <param name="instance">The loaded instance to back up.</param>
    /// <param name="kind">The reason the backup is being created.</param>
    /// <returns>A manifest describing the completed backup.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="instance"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the instance is disabled, contains no enabled targets, or contains an unsupported target type.
    /// </exception>
    /// <exception cref="FileNotFoundException">Thrown when a configured file target does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when a configured directory target does not exist.</exception>
    /// <exception cref="IOException">
    /// Thrown when a filesystem error prevents the backup from being created or a symbolic link is encountered.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the application does not have permission to read a source or write the backup.
    /// </exception>
    public BackupManifest CreateBackup(
        InstanceContext instance, 
        BackupKind kind = BackupKind.Manual
    )
    {
        ArgumentNullException.ThrowIfNull(instance);

        ArgumentNullException.ThrowIfNull(instance);

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "The backup kind is not supported."
            );
        }

        if (!instance.Config.Enabled)
        {
            throw new InvalidOperationException($"Instance '{instance.Config.Name}' is disabled.");
        }

        var enabledTargets = instance.Config.Targets
            .Where(target => target.Enabled)
            .ToList();

        if (enabledTargets.Count == 0)
        {
            throw new InvalidOperationException(
                $"Instance '{instance.Config.Name}' does not contain any enabled targets."
            );
        }

        Directory.CreateDirectory(instance.BackupsPath);

        var createdUtc = TimeProvider.GetUtcNow();
        var backupName = CreateUniqueBackupName(instance.BackupsPath, createdUtc);
        var completedBackupPath = Path.Combine(instance.BackupsPath, backupName);
        var temporaryBackupPath = Path.Combine(instance.BackupsPath, $".in-progress-{Guid.NewGuid():N}");

        Directory.CreateDirectory(temporaryBackupPath);

        try
        {
            var entries = new List<BackupManifestEntry>();

            foreach (var target in enabledTargets)
            {
                var entry = BackupTarget(
                    target,
                    instance.InstancePath,
                    temporaryBackupPath
                );

                // Optional targets that do not currently exist are not included in the completed backup manifest.
                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }

            if (entries.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Instance '{instance.Config.Name}' does not contain any available targets to back up."
                );
            }

            var manifest = new BackupManifest
            {
                SchemaVersion = ManifestSchemaVersion,
                InstanceName = instance.Config.Name,
                BackupName = backupName,
                Kind = kind,
                CreatedUtc = createdUtc,
                Entries = entries.AsReadOnly()
            };

            WriteManifest(temporaryBackupPath, manifest);

            /*
             * The temporary and completed directories share the same parent directory. Moving the temporary directory
             * prevents an incomplete backup from appearing as a normal timestamped backup.
             */
            Directory.Move(temporaryBackupPath, completedBackupPath);

            return manifest;
        }
        catch
        {
            // Best-effort cleanup prevents failed operations from leaving unnecessary temporary data behind.
            TryDeleteDirectory(temporaryBackupPath);
            throw;
        }
    }

    #endregion

    #region Backup Operations

    /// <summary>
    /// Copies one configured target into a temporary backup directory.
    /// </summary>
    /// <param name="target">The enabled target to copy.</param>
    /// <param name="instancePath">The absolute instance directory used to resolve relative source paths.</param>
    /// <param name="temporaryBackupPath">The temporary directory holding the backup while it is being created.</param>
    /// <returns>
    /// A manifest entry describing the copied target, or <see langword="null"/> when an optional source does not exist.
    /// </returns>
    private static BackupManifestEntry? BackupTarget(
        TargetPath target,
        string instancePath,
        string temporaryBackupPath
    )
    {
        var sourcePath = PathResolver.ResolveSourcePath(target.Source, instancePath);

        if (!SourceExists(target.Type, sourcePath))
        {
            if (!target.Required)
            {
                return null;
            }

            ThrowMissingSourceException(
                target,
                sourcePath
            );
        }

        var destinationPath = Path.GetFullPath(target.BackupPath, temporaryBackupPath);

        EnsurePathIsWithinDirectory(
            destinationPath,
            temporaryBackupPath,
            target.Id
        );

        var copyResult = target.Type switch
        {
            TargetPathType.File => CopyFile(sourcePath, destinationPath),
            TargetPathType.Directory => CopyDirectory(sourcePath, destinationPath),
            _ => throw new InvalidOperationException(
                $"Target '{target.Id}' has an unsupported target type '{target.Type}'."
            )
        };

        return new BackupManifestEntry
        {
            TargetId = target.Id,
            TargetName = target.Name,
            Source = target.Source,
            Type = target.Type,
            BackupPath = target.BackupPath,
            FileCount = copyResult.FileCount,
            TotalBytes = copyResult.TotalBytes
        };
    }

    /// <summary>
    /// Copies a configured file into the backup.
    /// </summary>
    /// <param name="sourcePath">The absolute source-file path.</param>
    /// <param name="destinationPath">The absolute backup-file path.</param>
    /// <returns>The number of files and bytes copied.</returns>
    /// <exception cref="FileNotFoundException">Thrown when <paramref name="sourcePath"/> does not exist.</exception>
    /// <exception cref="IOException">Thrown when the source is a symbolic link or the copy operation fails.</exception>
    private static CopyResult CopyFile(
        string sourcePath,
        string destinationPath
    )
    {
        var sourceFile = new FileInfo(sourcePath);

        if (!sourceFile.Exists)
        {
            throw new FileNotFoundException(
                "The configured backup source file was not found.",
                sourcePath
            );
        }

        ThrowIfReparsePoint(sourceFile);

        var destinationDirectory = Path.GetDirectoryName(destinationPath);

        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        File.Copy(sourcePath, destinationPath, overwrite: false);

        return new CopyResult(
            FileCount: 1,
            TotalBytes: sourceFile.Length
        );
    }

    /// <summary>
    /// Recursively copies a configured directory into the backup while preserving empty directories.
    /// </summary>
    /// <param name="sourcePath">The absolute source-directory path.</param>
    /// <param name="destinationPath">The absolute backup-directory path.</param>
    /// <returns>The number of files and bytes copied.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when <paramref name="sourcePath"/> does not exist.</exception>
    /// <exception cref="IOException">Thrown when a symbolic link or junction is encountered or a copy operation fails.</exception>
    private static CopyResult CopyDirectory(
        string sourcePath,
        string destinationPath
    )
    {
        var sourceDirectory = new DirectoryInfo(sourcePath);

        if (!sourceDirectory.Exists)
        {
            throw new DirectoryNotFoundException(
                $"The configured backup source directory '{sourcePath}' was not found."
            );
        }

        ThrowIfReparsePoint(sourceDirectory);

        Directory.CreateDirectory(destinationPath);

        long fileCount = 0;
        long totalBytes = 0;

        CopyDirectoryContents(
            sourceDirectory,
            destinationPath,
            ref fileCount,
            ref totalBytes
        );

        return new CopyResult(fileCount, totalBytes);
    }

    /// <summary>
    /// Copies the contents of a source directory and accumulates file-count and byte-count information.
    /// </summary>
    /// <param name="sourceDirectory">The source directory currently being copied.</param>
    /// <param name="destinationPath">The corresponding destination directory.</param>
    /// <param name="fileCount">The accumulated number of copied files.</param>
    /// <param name="totalBytes">The accumulated size, in bytes, of copied files.</param>
    private static void CopyDirectoryContents(
        DirectoryInfo sourceDirectory,
        string destinationPath,
        ref long fileCount,
        ref long totalBytes
    )
    {
        foreach (var file in sourceDirectory.EnumerateFiles())
        {
            ThrowIfReparsePoint(file);

            var destinationFilePath = Path.Combine(destinationPath, file.Name);

            file.CopyTo(destinationFilePath, overwrite: false);

            fileCount++;
            totalBytes += file.Length;
        }

        foreach (var childDirectory in sourceDirectory.EnumerateDirectories())
        {
            ThrowIfReparsePoint(childDirectory);

            var childDestinationPath = Path.Combine(destinationPath, childDirectory.Name);

            // Creating the destination before recursion preserves directories that contain no files.
            Directory.CreateDirectory(childDestinationPath);

            CopyDirectoryContents(
                childDirectory,
                childDestinationPath,
                ref fileCount,
                ref totalBytes
            );
        }
    }

    /// <summary>
    /// Determines whether a configured source exists and matches its declared target type.
    /// </summary>
    /// <param name="type">The declared target type.</param>
    /// <param name="sourcePath">The resolved absolute source path.</param>
    /// <returns><see langword="true"/> when the expected source exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="type"/> is unsupported.</exception>
    private static bool SourceExists(
        TargetPathType type,
        string sourcePath
    )
    {
        return type switch
        {
            TargetPathType.File => File.Exists(sourcePath),
            TargetPathType.Directory => Directory.Exists(sourcePath),
            _ => throw new InvalidOperationException(
                $"Target type '{type}' is not supported for backup operations."
            )
        };
    }

    /// <summary>
    /// Throws the appropriate missing-source exception for a required target.
    /// </summary>
    /// <param name="target">The required target whose source does not exist.</param>
    /// <param name="sourcePath">The resolved absolute source path.</param>
    /// <exception cref="FileNotFoundException">Thrown when a required file target does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when a required directory target does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the target type is unsupported.</exception>
    private static void ThrowMissingSourceException(
        TargetPath target,
        string sourcePath
    )
    {
        switch (target.Type)
        {
            case TargetPathType.File:
                throw new FileNotFoundException(
                    $"Required source file for target '{target.Id}' was not found.",
                    sourcePath
                );

            case TargetPathType.Directory:
                throw new DirectoryNotFoundException(
                    $"Required source directory for target '{target.Id}' was not found: '{sourcePath}'."
                );

            default:
                throw new InvalidOperationException(
                    $"Target '{target.Id}' has an unsupported target type '{target.Type}'."
                );
        }
    }

    #endregion

    #region Manifest Operations

    /// <summary>
    /// Writes the completed backup manifest into the temporary backup directory.
    /// </summary>
    /// <param name="temporaryBackupPath">The temporary directory containing the copied targets.</param>
    /// <param name="manifest">The completed manifest to serialize.</param>
    private void WriteManifest(
        string temporaryBackupPath,
        BackupManifest manifest
    )
    {
        var manifestPath = Path.Combine(temporaryBackupPath, ManifestFileName);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);

        File.WriteAllText(manifestPath, json);
    }

    #endregion

    #region Backup Naming

    /// <summary>
    /// Creates a unique, sortable directory name for a backup.
    /// </summary>
    /// <param name="backupsPath">The directory containing completed backups.</param>
    /// <param name="createdUtc">The UTC date and time assigned to the backup.</param>
    /// <returns>A directory name that does not currently exist beneath <paramref name="backupsPath"/>.</returns>
    private static string CreateUniqueBackupName(
        string backupsPath,
        DateTimeOffset createdUtc
    )
    {
        var baseName = createdUtc.ToString(
            "yyyy-MM-dd_HH-mm-ss-fff'Z'",
            CultureInfo.InvariantCulture
        );

        var candidateName = baseName;
        var suffix = 1;

        while (Directory.Exists(Path.Combine(backupsPath, candidateName))
               || File.Exists(Path.Combine(backupsPath, candidateName)))
        {
            candidateName = $"{baseName}-{suffix:D2}";
            suffix++;
        }

        return candidateName;
    }

    #endregion

    #region Filesystem Safety

    /// <summary>
    /// Ensures that a resolved backup destination remains beneath the temporary backup directory.
    /// </summary>
    /// <param name="destinationPath">The resolved target destination.</param>
    /// <param name="temporaryBackupPath">The temporary backup directory that must contain the destination.</param>
    /// <param name="targetId">The identifier used when reporting an unsafe target.</param>
    /// <exception cref="InvalidDataException">
    /// Thrown when the destination is equal to or outside the temporary backup directory.
    /// </exception>
    private static void EnsurePathIsWithinDirectory(
        string destinationPath,
        string temporaryBackupPath,
        string targetId
    )
    {
        var normalizedDestination = Path.GetFullPath(destinationPath);
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(temporaryBackupPath));
        var rootWithSeparator = normalizedRoot + Path.DirectorySeparatorChar;

        if (!normalizedDestination.StartsWith(rootWithSeparator, GetPathComparison()))
        {
            throw new InvalidDataException(
                $"Target '{targetId}' has a backup path that escapes the temporary backup directory."
            );
        }
    }

    /// <summary>
    /// Throws an exception when a filesystem entry is a symbolic link, junction, or another reparse-point type.
    /// </summary>
    /// <param name="entry">The filesystem entry to inspect.</param>
    /// <exception cref="IOException">Thrown when <paramref name="entry"/> is a reparse point.</exception>
    private static void ThrowIfReparsePoint(FileSystemInfo entry)
    {
        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException(
                $"Symbolic links and junctions are not currently supported: '{entry.FullName}'."
            );
        }
    }

    /// <summary>
    /// Attempts to remove an incomplete temporary backup without masking the exception that caused the backup to fail.
    /// </summary>
    /// <param name="directoryPath">The temporary backup directory to remove.</param>
    private static void TryDeleteDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
        catch
        {
            /*
             * Cleanup is best-effort. A cleanup failure must not replace the original backup exception, which provides
             * more useful information about why the operation failed.
             */
        }
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

    #region Private Types

    /// <summary>
    /// Contains aggregate information about files copied for a target.
    /// </summary>
    /// <param name="FileCount">The number of copied files.</param>
    /// <param name="TotalBytes">The combined size, in bytes, of the copied files.</param>
    private readonly record struct CopyResult(
        long FileCount,
        long TotalBytes
    );

    #endregion
}