using System.Text.Json.Nodes;

namespace InstanceBackupManager.Processing.Migrations;

/// <summary>
/// Defines one version-to-version transformation for an instance configuration document.
/// </summary>
internal interface IInstanceConfigMigration
{
    #region Properties

    /// <summary>
    /// Gets the schema version accepted by this migration.
    /// </summary>
    int SourceVersion { get; }

    /// <summary>
    /// Gets the schema version produced by this migration.
    /// </summary>
    int TargetVersion { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Creates a migrated configuration document without modifying the supplied document.
    /// </summary>
    /// <param name="configuration">The configuration document using <see cref="SourceVersion"/>.</param>
    /// <returns>A new configuration document using <see cref="TargetVersion"/>.</returns>
    JsonObject Migrate(JsonObject configuration);

    #endregion
}
