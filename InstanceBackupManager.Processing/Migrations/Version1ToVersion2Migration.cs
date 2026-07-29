using System.Text.Json.Nodes;
using InstanceBackupManager.Processing.Constants;

namespace InstanceBackupManager.Processing.Migrations;

/// <summary>
/// Migrates schema-version-1 configurations from target-specific backup paths to an instance-level backup root.
/// </summary>
internal sealed class Version1ToVersion2Migration : IInstanceConfigMigration
{
    #region Properties

    /// <inheritdoc />
    public int SourceVersion => 1;

    /// <inheritdoc />
    public int TargetVersion => 2;

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public JsonObject Migrate(JsonObject configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var migratedConfiguration = configuration.DeepClone().AsObject();

        SetProperty(
            migratedConfiguration,
            "SchemaVersion",
            TargetVersion
        );

        if (FindPropertyName(migratedConfiguration, "BackupRoot") is null)
        {
            migratedConfiguration.Add(
                "BackupRoot",
                BackupStorageConstants.BackupsDirectoryName
            );
        }

        var targetsPropertyName = FindPropertyName(
            migratedConfiguration,
            "Targets"
        );

        if (targetsPropertyName is not null
            && migratedConfiguration[targetsPropertyName] is JsonArray targets)
        {
            foreach (var target in targets.OfType<JsonObject>())
            {
                var backupPathPropertyName = FindPropertyName(
                    target,
                    "BackupPath"
                );

                if (backupPathPropertyName is not null)
                {
                    target.Remove(backupPathPropertyName);
                }
            }
        }

        return migratedConfiguration;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Finds the actual property name matching a case-insensitive JSON property lookup.
    /// </summary>
    private static string? FindPropertyName(
        JsonObject jsonObject,
        string propertyName
    )
    {
        return jsonObject
            .Select(property => property.Key)
            .FirstOrDefault(
                candidate => string.Equals(
                    candidate,
                    propertyName,
                    StringComparison.OrdinalIgnoreCase
                )
            );
    }

    /// <summary>
    /// Replaces an existing case-insensitive property or adds it when it is not present.
    /// </summary>
    private static void SetProperty(
        JsonObject jsonObject,
        string propertyName,
        JsonNode? value
    )
    {
        var existingPropertyName = FindPropertyName(
            jsonObject,
            propertyName
        );

        if (existingPropertyName is not null)
        {
            jsonObject[existingPropertyName] = value;
            return;
        }

        jsonObject.Add(
            propertyName,
            value
        );
    }

    #endregion
}
