namespace ClinStrata.QC.Engine.Models;

/// <summary>
/// Describes the structural type of a CLIA PT acceptance criterion.
/// </summary>
public enum CriteriaType
{
    /// <summary>TV ± X% only. TEa is concentration-independent.</summary>
    Simple,

    /// <summary>
    /// TV ± X units OR ± Y%, whichever is greater.
    /// TEa% is concentration-dependent — qcMean is required for correct calculation.
    /// </summary>
    Composite,

    /// <summary>
    /// TV ± X units only (no percentage component).
    /// Examples: pH ± 0.04, Calcium ± 1.0 mg/dL, Sodium ± 4 mmol/L.
    /// TEa% can only be calculated when qcMean is provided.
    /// </summary>
    AbsoluteOnly
}

/// <summary>
/// Represents a single CLIA 2025 PT acceptance criterion for sigma metric calculation.
/// Source: 42 CFR Part 493 Subpart I (CMS-3355-F, effective July 11, 2024).
/// </summary>
public record CliaTeaCriteria(
    string AnalyteName,
    double FixedPct,
    double? AbsoluteValue,
    string? AbsoluteUnit,
    CriteriaType Type
)
{
    private const string RegSource = "2025 CLIA PT criteria (42 CFR §493, CMS-3355-F)";
    public string Source      => RegSource;
    public string EffectiveDate => "2025-01-01";

    /// <summary>
    /// Calculates TEa% at the specified QC material mean concentration.
    ///
    /// For Simple criteria: returns FixedPct regardless of qcMean.
    ///
    /// For Composite criteria: returns max(FixedPct, AbsoluteValue / |qcMean| × 100).
    /// If qcMean is not provided, returns FixedPct with a documentation flag
    /// that the result may underestimate TEa at low concentrations.
    ///
    /// For AbsoluteOnly criteria: requires qcMean to produce a meaningful TEa%.
    /// Returns 0 if qcMean is null or zero (caller must handle this case).
    /// </summary>
    public double CalculateTeaPct(double? qcMean = null)
    {
        return Type switch
        {
            CriteriaType.Simple =>
                FixedPct,

            CriteriaType.Composite when AbsoluteValue.HasValue && qcMean is > 0 =>
                Math.Max(FixedPct, AbsoluteValue.Value / Math.Abs(qcMean.Value) * 100.0),

            CriteriaType.Composite =>
                // qcMean not provided — return fixed percentage with documentation flag
                FixedPct,

            CriteriaType.AbsoluteOnly when AbsoluteValue.HasValue && qcMean is > 0 =>
                AbsoluteValue.Value / Math.Abs(qcMean.Value) * 100.0,

            _ => 0.0
        };
    }

    /// <summary>
    /// True when the criterion has an absolute component and qcMean is
    /// needed for a precise TEa% calculation.
    /// </summary>
    public bool RequiresQcMean =>
        Type is CriteriaType.Composite or CriteriaType.AbsoluteOnly;

    /// <summary>Human-readable description of the criterion.</summary>
    public string Description => Type switch
    {
        CriteriaType.Simple =>
            $"TV \u00b1{FixedPct}%",
        CriteriaType.Composite =>
            $"TV \u00b1{FixedPct}% or \u00b1{AbsoluteValue} {AbsoluteUnit} (greater)",
        CriteriaType.AbsoluteOnly =>
            $"TV \u00b1{AbsoluteValue} {AbsoluteUnit}",
        _ => "Unknown"
    };
}
