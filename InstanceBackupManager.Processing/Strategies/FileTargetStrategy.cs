using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Utilities;

namespace InstanceBackupManager.Processing.Strategies;

/// <summary>
/// Provides backup, restore, inspection, and clear algorithms for individual file targets.
/// </summary>
internal sealed class FileTargetStrategy :
    IBackupTargetStrategy,
    IRestoreTargetStrategy,
    IClearTargetStrategy
{
    #region Properties

    /// <inheritdoc/>
    public TargetPathType Type => TargetPathType.File;

    #endregion

    #region Backup Operations

    /// <inheritdoc/>
    public bool SourceExists(string sourcePath)
    {
        return File.Exists(sourcePath);
    }

    /// <inheritdoc/>
    public FileOperationStatistics Backup(
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

        FileSystemSafety.ThrowIfReparsePoint(sourceFile);

        var destinationDirectoryPath = Path.GetDirectoryName(destinationPath);

        if (!string.IsNullOrWhiteSpace(destinationDirectoryPath))
        {
            Directory.CreateDirectory(destinationDirectoryPath);
        }

        File.Copy(
            sourcePath,
            destinationPath,
            overwrite: false
        );

        return new FileOperationStatistics(
            FileCount: 1,
            TotalBytes: sourceFile.Length
        );
    }

    #endregion

    #region Restore Operations

    /// <inheritdoc/>
    public void ValidatePayload(string payloadPath)
    {
        var payloadFile = new FileInfo(payloadPath);

        if (!payloadFile.Exists)
        {
            throw new FileNotFoundException(
                "The backup payload file was not found.",
                payloadPath
            );
        }

        FileSystemSafety.ThrowIfReparsePoint(payloadFile);
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

        var payloadFile = new FileInfo(payloadPath);
        var destinationFile = new FileInfo(destinationPath);

        if (destinationFile.Exists)
        {
            FileSystemSafety.ThrowIfReparsePoint(destinationFile);
        }

        var destinationDirectoryPath = Path.GetDirectoryName(destinationPath);

        if (!string.IsNullOrWhiteSpace(destinationDirectoryPath))
        {
            FileSystemSafety.EnsureExistingPathContainsNoReparsePoints(destinationDirectoryPath);
            Directory.CreateDirectory(destinationDirectoryPath);
        }

        File.Copy(
            payloadPath,
            destinationPath,
            overwrite: true
        );

        return new FileOperationStatistics(
            FileCount: 1,
            TotalBytes: payloadFile.Length
        );
    }

    #endregion

    #region Clear Operations

    /// <inheritdoc/>
    public FileOperationStatistics Inspect(string sourcePath)
    {
        if (!File.Exists(sourcePath))
        {
            return new FileOperationStatistics(
                FileCount: 0,
                TotalBytes: 0
            );
        }

        var sourceFile = new FileInfo(sourcePath);

        FileSystemSafety.ThrowIfReparsePoint(sourceFile);

        return new FileOperationStatistics(
            FileCount: 1,
            TotalBytes: sourceFile.Length
        );
    }

    /// <inheritdoc/>
    public void Clear(string sourcePath)
    {
        if (File.Exists(sourcePath))
        {
            File.Delete(sourcePath);
        }
    }

    #endregion
}