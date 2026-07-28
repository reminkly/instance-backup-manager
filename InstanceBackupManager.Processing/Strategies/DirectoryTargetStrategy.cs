using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Utilities;

namespace InstanceBackupManager.Processing.Strategies;

/// <summary>
/// Provides backup, restore, inspection, and clear algorithms for directory targets.
/// </summary>
internal sealed class DirectoryTargetStrategy :
    IBackupTargetStrategy,
    IRestoreTargetStrategy,
    IClearTargetStrategy
{
    #region Properties

    /// <inheritdoc/>
    public TargetPathType Type => TargetPathType.Directory;

    #endregion

    #region Backup Operations

    /// <inheritdoc/>
    public bool SourceExists(string sourcePath)
    {
        return Directory.Exists(sourcePath);
    }

    /// <inheritdoc/>
    public FileOperationStatistics Backup(
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

        FileSystemSafety.ThrowIfReparsePoint(sourceDirectory);

        Directory.CreateDirectory(destinationPath);

        long fileCount = 0;
        long totalBytes = 0;

        BackupDirectoryContents(
            sourceDirectory,
            destinationPath,
            ref fileCount,
            ref totalBytes
        );

        return new FileOperationStatistics(
            FileCount: fileCount,
            TotalBytes: totalBytes
        );
    }

    /// <summary>
    /// Recursively copies directory contents into a backup while preserving empty directories.
    /// </summary>
    /// <param name="sourceDirectory">The source directory currently being copied.</param>
    /// <param name="destinationPath">The corresponding backup destination.</param>
    /// <param name="fileCount">The accumulated number of copied files.</param>
    /// <param name="totalBytes">The accumulated size, in bytes, of copied files.</param>
    private static void BackupDirectoryContents(
        DirectoryInfo sourceDirectory,
        string destinationPath,
        ref long fileCount,
        ref long totalBytes
    )
    {
        foreach (var sourceFile in sourceDirectory.EnumerateFiles())
        {
            FileSystemSafety.ThrowIfReparsePoint(sourceFile);

            var destinationFilePath = Path.Combine(
                destinationPath,
                sourceFile.Name
            );

            sourceFile.CopyTo(
                destinationFilePath,
                overwrite: false
            );

            fileCount++;
            totalBytes += sourceFile.Length;
        }

        foreach (var childDirectory in sourceDirectory.EnumerateDirectories())
        {
            FileSystemSafety.ThrowIfReparsePoint(childDirectory);

            var childDestinationPath = Path.Combine(
                destinationPath,
                childDirectory.Name
            );

            Directory.CreateDirectory(childDestinationPath);

            BackupDirectoryContents(
                childDirectory,
                childDestinationPath,
                ref fileCount,
                ref totalBytes
            );
        }
    }

    #endregion

    #region Restore Operations

    /// <inheritdoc/>
    public void ValidatePayload(string payloadPath)
    {
        var payloadDirectory = new DirectoryInfo(payloadPath);

        if (!payloadDirectory.Exists)
        {
            throw new DirectoryNotFoundException(
                $"The backup payload directory '{payloadPath}' was not found."
            );
        }

        InspectDirectoryContents(payloadDirectory);
    }

    /// <inheritdoc/>
    public FileOperationStatistics Restore(
        string payloadPath,
        string destinationPath
    )
    {
        /*
         * Validation is repeated here so the strategy remains safe when called directly. RestoreProcessor also calls it
         * while constructing the complete restore plan, before any destination is modified.
         */
        ValidatePayload(payloadPath);

        var payloadDirectory = new DirectoryInfo(payloadPath);

        FileSystemSafety.EnsureExistingPathContainsNoReparsePoints(destinationPath);
        Directory.CreateDirectory(destinationPath);

        long fileCount = 0;
        long totalBytes = 0;

        RestoreDirectoryContents(
            payloadDirectory,
            destinationPath,
            ref fileCount,
            ref totalBytes
        );

        return new FileOperationStatistics(
            FileCount: fileCount,
            TotalBytes: totalBytes
        );
    }

    /// <summary>
    /// Recursively restores directory contents while preserving unrelated destination files.
    /// </summary>
    /// <param name="payloadDirectory">The payload directory currently being restored.</param>
    /// <param name="destinationPath">The corresponding current destination.</param>
    /// <param name="fileCount">The accumulated number of restored files.</param>
    /// <param name="totalBytes">The accumulated size, in bytes, of restored files.</param>
    private static void RestoreDirectoryContents(
        DirectoryInfo payloadDirectory,
        string destinationPath,
        ref long fileCount,
        ref long totalBytes
    )
    {
        foreach (var payloadFile in payloadDirectory.EnumerateFiles())
        {
            FileSystemSafety.ThrowIfReparsePoint(payloadFile);

            var destinationFilePath = Path.Combine(
                destinationPath,
                payloadFile.Name
            );

            var destinationFile = new FileInfo(destinationFilePath);

            if (destinationFile.Exists)
            {
                FileSystemSafety.ThrowIfReparsePoint(destinationFile);
            }

            payloadFile.CopyTo(
                destinationFilePath,
                overwrite: true
            );

            fileCount++;
            totalBytes += payloadFile.Length;
        }

        foreach (var childPayloadDirectory in payloadDirectory.EnumerateDirectories())
        {
            FileSystemSafety.ThrowIfReparsePoint(childPayloadDirectory);

            var childDestinationPath = Path.Combine(
                destinationPath,
                childPayloadDirectory.Name
            );

            FileSystemSafety.EnsureExistingPathContainsNoReparsePoints(childDestinationPath);
            Directory.CreateDirectory(childDestinationPath);

            RestoreDirectoryContents(
                childPayloadDirectory,
                childDestinationPath,
                ref fileCount,
                ref totalBytes
            );
        }
    }

    #endregion

    #region Clear Operations

    /// <inheritdoc/>
    public FileOperationStatistics Inspect(string sourcePath)
    {
        if (!Directory.Exists(sourcePath))
        {
            return new FileOperationStatistics(
                FileCount: 0,
                TotalBytes: 0
            );
        }

        return InspectDirectoryContents(
            new DirectoryInfo(sourcePath)
        );
    }

    /// <summary>
    /// Recursively validates directory contents and calculates their file count and combined size.
    /// </summary>
    /// <param name="directory">The directory currently being inspected.</param>
    /// <returns>Statistics describing all files contained by the directory.</returns>
    private static FileOperationStatistics InspectDirectoryContents(DirectoryInfo directory)
    {
        FileSystemSafety.ThrowIfReparsePoint(directory);

        long fileCount = 0;
        long totalBytes = 0;

        foreach (var file in directory.EnumerateFiles())
        {
            FileSystemSafety.ThrowIfReparsePoint(file);

            fileCount++;
            totalBytes += file.Length;
        }

        foreach (var childDirectory in directory.EnumerateDirectories())
        {
            var childStatistics = InspectDirectoryContents(childDirectory);

            fileCount += childStatistics.FileCount;
            totalBytes += childStatistics.TotalBytes;
        }

        return new FileOperationStatistics(
            FileCount: fileCount,
            TotalBytes: totalBytes
        );
    }

    /// <inheritdoc/>
    public void Clear(string sourcePath)
    {
        if (!Directory.Exists(sourcePath))
        {
            return;
        }

        var directory = new DirectoryInfo(sourcePath);

        foreach (var file in directory.EnumerateFiles())
        {
            file.Delete();
        }

        foreach (var childDirectory in directory.EnumerateDirectories())
        {
            childDirectory.Delete(recursive: true);
        }
    }

    #endregion
}