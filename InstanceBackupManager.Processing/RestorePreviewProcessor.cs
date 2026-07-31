using InstanceBackupManager.Processing.Catalogs;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Backups;
using InstanceBackupManager.Processing.Models.Instances;
using InstanceBackupManager.Processing.Models.Restore;
using InstanceBackupManager.Processing.Utilities;

namespace InstanceBackupManager.Processing;

/// <summary>
/// Builds a non-mutating comparison between a completed backup and the current configured destinations.
/// </summary>
public sealed class RestorePreviewProcessor(BackupCatalog backupCatalog)
{
    #region Properties

    private BackupCatalog BackupCatalog { get; } = backupCatalog ?? throw new ArgumentNullException(nameof(backupCatalog));

    #endregion

    #region Public Methods

    /// <summary>
    /// Compares every currently enabled manifest target with its current destination.
    /// </summary>
    public RestorePreview CreatePreview(
        InstanceContext instance,
        string backupName
    )
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupName);

        var backup = BackupCatalog.GetCompletedBackup(
            instance,
            backupName
        );

        var targets = backup.Manifest.Entries
            .Select(entry => CreateTargetPreview(instance, backup, entry))
            .Where(preview => preview is not null)
            .Cast<RestoreTargetPreview>()
            .ToList()
            .AsReadOnly();

        if (targets.Count == 0)
        {
            throw new InvalidOperationException(
                $"Backup '{backupName}' does not contain any targets that are currently enabled."
            );
        }

        return new RestorePreview
        {
            BackupName = backup.BackupName,
            Targets = targets
        };
    }

    #endregion

    #region Target Comparison

    private static RestoreTargetPreview? CreateTargetPreview(
        InstanceContext instance,
        BackupDescriptor backup,
        BackupManifestEntry manifestEntry
    )
    {
        var currentTarget = instance.Config.Targets.SingleOrDefault(
            target => string.Equals(target.Id, manifestEntry.TargetId, StringComparison.OrdinalIgnoreCase)
        ) ?? throw new InvalidDataException(
            $"Backup target '{manifestEntry.TargetId}' does not exist in the current instance configuration."
        );

        if (!currentTarget.Enabled)
        {
            return null;
        }

        if (currentTarget.Type != manifestEntry.Type)
        {
            throw new InvalidDataException(
                $"Backup target '{manifestEntry.TargetId}' has type '{manifestEntry.Type}', but the current configuration defines it as '{currentTarget.Type}'."
            );
        }

        var payloadPath = ResolvePayloadPath(
            backup.BackupPath,
            manifestEntry.BackupPath,
            manifestEntry.TargetId
        );

        var destinationPath = PathResolver.ResolveSourcePath(
            currentTarget.Source,
            instance.InstancePath
        );

        if (FileSystemSafety.PathsOverlap(destinationPath, instance.BackupsPath))
        {
            throw new InvalidDataException(
                $"Current destination for target '{currentTarget.Id}' overlaps the instance backups directory."
            );
        }

        var files = manifestEntry.Type switch
        {
            TargetPathType.File => CompareFile(payloadPath, destinationPath),
            TargetPathType.Directory => CompareDirectory(payloadPath, destinationPath),
            _ => throw new InvalidDataException(
                $"Backup target '{manifestEntry.TargetId}' has unsupported type '{manifestEntry.Type}'."
            )
        };

        return new RestoreTargetPreview
        {
            TargetId = currentTarget.Id,
            TargetName = currentTarget.Name,
            Type = currentTarget.Type,
            DestinationPath = destinationPath,
            Files = files
        };
    }

    #endregion

    #region File Comparison

    private static IReadOnlyCollection<RestorePreviewFile> CompareFile(
        string payloadPath,
        string destinationPath
    )
    {
        if (!File.Exists(payloadPath))
        {
            throw new FileNotFoundException("The backup payload file was not found.", payloadPath);
        }

        FileSystemSafety.ThrowIfReparsePoint(new FileInfo(payloadPath));

        if (File.Exists(destinationPath))
        {
            FileSystemSafety.ThrowIfReparsePoint(new FileInfo(destinationPath));
        }

        return new[]
        {
            CompareBackupFile(
                payloadPath,
                destinationPath,
                Path.GetFileName(destinationPath)
            )
        };
    }

    private static IReadOnlyCollection<RestorePreviewFile> CompareDirectory(
        string payloadPath,
        string destinationPath
    )
    {
        if (!Directory.Exists(payloadPath))
        {
            throw new DirectoryNotFoundException(
                $"The backup payload directory was not found: '{payloadPath}'."
            );
        }

        var files = EnumerateSafeFiles(payloadPath)
            .Select(
                backupFile =>
                {
                    return CompareBackupFile(
                        backupFile.FullPath,
                        Path.Combine(destinationPath, backupFile.RelativePath),
                        backupFile.RelativePath
                    );
                }
            )
            .ToList();

        if (Directory.Exists(destinationPath))
        {
            var backupRelativePaths = files
                .Select(file => file.RelativePath)
                .ToHashSet(FileSystemSafety.GetPathComparer());

            files.AddRange(
                EnumerateSafeFiles(destinationPath)
                    .Select(file => file.RelativePath)
                    .Where(relativePath => !backupRelativePaths.Contains(relativePath))
                    .Select(
                        relativePath => new RestorePreviewFile
                        {
                            RelativePath = relativePath,
                            ChangeKind = RestoreFileChangeKind.Preserve,
                            BackupBytes = 0,
                            CurrentBytes = new FileInfo(Path.Combine(destinationPath, relativePath)).Length
                        }
                    )
            );
        }

        return files
            .OrderBy(file => file.RelativePath, FileSystemSafety.GetPathComparer())
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Recursively enumerates normal files while rejecting symbolic links, junctions, and other reparse points.
    /// </summary>
    private static IReadOnlyCollection<EnumeratedFile> EnumerateSafeFiles(string rootPath)
    {
        var files = new List<EnumeratedFile>();
        EnumerateSafeFiles(
            new DirectoryInfo(rootPath),
            rootPath,
            files
        );

        return files.AsReadOnly();
    }

    private static void EnumerateSafeFiles(
        DirectoryInfo directory,
        string rootPath,
        ICollection<EnumeratedFile> files
    )
    {
        FileSystemSafety.ThrowIfReparsePoint(directory);

        foreach (var file in directory.EnumerateFiles())
        {
            FileSystemSafety.ThrowIfReparsePoint(file);
            files.Add(
                new EnumeratedFile(
                    file.FullName,
                    Path.GetRelativePath(rootPath, file.FullName)
                )
            );
        }

        foreach (var childDirectory in directory.EnumerateDirectories())
        {
            EnumerateSafeFiles(
                childDirectory,
                rootPath,
                files
            );
        }
    }

    private static RestorePreviewFile CompareBackupFile(
        string backupFile,
        string destinationFile,
        string relativePath
    )
    {
        var backupLength = new FileInfo(backupFile).Length;

        if (!File.Exists(destinationFile))
        {
            return new RestorePreviewFile
            {
                RelativePath = relativePath,
                ChangeKind = RestoreFileChangeKind.Create,
                BackupBytes = backupLength,
                CurrentBytes = null
            };
        }

        var destinationLength = new FileInfo(destinationFile).Length;
        var filesMatch = backupLength == destinationLength && FilesAreEqual(backupFile, destinationFile);

        return new RestorePreviewFile
        {
            RelativePath = relativePath,
            ChangeKind = filesMatch
                ? RestoreFileChangeKind.Unchanged
                : RestoreFileChangeKind.Overwrite,
            BackupBytes = backupLength,
            CurrentBytes = destinationLength
        };
    }

    private static bool FilesAreEqual(
        string firstPath,
        string secondPath
    )
    {
        const int bufferSize = 81920;
        var firstBuffer = new byte[bufferSize];
        var secondBuffer = new byte[bufferSize];

        using var firstStream = File.OpenRead(firstPath);
        using var secondStream = File.OpenRead(secondPath);

        while (true)
        {
            var firstCount = firstStream.Read(firstBuffer);
            var secondCount = secondStream.Read(secondBuffer);

            if (firstCount != secondCount)
            {
                return false;
            }

            if (firstCount == 0)
            {
                return true;
            }

            if (!firstBuffer.AsSpan(0, firstCount).SequenceEqual(secondBuffer.AsSpan(0, secondCount)))
            {
                return false;
            }
        }
    }

    #endregion

    #region Path Resolution

    private static string ResolvePayloadPath(
        string backupPath,
        string relativePayloadPath,
        string targetId
    )
    {
        if (Path.IsPathRooted(relativePayloadPath))
        {
            throw new InvalidDataException($"Backup target '{targetId}' contains a rooted payload path.");
        }

        var payloadPath = Path.GetFullPath(relativePayloadPath, backupPath);

        FileSystemSafety.EnsurePathIsWithinDirectory(
            payloadPath,
            backupPath,
            $"Backup target '{targetId}'"
        );

        return payloadPath;
    }

    #endregion

    #region Private Types

    private sealed record EnumeratedFile(
        string FullPath,
        string RelativePath
    );

    #endregion
}
