namespace ClinStrata.QC.Engine.Models;

/// <summary>
/// The complete result of a Westgard multi-rule evaluation for a set of QC observations.
/// </summary>
public record QcEvaluationResult
{
    public required bool IsAccepted { get; init; }
    public ViolationResult? PrimaryViolation { get; init; }
    public IReadOnlyList<ViolationResult> AllViolations { get; init; } = [];
    public bool WarningTriggered { get; init; }
    public required IReadOnlyList<QcObservation> Observations { get; init; }
    public required QcControlLimits ControlLimits { get; init; }
}
