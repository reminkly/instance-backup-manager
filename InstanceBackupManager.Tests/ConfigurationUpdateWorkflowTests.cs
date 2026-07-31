using InstanceBackupManager.Console.Workflows;
using InstanceBackupManager.Processing;
using InstanceBackupManager.Processing.Exceptions;

namespace InstanceBackupManager.Tests;

/// <summary>
/// Tests the non-destructive return path from the configuration-update prompt.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class ConfigurationUpdateWorkflowTests
{
    #region Tests

    [TestMethod]
    public void Run_WhenReturnIsSelected_DisplaysSchemaDetailsAndReturnsToInstances()
    {
        var originalInput = System.Console.In;
        var originalOutput = System.Console.Out;

        try
        {
            System.Console.SetIn(new StringReader("r"));
            var output = new StringWriter();
            System.Console.SetOut(output);

            var outcome = new ConfigurationUpdateWorkflow(new ConfigProcessor()).Run(
                "Old Instance",
                new UnsupportedInstanceConfigurationSchemaException(
                    Path.Combine(@"C:\Instances", "Old Instance", "instance.json"),
                    configuredVersion: 1,
                    supportedVersion: 3
                )
            );

            Assert.AreEqual(ConfigurationUpdateWorkflowOutcome.ReturnToInstances, outcome);
            StringAssert.Contains(output.ToString(), "Configuration Update Required");
            StringAssert.Contains(output.ToString(), "Current schema:    1");
            StringAssert.Contains(output.ToString(), "Supported schema:  3");
        }
        finally
        {
            System.Console.SetIn(originalInput);
            System.Console.SetOut(originalOutput);
        }
    }

    #endregion
}
