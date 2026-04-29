namespace ClinStrata.QC.Engine.Models;

public enum ViolationType { None, Warning, RandomError, SystematicError }

/// <summary>
/// Describes a single rule violation event including rule name, type, and triggering observation indices.
/// </summary>
public record ViolationResult(
    string RuleName,
    ViolationType ViolationType,
    int[] TriggeringIndices,
    string Description
)
{
    public bool IsRejection =>
        ViolationType is ViolationType.RandomError or ViolationType.SystematicError;
}
