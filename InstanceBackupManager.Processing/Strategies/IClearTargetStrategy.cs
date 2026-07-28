namespace InstanceBackupManager.Processing.Strategies;

/// <summary>
/// Defines the inspection and deletion algorithms used to clear one target type.
/// </summary>
internal interface IClearTargetStrategy : ITargetPathStrategy
{
    #region Methods

    /// <summary>
    /// Inspects a target without modifying it.
    /// </summary>
    /// <param name="sourcePath">The absolute configured source path.</param>
    /// <returns>The number of files and bytes that would be removed.</returns>
    FileOperationStatistics Inspect(string sourcePath);

    /// <summary>
    /// Clears a previously inspected target.
    /// </summary>
    /// <param name="sourcePath">The absolute configured source path.</param>
    void Clear(string sourcePath);

    #endregion
}