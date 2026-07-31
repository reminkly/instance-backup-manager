using System.Text.Json;
using System.Text.Json.Nodes;

namespace InstanceBackupManager.Processing.Migrations;

/// <summary>
/// Selects and applies consecutive configuration migrations until a requested schema version is reached.
/// </summary>
internal sealed class InstanceConfigMigrationPipeline
{
    #region Properties

    /// <summary>
    /// Gets migrations indexed by the schema version they accept.
    /// </summary>
    private IReadOnlyDictionary<int, IInstanceConfigMigration> Migrations { get; }

    /// <summary>
    /// Gets the options used to parse tolerant configuration JSON.
    /// </summary>
    private JsonDocumentOptions DocumentOptions { get; } = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a migration pipeline containing every migration supported by the application.
    /// </summary>
    internal InstanceConfigMigrationPipeline()
        : this(
            [
                new Version1ToVersion2Migration(),
                new Version2ToVersion3Migration()
            ]
        )
    {

    }

    /// <summary>
    /// Initializes a migration pipeline from the supplied version-to-version migrations.
    /// </summary>
    /// <param name="migrations">The available configuration migrations.</param>
    internal InstanceConfigMigrationPipeline(IEnumerable<IInstanceConfigMigration> migrations)
    {
        ArgumentNullException.ThrowIfNull(migrations);

        var migrationList = migrations.ToList();

        if (migrationList.Any(migration => migration.TargetVersion <= migration.SourceVersion))
        {
            throw new ArgumentException(
                "Every configuration migration must advance to a greater schema version.",
                nameof(migrations)
            );
        }

        Migrations = migrationList.ToDictionary(
            migration => migration.SourceVersion
        );
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Determines whether consecutive registered migrations can reach the requested version.
    /// </summary>
    internal bool CanMigrate(
        int sourceVersion,
        int targetVersion
    )
    {
        if (sourceVersion >= targetVersion)
        {
            return false;
        }

        var currentVersion = sourceVersion;

        while (currentVersion < targetVersion)
        {
            if (!Migrations.TryGetValue(currentVersion, out var migration)
                || migration.TargetVersion > targetVersion)
            {
                return false;
            }

            currentVersion = migration.TargetVersion;
        }

        return currentVersion == targetVersion;
    }

    /// <summary>
    /// Parses and migrates a configuration through every required registered version step.
    /// </summary>
    internal string Migrate(
        string json,
        int sourceVersion,
        int targetVersion
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        if (!CanMigrate(sourceVersion, targetVersion))
        {
            throw new InvalidOperationException(
                $"No complete configuration migration path exists from schema version '{sourceVersion}' to '{targetVersion}'."
            );
        }

        var configuration = JsonNode.Parse(
            json,
            documentOptions: DocumentOptions
        ) as JsonObject
            ?? throw new InvalidDataException("The instance configuration must contain a JSON object.");

        var currentVersion = sourceVersion;

        while (currentVersion < targetVersion)
        {
            var migration = Migrations[currentVersion];
            configuration = migration.Migrate(configuration);
            currentVersion = migration.TargetVersion;
        }

        return configuration.ToJsonString(
            new JsonSerializerOptions
            {
                WriteIndented = true
            }
        );
    }

    #endregion
}
