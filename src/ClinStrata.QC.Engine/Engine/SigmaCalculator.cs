using ClinStrata.QC.Engine.Models;

namespace ClinStrata.QC.Engine.Engine;

/// <summary>
/// Calculates the sigma metric per CLSI EP23-A.
/// Formula: σ = (TEa − |bias|) / CV
///
/// TEa defaults to CLIA PT acceptance criteria (42 CFR §493).
/// Provide a custom teaPct for LDTs and esoteric assays where
/// CLIA PT criteria do not exist or are not clinically appropriate.
/// </summary>
public sealed class SigmaCalculator
{
    private static readonly Dictionary<string, double> CliaTeaDefaults =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["glucose"]              = 10.0,
        ["creatinine"]           = 15.0,
        ["sodium"]               = 4.0,
        ["potassium"]            = 8.0,
        ["chloride"]             = 5.0,
        ["bicarbonate"]          = 10.0,
        ["bun"]                  = 9.0,
        ["calcium"]              = 10.0,
        ["total protein"]        = 10.0,
        ["albumin"]              = 10.0,
        ["alt"]                  = 20.0,
        ["ast"]                  = 20.0,
        ["alkaline phosphatase"] = 30.0,
        ["total bilirubin"]      = 20.0,
        ["cholesterol"]          = 10.0,
        ["triglycerides"]        = 25.0,
        ["hdl"]                  = 30.0,
        ["ldl"]                  = 30.0,
        ["tsh"]                  = 3.0,
        ["cyclosporine"]         = 25.0,
        ["tacrolimus"]           = 20.0,
        ["sirolimus"]            = 20.0,
        ["mycophenolic acid"]    = 25.0,
        // No CLIA PT criteria exist for antifungal TDM analytes.
        // Conservative clinical estimates based on therapeutic range width.
        ["voriconazole"]         = 30.0,
        ["posaconazole"]         = 30.0,
        ["vancomycin"]           = 20.0,
        ["gentamicin"]           = 20.0,
        ["phenytoin"]            = 25.0,
        ["valproic acid"]        = 25.0,
        ["carbamazepine"]        = 25.0,
        ["lithium"]              = 20.0,
    };

    /// <summary>
    /// Calculate the sigma metric for an analyte.
    /// </summary>
    /// <param name="biasPct">Method bias as percent of target. Sign is ignored; absolute value is used.</param>
    /// <param name="cvPct">Between-day (within-lab) CV (%) from EP05 precision study.</param>
    /// <param name="teaPct">Total allowable error (%). If null, looks up CLIA PT default by analyte name.</param>
    /// <param name="analyte">Analyte name for TEa lookup and documentation.</param>
    /// <param name="teaSource">Description of TEa source for documentation.</param>
    public SigmaResult Calculate(
        double biasPct,
        double cvPct,
        double? teaPct = null,
        string? analyte = null,
        string? teaSource = null)
    {
        if (cvPct <= 0)
            throw new ArgumentOutOfRangeException(nameof(cvPct),
                "CV must be greater than zero. Division by zero would occur.");

        double tea;
        string resolvedSource;

        if (teaPct.HasValue)
        {
            if (teaPct.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(teaPct), "TEa must be greater than zero.");
            tea = teaPct.Value;
            resolvedSource = teaSource ?? "User-specified";
        }
        else if (analyte is not null && CliaTeaDefaults.TryGetValue(analyte, out double def))
        {
            tea = def;
            resolvedSource = "CLIA PT acceptance criteria (42 CFR \u00a7493)";
        }
        else
        {
            throw new InvalidOperationException(
                $"No TEa provided and no CLIA PT default found for analyte '{analyte ?? "(null)"}'. " +
                "Provide an explicit teaPct — especially for LDTs and esoteric assays.");
        }

        double sigma = (tea - Math.Abs(biasPct)) / cvPct;

        var rec = sigma switch
        {
            >= 6.0 => SigmaRuleRecommendation.SingleRule,
            >= 4.0 => SigmaRuleRecommendation.ThreeRuleSet,
            >= 3.0 => SigmaRuleRecommendation.FullMultiRule,
            _      => SigmaRuleRecommendation.MethodReviewRequired
        };

        return new SigmaResult
        {
            Tea            = tea,
            AbsBias        = Math.Abs(biasPct),
            Cv             = cvPct,
            Sigma          = Math.Round(sigma, 2),
            Recommendation = rec,
            TeaSource      = resolvedSource,
            Analyte        = analyte
        };
    }

    public static double? GetCliaTeaDefault(string analyte) =>
        CliaTeaDefaults.TryGetValue(analyte, out var t) ? t : null;
}
