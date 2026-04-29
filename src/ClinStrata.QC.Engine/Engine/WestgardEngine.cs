using ClinStrata.QC.Engine.Models;
using ClinStrata.QC.Engine.Rules;

namespace ClinStrata.QC.Engine.Engine;

/// <summary>Evaluation mode for Westgard multi-rule analysis.</summary>
public enum EvaluationMode { Sequential, Exhaustive }

/// <summary>
/// Stateless Westgard multi-rule QC engine.
/// Each call is independent — no state is maintained between evaluations.
/// Thread-safe for concurrent use.
/// </summary>
public sealed class WestgardEngine
{
    private readonly Rule1_2s _rule1_2s = new();
    private readonly Rule1_3s _rule1_3s = new();
    private readonly Rule2_2s _rule2_2s = new();
    private readonly RuleR_4s _ruleR_4s = new();
    private readonly Rule4_1s _rule4_1s = new();
    private readonly Rule10x  _rule10x  = new();

    private IReadOnlyList<IQcRule> RejectionRules =>
        [_rule1_3s, _rule2_2s, _ruleR_4s, _rule4_1s, _rule10x];

    /// <summary>
    /// Evaluates QC observations against control limits using the Westgard multi-rule procedure.
    /// The 1-2s rule serves as a warning indicator only — rejection rules are evaluated on every
    /// call regardless of whether 1-2s fires, consistent with the Westgard specification.
    /// </summary>
    public QcEvaluationResult Evaluate(
        IReadOnlyList<QcObservation> observations,
        QcControlLimits limits,
        EvaluationMode mode = EvaluationMode.Sequential)
    {
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();

        if (observations.Count == 0)
            throw new ArgumentException(
                "At least one observation is required.", nameof(observations));

        // Step 1: 1-2s warning — sets flag but does NOT gate rejection evaluation
        var warning = _rule1_2s.Evaluate(observations, limits);
        bool warningTriggered = warning is not null;

        // Step 2: Always evaluate all rejection rules regardless of warning status
        var allViolations = new List<ViolationResult>();
        ViolationResult? primary = null;

        foreach (var rule in RejectionRules)
        {
            if (observations.Count < rule.MinimumObservations) continue;
            var v = rule.Evaluate(observations, limits);
            if (v is null) continue;

            allViolations.Add(v);

            if (mode == EvaluationMode.Sequential)
                return new QcEvaluationResult
                {
                    IsAccepted = false,
                    WarningTriggered = warningTriggered,
                    PrimaryViolation = v,
                    AllViolations = [v],
                    Observations = observations,
                    ControlLimits = limits
                };

            primary ??= v;
        }

        bool hasRejection = allViolations.Any(v => v.IsRejection);
        if (warning is not null && !allViolations.Contains(warning))
            allViolations.Insert(0, warning);

        return new QcEvaluationResult
        {
            IsAccepted = !hasRejection,
            WarningTriggered = warningTriggered,
            PrimaryViolation = primary,
            AllViolations = allViolations.AsReadOnly(),
            Observations = observations,
            ControlLimits = limits
        };
    }
}
