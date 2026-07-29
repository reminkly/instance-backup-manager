using System.Text.Json.Nodes;
using InstanceBackupManager.Processing.Migrations;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests generic discovery and execution of consecutive configuration migration steps.
/// </summary>
[TestClass]
public sealed class InstanceConfigMigrationPipelineTests
{
    #region Tests

    /// <summary>
    /// Verifies that the pipeline composes multiple registered migrations without knowing their concrete types.
    /// </summary>
    [TestMethod]
    public void Migrate_WhenMultipleStepsReachTarget_AppliesStepsInVersionOrder()
    {
        var pipeline = new InstanceConfigMigrationPipeline(
            [
                new TestMigration(1, 2, "FirstStep"),
                new TestMigration(2, 3, "SecondStep")
            ]
        );

        var migratedJson = pipeline.Migrate(
            """
            {
              "SchemaVersion": 1
            }
            """,
            sourceVersion: 1,
            targetVersion: 3
        );

        var migratedConfiguration = JsonNode.Parse(migratedJson)?.AsObject()
            ?? throw new AssertFailedException("The migrated JSON did not contain an object.");

        Assert.AreEqual(3, migratedConfiguration["SchemaVersion"]?.GetValue<int>());
        Assert.IsTrue(migratedConfiguration["FirstStep"]?.GetValue<bool>());
        Assert.IsTrue(migratedConfiguration["SecondStep"]?.GetValue<bool>());
    }

    /// <summary>
    /// Verifies that a missing intermediate version prevents a partial migration.
    /// </summary>
    [TestMethod]
    public void CanMigrate_WhenIntermediateStepIsMissing_ReturnsFalse()
    {
        var pipeline = new InstanceConfigMigrationPipeline(
            [
                new TestMigration(1, 2, "FirstStep"),
                new TestMigration(3, 4, "ThirdStep")
            ]
        );

        Assert.IsFalse(
            pipeline.CanMigrate(
                sourceVersion: 1,
                targetVersion: 4
            )
        );
    }

    #endregion

    #region Test Types

    /// <summary>
    /// Provides a small migration used to prove that the pipeline is independent of concrete migration implementations.
    /// </summary>
    private sealed class TestMigration(
        int sourceVersion,
        int targetVersion,
        string markerProperty
    ) : IInstanceConfigMigration
    {
        public int SourceVersion { get; } = sourceVersion;

        public int TargetVersion { get; } = targetVersion;

        public JsonObject Migrate(JsonObject configuration)
        {
            var migratedConfiguration = configuration.DeepClone().AsObject();
            migratedConfiguration["SchemaVersion"] = TargetVersion;
            migratedConfiguration[markerProperty] = true;

            return migratedConfiguration;
        }
    }

    #endregion
}
