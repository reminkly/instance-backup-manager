using InstanceBackupManager.Processing;
using InstanceBackupManager.Processing.Constants;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests safe instance-directory creation, skeleton configuration generation, name validation, and collision handling.
/// </summary>
[TestClass]
public sealed class InstanceCreationProcessorTests
{
    #region Fields

    private string _testRootPath = null!;
    private string _instancesPath = null!;
    private ConfigProcessor _configProcessor = null!;
    private InstanceCreationProcessor _processor = null!;

    #endregion

    #region Test Initialization

    /// <summary>
    /// Creates an isolated instances directory and processor before each test.
    /// </summary>
    [TestInitialize]
    public void TestInitialize()
    {
        _testRootPath = Path.Combine(
            Path.GetTempPath(),
            "InstanceBackupManagerTests",
            Guid.NewGuid().ToString("N")
        );

        _instancesPath = Path.Combine(
            _testRootPath,
            BackupStorageConstants.InstancesDirectoryName
        );

        _configProcessor = new ConfigProcessor();
        _processor = new InstanceCreationProcessor(_configProcessor);
    }

    /// <summary>
    /// Removes the isolated test directory after each test.
    /// </summary>
    [TestCleanup]
    public void TestCleanup()
    {
        if (Directory.Exists(_testRootPath))
        {
            Directory.Delete(
                _testRootPath,
                recursive: true
            );
        }
    }

    #endregion

    #region Creation Tests

    /// <summary>
    /// Verifies that creation writes a loadable skeleton while preserving separate display and directory names.
    /// </summary>
    [TestMethod]
    public void CreateInstance_WhenValuesAreValid_CreatesLoadableSkeleton()
    {
        var result = _processor.CreateInstance(
            _instancesPath,
            "Pokémon Emerald - Kaizo",
            "Pokemon Emerald Kaizo"
        );

        var instance = _configProcessor.LoadInstance(result.InstancePath);

        Assert.AreEqual("Pokémon Emerald - Kaizo", result.Name);
        Assert.AreEqual("Pokemon Emerald Kaizo", result.FolderName);
        Assert.AreEqual("Pokémon Emerald - Kaizo", instance.Config.Name);
        Assert.IsTrue(File.Exists(result.ConfigPath));
        Assert.HasCount(1, instance.Config.Targets);
        Assert.IsFalse(instance.Config.Targets.Single().Enabled);
    }

    /// <summary>
    /// Verifies that leading and trailing whitespace is removed from both persisted names.
    /// </summary>
    [TestMethod]
    public void CreateInstance_WhenNamesContainOuterWhitespace_TrimsNames()
    {
        var result = _processor.CreateInstance(
            _instancesPath,
            "  Test Instance  ",
            "  Test Folder  "
        );

        Assert.AreEqual("Test Instance", result.Name);
        Assert.AreEqual("Test Folder", result.FolderName);
    }

    /// <summary>
    /// Verifies that an existing directory cannot be overwritten by instance creation.
    /// </summary>
    [TestMethod]
    public void CreateInstance_WhenDirectoryAlreadyExists_ThrowsIOException()
    {
        Directory.CreateDirectory(
            Path.Combine(_instancesPath, "Existing")
        );

        Assert.ThrowsExactly<IOException>(
            () => _processor.CreateInstance(
                _instancesPath,
                "Existing Instance",
                "Existing"
            )
        );
    }

    /// <summary>
    /// Verifies that parent traversal cannot escape the configured instances directory.
    /// </summary>
    [TestMethod]
    public void CreateInstance_WhenFolderNameTraversesParent_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => _processor.CreateInstance(
                _instancesPath,
                "Unsafe Instance",
                ".."
            )
        );
    }

    /// <summary>
    /// Verifies that Windows-reserved device names cannot be used as instance directories.
    /// </summary>
    [TestMethod]
    public void CreateInstance_WhenFolderNameIsReserved_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => _processor.CreateInstance(
                _instancesPath,
                "Console Instance",
                "CON"
            )
        );
    }

    /// <summary>
    /// Verifies that control characters are rejected from user-facing instance names.
    /// </summary>
    [TestMethod]
    public void CreateInstance_WhenInstanceNameContainsControlCharacter_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => _processor.CreateInstance(
                _instancesPath,
                "First line\nSecond line",
                "Valid Folder"
            )
        );
    }

    #endregion

    #region Suggestion Tests

    /// <summary>
    /// Verifies that suggested folder names replace invalid filesystem characters.
    /// </summary>
    [TestMethod]
    public void CreateSuggestedFolderName_WhenNameContainsInvalidCharacters_ReturnsSafeSuggestion()
    {
        var suggestion = _processor.CreateSuggestedFolderName("Game: Special Edition");

        Assert.AreEqual("Game- Special Edition", suggestion);
    }

    /// <summary>
    /// Verifies that a reserved display name receives a non-reserved folder suggestion.
    /// </summary>
    [TestMethod]
    public void CreateSuggestedFolderName_WhenNameIsReserved_ReturnsSafeSuggestion()
    {
        var suggestion = _processor.CreateSuggestedFolderName("CON");

        Assert.AreEqual("CON Instance", suggestion);
    }

    #endregion
}
