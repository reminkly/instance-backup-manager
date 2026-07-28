using InstanceBackupManager.Processing.Utilities;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests operating-system-aware path comparison, containment, overlap, and filesystem-entry safety rules.
/// </summary>
[TestClass]
public sealed class FileSystemSafetyTests
{
    #region Fields

    private string _testRootPath = null!;

    #endregion

    #region Test Initialization

    /// <summary>
    /// Creates an isolated directory before each test.
    /// </summary>
    [TestInitialize]
    public void TestInitialize()
    {
        _testRootPath = Path.Combine(
            Path.GetTempPath(),
            "InstanceBackupManagerTests",
            Guid.NewGuid().ToString("N")
        );

        Directory.CreateDirectory(_testRootPath);
    }

    /// <summary>
    /// Removes the isolated directory after each test.
    /// </summary>
    [TestCleanup]
    public void TestCleanup()
    {
        if (Directory.Exists(_testRootPath))
        {
            Directory.Delete(_testRootPath, recursive: true);
        }
    }

    #endregion

    #region Path Equality Tests

    /// <summary>
    /// Verifies that equivalent absolute paths are treated as equal after normalization.
    /// </summary>
    [TestMethod]
    public void PathsEqual_WhenPathsResolveToSameLocation_ReturnsTrue()
    {
        var firstPath = Path.Combine(
            _testRootPath,
            "Folder"
        );

        var secondPath = Path.Combine(
            _testRootPath,
            ".",
            "Folder"
        );

        var result = FileSystemSafety.PathsEqual(
            firstPath,
            secondPath
        );

        Assert.IsTrue(result);
    }

    /// <summary>
    /// Verifies that paths resolving to different locations are not treated as equal.
    /// </summary>
    [TestMethod]
    public void PathsEqual_WhenPathsResolveToDifferentLocations_ReturnsFalse()
    {
        var firstPath = Path.Combine(
            _testRootPath,
            "First"
        );

        var secondPath = Path.Combine(
            _testRootPath,
            "Second"
        );

        var result = FileSystemSafety.PathsEqual(
            firstPath,
            secondPath
        );

        Assert.IsFalse(result);
    }

    #endregion

    #region Path Relationship Tests

    /// <summary>
    /// Verifies that a path beneath the filesystem root is recognized as overlapping the root.
    /// </summary>
    [TestMethod]
    public void PathsOverlap_WhenOnePathIsFileSystemRoot_ReturnsTrue()
    {
        var rootPath = Path.GetPathRoot(_testRootPath);

        Assert.IsNotNull(rootPath);

        var result = FileSystemSafety.PathsOverlap(
            rootPath,
            _testRootPath
        );

        Assert.IsTrue(result);
    }

    /// <summary>
    /// Verifies that a direct child is recognized as belonging to its parent path.
    /// </summary>
    [TestMethod]
    public void IsSamePathOrChildOf_WhenCandidateIsChild_ReturnsTrue()
    {
        var childPath = Path.Combine(
            _testRootPath,
            "Parent",
            "Child"
        );

        var parentPath = Path.Combine(
            _testRootPath,
            "Parent"
        );

        var result = FileSystemSafety.IsSamePathOrChildOf(
            childPath,
            parentPath
        );

        Assert.IsTrue(result);
    }

    /// <summary>
    /// Verifies that a path is recognized as belonging to itself.
    /// </summary>
    [TestMethod]
    public void IsSamePathOrChildOf_WhenPathsAreEqual_ReturnsTrue()
    {
        var result = FileSystemSafety.IsSamePathOrChildOf(
            _testRootPath,
            _testRootPath
        );

        Assert.IsTrue(result);
    }

    /// <summary>
    /// Verifies that sibling paths are not treated as parent and child.
    /// </summary>
    [TestMethod]
    public void IsSamePathOrChildOf_WhenPathsAreSiblings_ReturnsFalse()
    {
        var firstPath = Path.Combine(
            _testRootPath,
            "First"
        );

        var secondPath = Path.Combine(
            _testRootPath,
            "Second"
        );

        var result = FileSystemSafety.IsSamePathOrChildOf(
            firstPath,
            secondPath
        );

        Assert.IsFalse(result);
    }

    /// <summary>
    /// Verifies that a parent path and its child are considered overlapping.
    /// </summary>
    [TestMethod]
    public void PathsOverlap_WhenOnePathContainsOther_ReturnsTrue()
    {
        var parentPath = Path.Combine(
            _testRootPath,
            "Parent"
        );

        var childPath = Path.Combine(
            parentPath,
            "Child"
        );

        var result = FileSystemSafety.PathsOverlap(
            parentPath,
            childPath
        );

        Assert.IsTrue(result);
    }

    /// <summary>
    /// Verifies that unrelated sibling paths are not considered overlapping.
    /// </summary>
    [TestMethod]
    public void PathsOverlap_WhenPathsAreUnrelated_ReturnsFalse()
    {
        var firstPath = Path.Combine(
            _testRootPath,
            "First"
        );

        var secondPath = Path.Combine(
            _testRootPath,
            "Second"
        );

        var result = FileSystemSafety.PathsOverlap(
            firstPath,
            secondPath
        );

        Assert.IsFalse(result);
    }

    #endregion

    #region Path Containment Tests

    /// <summary>
    /// Verifies that a filesystem root is not accepted as a child of itself when strict containment is required.
    /// </summary>
    [TestMethod]
    public void EnsurePathIsWithinDirectory_WhenCandidateAndParentAreFileSystemRoot_ThrowsInvalidDataException()
    {
        var rootPath = Path.GetPathRoot(_testRootPath);

        Assert.IsNotNull(rootPath);

        Assert.ThrowsExactly<InvalidDataException>(
            () => FileSystemSafety.EnsurePathIsWithinDirectory(
                rootPath,
                rootPath,
                "Test path"
            )
        );
    }

    /// <summary>
    /// Verifies that a path beneath the required parent passes containment validation.
    /// </summary>
    [TestMethod]
    public void EnsurePathIsWithinDirectory_WhenCandidateIsChild_DoesNotThrow()
    {
        var childPath = Path.Combine(
            _testRootPath,
            "Child"
        );

        FileSystemSafety.EnsurePathIsWithinDirectory(
            childPath,
            _testRootPath,
            "Test path"
        );
    }

    /// <summary>
    /// Verifies that the parent directory itself is not accepted when a contained child path is required.
    /// </summary>
    [TestMethod]
    public void EnsurePathIsWithinDirectory_WhenCandidateEqualsParent_ThrowsInvalidDataException()
    {
        var exception = Assert.ThrowsExactly<InvalidDataException>(
            () => FileSystemSafety.EnsurePathIsWithinDirectory(
                _testRootPath,
                _testRootPath,
                "Test path"
            )
        );

        StringAssert.Contains(
            exception.Message,
            "Test path escapes its required parent directory."
        );
    }

    /// <summary>
    /// Verifies that a sibling path is rejected when it is outside the required parent directory.
    /// </summary>
    [TestMethod]
    public void EnsurePathIsWithinDirectory_WhenCandidateIsOutsideParent_ThrowsInvalidDataException()
    {
        var requiredParentPath = Path.Combine(
            _testRootPath,
            "Parent"
        );

        var outsidePath = Path.Combine(
            _testRootPath,
            "Outside"
        );

        Assert.ThrowsExactly<InvalidDataException>(
            () => FileSystemSafety.EnsurePathIsWithinDirectory(
                outsidePath,
                requiredParentPath,
                "Test path"
            )
        );
    }

    #endregion

    #region Reparse-Point Tests

    /// <summary>
    /// Verifies that an ordinary directory passes direct reparse-point validation.
    /// </summary>
    [TestMethod]
    public void ThrowIfReparsePoint_WhenEntryIsOrdinaryDirectory_DoesNotThrow()
    {
        var directory = new DirectoryInfo(_testRootPath);

        FileSystemSafety.ThrowIfReparsePoint(directory);
    }

    /// <summary>
    /// Verifies that a null filesystem entry is rejected.
    /// </summary>
    [TestMethod]
    public void ThrowIfReparsePoint_WhenEntryIsNull_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => FileSystemSafety.ThrowIfReparsePoint(null!)
        );
    }

    /// <summary>
    /// Verifies that a normal existing path and all of its ancestors pass reparse-point validation.
    /// </summary>
    [TestMethod]
    public void EnsureExistingPathContainsNoReparsePoints_WhenPathIsOrdinary_DoesNotThrow()
    {
        var nestedPath = Path.Combine(
            _testRootPath,
            "Parent",
            "Child"
        );

        Directory.CreateDirectory(nestedPath);

        FileSystemSafety.EnsureExistingPathContainsNoReparsePoints(nestedPath);
    }

    /// <summary>
    /// Verifies that validation can walk upward from a path whose final components do not yet exist.
    /// </summary>
    [TestMethod]
    public void EnsureExistingPathContainsNoReparsePoints_WhenLeafDoesNotExist_DoesNotThrow()
    {
        var missingPath = Path.Combine(
            _testRootPath,
            "Missing",
            "Child"
        );

        FileSystemSafety.EnsureExistingPathContainsNoReparsePoints(missingPath);
    }

    #endregion
}