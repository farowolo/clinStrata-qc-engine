using ClinStrata.QC.Engine.Engine;
using ClinStrata.QC.Engine.Models;

namespace ClinStrata.QC.Engine.Output;

/// <summary>
/// Builds structured Levey-Jennings output from an ordered series of QC observations.
/// Each observation is evaluated in a sliding window so multi-observation rules are correctly attributed.
/// </summary>
public sealed class LeveyJenningsBuilder
{
    private readonly WestgardEngine _engine = new();

    public LeveyJenningsOutput Build(
        IReadOnlyList<QcObservation> observations,
        QcControlLimits limits,
        int windowSize = 10)
    {
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();

        var points = new List<LeveyJenningsDataPoint>(observations.Count);

        for (int i = 0; i < observations.Count; i++)
        {
            int start = Math.Max(0, i - windowSize + 1);
            var window = observations.Skip(start).Take(i - start + 1).ToList();
            var result = _engine.Evaluate(window, limits, EvaluationMode.Exhaustive);

            int localIndex = window.Count - 1;
            var violation = result.AllViolations
                .Where(v => v.IsRejection)
                .FirstOrDefault(v => v.TriggeringIndices.Contains(localIndex));
            var warn = !result.AllViolations.Any(v => v.IsRejection)
                ? result.AllViolations.FirstOrDefault(v =>
                    v.ViolationType == ViolationType.Warning &&
                    v.TriggeringIndices.Contains(localIndex))
                : null;

            var active = violation ?? warn;

            points.Add(new LeveyJenningsDataPoint(
                Index: i,
                Value: observations[i].Value,
                IsViolation: active is not null,
                ViolatingRuleName: active?.RuleName,
                ViolationType: active?.ViolationType ?? ViolationType.None,
                RunId: observations[i].RunId,
                Timestamp: observations[i].Timestamp
            ));
        }

        return new LeveyJenningsOutput
        {
            Mean    = limits.Mean,
            Plus1s  = limits.Plus1s,  Minus1s = limits.Minus1s,
            Plus2s  = limits.Plus2s,  Minus2s = limits.Minus2s,
            Plus3s  = limits.Plus3s,  Minus3s = limits.Minus3s,
            DataPoints = points.AsReadOnly()
        };
    }
}
