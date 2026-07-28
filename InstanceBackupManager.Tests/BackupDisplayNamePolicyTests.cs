using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Backups;
using InstanceBackupManager.Processing.Policies;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests centralized backup display-name generation, validation, and backward-compatible fallback behavior.
/// </summary>
[TestClass]
public sealed class BackupDisplayNamePolicyTests
{
    #region Fields

    private static readonly DateTimeOffset BackupTime = new(2026, 7, 28, 18, 30, 5, TimeSpan.Zero);

    #endregion

    #region Display Name Creation

    /// <summary>
    /// Verifies that a supplied name is trimmed before it is persisted.
    /// </summary>
    [TestMethod]
    public void CreateDisplayName_WhenNameIsProvided_TrimsName()
    {
        var displayName = BackupDisplayNamePolicy.CreateDisplayName(
            BackupKind.Manual,
            "  Before Palace of Winds  ",
            BackupTime
        );

        Assert.AreEqual("Before Palace of Winds", displayName);
    }

    /// <summary>
    /// Verifies that an omitted manual name receives a useful timestamped fallback.
    /// </summary>
    [TestMethod]
    public void CreateDisplayName_WhenManualNameIsBlank_GeneratesManualName()
    {
        var displayName = BackupDisplayNamePolicy.CreateDisplayName(
            BackupKind.Manual,
            "   ",
            BackupTime
        );

        Assert.AreEqual(
            $"Manual backup - {BackupTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}",
            displayName
        );
    }

    /// <summary>
    /// Verifies that pre-restore names identify the selected backup that triggered the safety backup.
    /// </summary>
    [TestMethod]
    public void CreatePreRestoreDisplayName_WhenSelectedBackupHasName_IdentifiesSelectedBackup()
    {
        var selectedBackup = CreateBackupDescriptor("Before Palace of Winds");

        var displayName = BackupDisplayNamePolicy.CreatePreRestoreDisplayName(selectedBackup);

        Assert.AreEqual("Before restoring \u0022Before Palace of Winds\u0022", displayName);
    }

    /// <summary>
    /// Verifies that a generated pre-restore name is shortened when the selected name would exceed the storage limit.
    /// </summary>
    [TestMethod]
    public void CreatePreRestoreDisplayName_WhenSelectedNameIsLong_RespectsMaximumLength()
    {
        var selectedBackup = CreateBackupDescriptor(new string('A', BackupDisplayNamePolicy.MaximumLength));

        var displayName = BackupDisplayNamePolicy.CreatePreRestoreDisplayName(selectedBackup);

        Assert.AreEqual(BackupDisplayNamePolicy.MaximumLength, displayName.Length);
    }

    #endregion

    #region Validation

    /// <summary>
    /// Verifies that names longer than the configured limit are rejected.
    /// </summary>
    [TestMethod]
    public void Normalize_WhenNameIsTooLong_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => BackupDisplayNamePolicy.Normalize(
                new string('A', BackupDisplayNamePolicy.MaximumLength + 1)
            )
        );
    }

    /// <summary>
    /// Verifies that line breaks and other control characters cannot be stored in a display name.
    /// </summary>
    [TestMethod]
    public void Normalize_WhenNameContainsControlCharacter_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => BackupDisplayNamePolicy.Normalize("First line\nSecond line")
        );
    }

    #endregion

    #region Backward Compatibility

    /// <summary>
    /// Verifies that manifests created before display names were introduced still receive a useful label.
    /// </summary>
    [TestMethod]
    public void GetDisplayName_WhenManifestHasNoDisplayName_GeneratesFallbackName()
    {
        var manifest = CreateBackupDescriptor(displayName: null).Manifest;

        var displayName = BackupDisplayNamePolicy.GetDisplayName(manifest);

        Assert.AreEqual(
            $"Manual backup - {BackupTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}",
            displayName
        );
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Creates a completed backup descriptor containing the supplied optional display name.
    /// </summary>
    /// <param name="displayName">The optional persisted display name.</param>
    /// <returns>A completed backup descriptor suitable for policy tests.</returns>
    private static BackupDescriptor CreateBackupDescriptor(string? displayName)
    {
        const string backupName = "2026-07-28_18-30-05-000Z";

        return new BackupDescriptor
        {
            BackupName = backupName,
            BackupPath = Path.Combine("C:\\Backups", backupName),
            Manifest = new BackupManifest
            {
                InstanceName = "Test Instance",
                DisplayName = displayName,
                BackupName = backupName,
                Kind = BackupKind.Manual,
                CreatedUtc = BackupTime,
                Entries = Array.Empty<BackupManifestEntry>()
            }
        };
    }

    #endregion
}
