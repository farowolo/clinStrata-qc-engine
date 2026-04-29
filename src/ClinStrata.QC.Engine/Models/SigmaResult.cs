namespace ClinStrata.QC.Engine.Models;

public enum SigmaRuleRecommendation
{
    MethodReviewRequired,
    FullMultiRule,
    ThreeRuleSet,
    SingleRule
}

/// <summary>
/// Result of a sigma metric calculation per CLSI EP23-A.
/// sigma = (TEa - |bias|) / CV
/// </summary>
public record SigmaResult
{
    public required double Tea      { get; init; }
    public required double AbsBias  { get; init; }
    public required double Cv       { get; init; }
    public required double Sigma    { get; init; }
    public required SigmaRuleRecommendation Recommendation { get; init; }
    public string? TeaSource  { get; init; }
    public string? Analyte    { get; init; }

    public string RecommendationDescription => Recommendation switch
    {
        SigmaRuleRecommendation.MethodReviewRequired =>
            "Sigma < 3: Method performance requires review. Bias and/or imprecision exceed allowable error thresholds. Do not finalize QC plan without method remediation.",
        SigmaRuleRecommendation.FullMultiRule =>
            "Sigma 3\u2013<4: Apply full Westgard multi-rule set (1-2s / 1-3s / 2-2s / R-4s / 4-1s / 10x\u0305) with N=4.",
        SigmaRuleRecommendation.ThreeRuleSet =>
            "Sigma 4\u2013<6: Apply three-rule set (1-3s / 2-2s / R-4s) with N=2.",
        SigmaRuleRecommendation.SingleRule =>
            "Sigma \u22656: Single rule sufficient (1-3s with N=2). Consider patient-based real-time QC as a complement.",
        _ => "Unknown recommendation."
    };
}
