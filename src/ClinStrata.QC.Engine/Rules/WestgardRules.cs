using ClinStrata.QC.Engine.Models;

namespace ClinStrata.QC.Engine.Rules;

// ─────────────────────────────────────────────────────────────────────────────
// 1-2s WARNING RULE
// Trigger: any single observation falls outside ±2s (inclusive boundary).
// WARNING ONLY — initiates multi-rule evaluation, does not reject the run.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class Rule1_2s : IQcRule
{
    public string RuleName => "1-2s";
    public int MinimumObservations => 1;

    public ViolationResult? Evaluate(IReadOnlyList<QcObservation> observations, QcControlLimits limits)
    {
        for (int i = 0; i < observations.Count; i++)
        {
            double v = observations[i].Value;
            if (v >= limits.Plus2s || v <= limits.Minus2s)
                return new ViolationResult(RuleName, ViolationType.Warning, [i],
                    $"Observation at index {i} (value={v:F4}) exceeded \u00B12s. Multi-rule evaluation initiated.");
        }
        return null;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 1-3s REJECTION RULE
// Trigger: any single observation falls outside ±3s (inclusive boundary).
// Error type: random error.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class Rule1_3s : IQcRule
{
    public string RuleName => "1-3s";
    public int MinimumObservations => 1;

    public ViolationResult? Evaluate(IReadOnlyList<QcObservation> observations, QcControlLimits limits)
    {
        for (int i = 0; i < observations.Count; i++)
        {
            double v = observations[i].Value;
            if (v >= limits.Plus3s || v <= limits.Minus3s)
                return new ViolationResult(RuleName, ViolationType.RandomError, [i],
                    $"Observation at index {i} (value={v:F4}) exceeded \u00B13s. Probable random error. Run rejected.");
        }
        return null;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 2-2s REJECTION RULE
// Trigger: two consecutive observations both exceed +2s OR both exceed -2s.
// Error type: systematic error.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class Rule2_2s : IQcRule
{
    public string RuleName => "2-2s";
    public int MinimumObservations => 2;

    public ViolationResult? Evaluate(IReadOnlyList<QcObservation> observations, QcControlLimits limits)
    {
        for (int i = 0; i <= observations.Count - 2; i++)
        {
            double v1 = observations[i].Value;
            double v2 = observations[i + 1].Value;
            bool bothHigh = v1 >= limits.Plus2s  && v2 >= limits.Plus2s;
            bool bothLow  = v1 <= limits.Minus2s && v2 <= limits.Minus2s;
            if (bothHigh || bothLow)
                return new ViolationResult(RuleName, ViolationType.SystematicError, [i, i + 1],
                    $"Observations at indices {i} and {i+1} both exceeded {(bothHigh ? "+2s" : "-2s")}. Probable systematic error. Run rejected.");
        }
        return null;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// R-4s REJECTION RULE
// Trigger: range between two consecutive observations exceeds 4s (strict).
// Error type: random error.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class RuleR_4s : IQcRule
{
    public string RuleName => "R-4s";
    public int MinimumObservations => 2;

    public ViolationResult? Evaluate(IReadOnlyList<QcObservation> observations, QcControlLimits limits)
    {
        for (int i = 0; i <= observations.Count - 2; i++)
        {
            double range = Math.Abs(observations[i].Value - observations[i + 1].Value);
            if (range > 4 * limits.Sd)
                return new ViolationResult(RuleName, ViolationType.RandomError, [i, i + 1],
                    $"Range between indices {i} and {i+1} is {range:F4}, exceeding 4s ({4 * limits.Sd:F4}). Probable random error. Run rejected.");
        }
        return null;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 4-1s REJECTION RULE
// Trigger: four consecutive observations all exceed +1s OR all exceed -1s.
// Error type: systematic error.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class Rule4_1s : IQcRule
{
    public string RuleName => "4-1s";
    public int MinimumObservations => 4;

    public ViolationResult? Evaluate(IReadOnlyList<QcObservation> observations, QcControlLimits limits)
    {
        for (int i = 0; i <= observations.Count - 4; i++)
        {
            var w = observations.Skip(i).Take(4).Select(o => o.Value).ToList();
            bool allHigh = w.All(v => v > limits.Plus1s);
            bool allLow  = w.All(v => v < limits.Minus1s);
            if (allHigh || allLow)
                return new ViolationResult(RuleName, ViolationType.SystematicError,
                    [i, i + 1, i + 2, i + 3],
                    $"Four consecutive observations (indices {i}\u2013{i+3}) all exceeded {(allHigh ? "+1s" : "-1s")}. Probable systematic error. Run rejected.");
        }
        return null;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 10x̄ REJECTION RULE
// Trigger: ten consecutive observations all fall on the same side of the mean.
// Error type: systematic error.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class Rule10x : IQcRule
{
    public string RuleName => "10x\u0305";
    public int MinimumObservations => 10;

    public ViolationResult? Evaluate(IReadOnlyList<QcObservation> observations, QcControlLimits limits)
    {
        for (int i = 0; i <= observations.Count - 10; i++)
        {
            var w = observations.Skip(i).Take(10).Select(o => o.Value).ToList();
            bool allAbove = w.All(v => v > limits.Mean);
            bool allBelow = w.All(v => v < limits.Mean);
            if (allAbove || allBelow)
                return new ViolationResult(RuleName, ViolationType.SystematicError,
                    Enumerable.Range(i, 10).ToArray(),
                    $"Ten consecutive observations (indices {i}\u2013{i+9}) all fell {(allAbove ? "above" : "below")} the mean. Probable systematic error. Run rejected.");
        }
        return null;
    }
}
