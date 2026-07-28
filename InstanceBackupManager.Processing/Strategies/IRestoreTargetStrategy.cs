namespace InstanceBackupManager.Processing.Strategies;

/// <summary>
/// Defines the validation and restoration algorithms used for one stored backup-payload type.
/// </summary>
internal interface IRestoreTargetStrategy : ITargetPathStrategy
{
    #region Methods

    /// <summary>
    /// Verifies that a stored payload exists, matches the expected filesystem-entry type, and is safe to restore.
    /// </summary>
    /// <param name="payloadPath">The absolute stored-payload path.</param>
    void ValidatePayload(string payloadPath);

    /// <summary>
    /// Restores a previously validated payload to its current configured destination.
    /// </summary>
    /// <param name="payloadPath">The absolute stored-payload path.</param>
    /// <param name="destinationPath">The absolute current destination path.</param>
    /// <returns>The number of files and bytes restored.</returns>
    FileOperationStatistics Restore(
        string payloadPath,
        string destinationPath
    );

    #endregion
}