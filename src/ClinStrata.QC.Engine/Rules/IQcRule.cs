using ClinStrata.QC.Engine.Models;

namespace ClinStrata.QC.Engine.Rules;

/// <summary>
/// Contract for a single Westgard QC rule evaluator.
/// Each rule is stateless and evaluates a window of observations against control limits.
/// </summary>
public interface IQcRule
{
    string RuleName { get; }
    int MinimumObservations { get; }
    ViolationResult? Evaluate(IReadOnlyList<QcObservation> observations, QcControlLimits limits);
}
