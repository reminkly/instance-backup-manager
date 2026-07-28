namespace InstanceBackupManager.Processing.Strategies;

/// <summary>
/// Defines the validation and copy algorithms used to back up one target type.
/// </summary>
internal interface IBackupTargetStrategy : ITargetPathStrategy
{
    #region Methods

    /// <summary>
    /// Determines whether a source exists and matches the filesystem-entry type handled by the strategy.
    /// </summary>
    /// <param name="sourcePath">The absolute configured source path.</param>
    /// <returns><see langword="true"/> when the expected source exists; otherwise, <see langword="false"/>.</returns>
    bool SourceExists(string sourcePath);

    /// <summary>
    /// Copies a source into its resolved backup destination.
    /// </summary>
    /// <param name="sourcePath">The absolute source path.</param>
    /// <param name="destinationPath">The absolute backup destination path.</param>
    /// <returns>The number of files and bytes copied.</returns>
    FileOperationStatistics Backup(
        string sourcePath,
        string destinationPath
    );

    #endregion
}