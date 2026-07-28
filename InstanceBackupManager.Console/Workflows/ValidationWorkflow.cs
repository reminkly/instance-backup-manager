using InstanceBackupManager.Console.Utilities;
using InstanceBackupManager.Processing;
using InstanceBackupManager.Processing.Enums;
using InstanceBackupManager.Processing.Models.Instances;
using InstanceBackupManager.Processing.Models.Validation;
using SystemConsole = System.Console;

namespace InstanceBackupManager.Console.Workflows;

/// <summary>
/// Displays a non-mutating validation report for a configured instance.
/// </summary>
internal sealed class ValidationWorkflow(InstanceValidationProcessor validationProcessor)
{
    #region Properties

    /// <summary>
    /// Gets the processor used to validate loaded instances.
    /// </summary>
    private InstanceValidationProcessor ValidationProcessor { get; } =
        validationProcessor ?? throw new ArgumentNullException(nameof(validationProcessor));

    #endregion

    #region Internal Methods

    /// <summary>
    /// Validates an instance, displays every finding, and returns to the instance menu.
    /// </summary>
    /// <param name="instance">The loaded instance to validate.</param>
    /// <returns>Zero because validation findings are reported results rather than application failures.</returns>
    internal int Run(InstanceContext instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var report = ValidationProcessor.Validate(instance);

        SystemConsole.WriteLine();
        SystemConsole.WriteLine($"Validating: {report.InstanceName}");
        SystemConsole.WriteLine(new string('=', $"Validating: {report.InstanceName}".Length));

        foreach (var category in report.Findings.GroupBy(finding => finding.Category))
        {
            SystemConsole.WriteLine();
            SystemConsole.WriteLine(category.Key);
            SystemConsole.WriteLine(new string('-', category.Key.Length));

            foreach (var finding in category)
            {
                SystemConsole.WriteLine(
                    $"[{GetSeverityLabel(finding)}] {finding.Message}"
                );
            }
        }

        SystemConsole.WriteLine();
        SystemConsole.WriteLine(
            $"Validation completed with {report.WarningCount} warning{(report.WarningCount == 1 ? string.Empty : "s")} " +
            $"and {report.ErrorCount} error{(report.ErrorCount == 1 ? string.Empty : "s")}."
        );

        SystemConsole.WriteLine("No files were changed.");
        ConsoleHelper.WaitForContinue();

        return 0;
    }

    #endregion

    #region Display Helpers

    /// <summary>
    /// Gets the display label associated with a validation finding.
    /// </summary>
    private static string GetSeverityLabel(ValidationFinding finding)
    {
        return finding.Severity switch
        {
            ValidationSeverity.Success => "OK",
            ValidationSeverity.Warning => "WARNING",
            ValidationSeverity.Error => "ERROR",
            _ => "UNKNOWN"
        };
    }

    #endregion
}
