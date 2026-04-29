using FluentAssertions;
using ClinStrata.QC.Engine.Engine;
using ClinStrata.QC.Engine.Models;
using Xunit;

namespace ClinStrata.QC.Engine.Tests;

/// <summary>
/// Sigma metric validation tests.
/// LC-MS/MS analyte values (voriconazole, posaconazole) sourced from
/// internal EP05/EP09 method performance data at a CLIA-regulated clinical laboratory.
/// General chemistry values cross-referenced against published benchmarks.
/// </summary>
public class SigmaCalculatorTests
{
    private readonly SigmaCalculator _calc = new();

    [Theory(DisplayName = "Sigma calculation — formula correctness and rule recommendation")]
    // General chemistry — published benchmark values
    [InlineData("glucose",      10.0, 1.5,  2.1,  4.05, SigmaRuleRecommendation.ThreeRuleSet)]
    [InlineData("creatinine",   15.0, 2.0,  3.5,  3.71, SigmaRuleRecommendation.FullMultiRule)]
    // Antifungal TDM — internal EP05/EP09 method performance data
    // Voriconazole: TEa=30% (no CLIA PT exists), bias=-9.8%, within-lab CV=5.0%
    [InlineData("voriconazole", 30.0, 9.8,  5.0,  4.04, SigmaRuleRecommendation.ThreeRuleSet)]
    // Posaconazole: TEa=30% (no CLIA PT exists), bias=-3.6%, within-lab CV=6.0%
    [InlineData("posaconazole", 30.0, 3.6,  6.0,  4.40, SigmaRuleRecommendation.ThreeRuleSet)]
    public void Calculate_CorrectSigmaAndRecommendation(
        string analyte, double tea, double bias, double cv,
        double expectedSigma, SigmaRuleRecommendation expectedRec)
    {
        var r = _calc.Calculate(biasPct: bias, cvPct: cv, teaPct: tea, analyte: analyte);
        r.Sigma.Should().BeApproximately(expectedSigma, precision: 0.1);
        r.Recommendation.Should().Be(expectedRec);
    }

    [Fact(DisplayName = "Sigma >= 6 → SingleRule recommendation")]
    public void Sigma6OrAbove_SingleRule()
    {
        var r = _calc.Calculate(biasPct: 0.5, cvPct: 1.0, teaPct: 10.0);
        r.Sigma.Should().BeGreaterThanOrEqualTo(6.0);
        r.Recommendation.Should().Be(SigmaRuleRecommendation.SingleRule);
    }

    [Fact(DisplayName = "Sigma < 3 → MethodReviewRequired")]
    public void SigmaBelow3_MethodReviewRequired()
    {
        var r = _calc.Calculate(biasPct: 4.0, cvPct: 3.5, teaPct: 10.0);
        r.Sigma.Should().BeLessThan(3.0);
        r.Recommendation.Should().Be(SigmaRuleRecommendation.MethodReviewRequired);
    }

    [Fact(DisplayName = "CLIA default TEa lookup — glucose returns 10.0%")]
    public void CliaTeaLookup_Glucose()
    {
        SigmaCalculator.GetCliaTeaDefault("glucose").Should().Be(10.0);
    }

    [Fact(DisplayName = "CLIA default TEa lookup — case-insensitive")]
    public void CliaTeaLookup_CaseInsensitive()
    {
        var t1 = SigmaCalculator.GetCliaTeaDefault("Voriconazole");
        var t2 = SigmaCalculator.GetCliaTeaDefault("VORICONAZOLE");
        t1.Should().Be(t2).And.NotBeNull();
    }

    [Fact(DisplayName = "Voriconazole — no CLIA PT exists, default TEa is conservative clinical estimate (30%)")]
    public void Voriconazole_DefaultTea_IsConservativeClinicalEstimate()
    {
        SigmaCalculator.GetCliaTeaDefault("voriconazole").Should().Be(30.0);
    }

    [Fact(DisplayName = "Unknown analyte with no explicit TEa — throws InvalidOperationException")]
    public void UnknownAnalyte_NoTea_Throws()
    {
        var act = () => _calc.Calculate(biasPct: 1.0, cvPct: 2.0, analyte: "unknown_ldt_xyz");
        act.Should().Throw<InvalidOperationException>().WithMessage("*No TEa provided*");
    }

    [Fact(DisplayName = "Bias sign is ignored — absolute value used in sigma calculation")]
    public void BiasSign_AbsoluteValueUsed()
    {
        var pos = _calc.Calculate(biasPct:  9.8, cvPct: 5.0, teaPct: 30.0);
        var neg = _calc.Calculate(biasPct: -9.8, cvPct: 5.0, teaPct: 30.0);
        pos.Sigma.Should().Be(neg.Sigma);
    }
}
