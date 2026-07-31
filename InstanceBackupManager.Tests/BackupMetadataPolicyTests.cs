using InstanceBackupManager.Processing.Policies;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests normalization and validation of optional backup notes and tags.
/// </summary>
[TestClass]
public sealed class BackupMetadataPolicyTests
{
    #region Tests

    [TestMethod]
    public void NormalizeNotes_WhenValueHasOuterWhitespace_ReturnsTrimmedValue()
    {
        Assert.AreEqual(
            "Before final boss",
            BackupMetadataPolicy.NormalizeNotes("  Before final boss  ")
        );
    }

    [TestMethod]
    public void NormalizeTags_WhenValuesRepeatCaseInsensitively_ReturnsUniqueTrimmedValues()
    {
        var tags = BackupMetadataPolicy.NormalizeTags(
            [" milestone ", "Story", "MILESTONE", ""]
        );

        CollectionAssert.AreEqual(
            new[] { "milestone", "Story" },
            tags.ToArray()
        );
    }

    [TestMethod]
    public void NormalizeTags_WhenTooManyValues_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => BackupMetadataPolicy.NormalizeTags(
                Enumerable.Range(1, BackupMetadataPolicy.MaximumTagCount + 1).Select(index => $"tag-{index}")
            )
        );
    }

    #endregion
}
