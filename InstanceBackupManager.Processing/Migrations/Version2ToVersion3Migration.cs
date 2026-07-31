using System.Text.Json.Nodes;

namespace InstanceBackupManager.Processing.Migrations;

/// <summary>
/// Migrates schema-version-2 configurations to the schema that supports optional stored filenames for file targets.
/// </summary>
internal sealed class Version2ToVersion3Migration : IInstanceConfigMigration
{
    #region Properties

    public int SourceVersion => 2;

    public int TargetVersion => 3;

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public JsonObject Migrate(JsonObject configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var migratedConfiguration = configuration.DeepClone().AsObject();
        var schemaPropertyName = migratedConfiguration
            .Select(property => property.Key)
            .FirstOrDefault(
                propertyName => string.Equals(
                    propertyName,
                    "SchemaVersion",
                    StringComparison.OrdinalIgnoreCase
                )
            );

        if (schemaPropertyName is null)
        {
            migratedConfiguration.Add("SchemaVersion", TargetVersion);
        }
        else
        {
            migratedConfiguration[schemaPropertyName] = TargetVersion;
        }

        return migratedConfiguration;
    }

    #endregion
}
