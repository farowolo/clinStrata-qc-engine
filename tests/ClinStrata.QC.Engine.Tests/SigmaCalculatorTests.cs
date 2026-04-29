using FluentAssertions;
using ClinStrata.QC.Engine.Engine;
using ClinStrata.QC.Engine.Models;
using Xunit;

namespace ClinStrata.QC.Engine.Tests;

/// <summary>
/// Sigma metric validation tests against 2025 CLIA PT acceptance criteria.
/// Source: 42 CFR Part 493 (CMS-3355-F, effective July 11, 2024,
///         implemented by PT programs January 1, 2025).
///
/// LC-MS/MS TDM analyte values (voriconazole, posaconazole) are user-supplied —
/// no CLIA PT criteria exist for these analytes.
/// Internal EP05/EP09 method performance data from a CLIA-regulated clinical laboratory.
/// </summary>
public class SigmaCalculatorTests
{
    private readonly SigmaCalculator _calc = new();

    // ── 2025 CLIA CRITERIA — SIMPLE ──────────────────────────────────────────

    [Fact(DisplayName = "Albumin: 2025 CLIA ±8% (tightened from ±10%)")]
    public void Albumin_2025Criteria_8Pct()
    {
        var r = _calc.Calculate(biasPct: 1.0, cvPct: 2.0, analyte: "albumin");
        r.Tea.Should().Be(8.0);
        r.Criteria!.Type.Should().Be(CriteriaType.Simple);
    }

    [Fact(DisplayName = "Triglycerides: 2025 CLIA ±15% (tightened from ±25%)")]
    public void Triglycerides_2025Criteria_15Pct()
    {
        var r = _calc.Calculate(biasPct: 2.0, cvPct: 3.0, analyte: "triglycerides");
        r.Tea.Should().Be(15.0);
    }

    [Fact(DisplayName = "HbA1c: 2025 CLIA ±8% (new — no prior criteria)")]
    public void HbA1c_2025Criteria_NewEntry()
    {
        var r = _calc.Calculate(biasPct: 0.5, cvPct: 1.5, analyte: "hba1c");
        r.Tea.Should().Be(8.0);
        r.Criteria!.Type.Should().Be(CriteriaType.Simple);
    }

    [Fact(DisplayName = "BNP: 2025 CLIA ±30% (new — no prior criteria)")]
    public void BNP_2025Criteria_NewEntry()
    {
        var r = _calc.Calculate(biasPct: 5.0, cvPct: 8.0, analyte: "bnp");
        r.Tea.Should().Be(30.0);
    }

    [Fact(DisplayName = "Hemoglobin: 2025 CLIA ±4% (tightened from ±7%)")]
    public void Hemoglobin_2025Hematology()
    {
        var r = _calc.Calculate(biasPct: 0.5, cvPct: 1.0, analyte: "hemoglobin");
        r.Tea.Should().Be(4.0);
        r.Sigma.Should().BeApproximately(3.5, 0.1);
        r.Recommendation.Should().Be(SigmaRuleRecommendation.FullMultiRule);
    }

    [Fact(DisplayName = "Platelet count: 2025 CLIA ±25%")]
    public void Platelets_Hematology()
    {
        var r = _calc.Calculate(biasPct: 3.0, cvPct: 4.0, analyte: "platelets");
        r.Tea.Should().Be(25.0);
        r.Sigma.Should().BeApproximately(5.5, 0.1);
        r.Recommendation.Should().Be(SigmaRuleRecommendation.ThreeRuleSet);
    }

    // ── 2025 CLIA CRITERIA — COMPOSITE ───────────────────────────────────────

    [Theory(DisplayName = "Glucose: composite ±8% or ±6 mg/dL — TEa varies by concentration")]
    [InlineData(50.0,  12.0)]  // abs% = 6/50*100 = 12% > 8%  → TEa = 12%
    [InlineData(100.0,  8.0)]  // abs% = 6/100*100 = 6% < 8%  → TEa = 8%
    [InlineData(300.0,  8.0)]  // abs% = 6/300*100 = 2% < 8%  → TEa = 8%
    public void Glucose_CompositeTeaVariesByConcentration(double mean, double expectedTea)
    {
        var r = _calc.Calculate(biasPct: 1.0, cvPct: 2.0, analyte: "glucose", qcMean: mean);
        r.Tea.Should().BeApproximately(expectedTea, 0.1);
        r.Criteria!.Type.Should().Be(CriteriaType.Composite);
    }

    [Theory(DisplayName = "Creatinine: composite ±10% or ±0.2 mg/dL — TEa varies by concentration")]
    [InlineData(0.5,  40.0)]  // abs% = 0.2/0.5*100 = 40% > 10% → TEa = 40%
    [InlineData(1.0,  20.0)]  // abs% = 0.2/1.0*100 = 20% > 10% → TEa = 20%
    [InlineData(3.0,  10.0)]  // abs% = 0.2/3.0*100 = 6.7% < 10% → TEa = 10%
    public void Creatinine_CompositeConcentrationDependent(double mean, double expectedTea)
    {
        var r = _calc.Calculate(biasPct: 1.0, cvPct: 2.0, analyte: "creatinine", qcMean: mean);
        r.Tea.Should().BeApproximately(expectedTea, 0.5);
    }

    [Fact(DisplayName = "TSH: composite ±20% or ±0.2 mIU/L — fixed% dominates at higher concentrations")]
    public void TSH_CompositeHighConcentration()
    {
        // At TSH = 5 mIU/L: abs% = 0.2/5*100 = 4% < 20% → TEa = 20%
        var r = _calc.Calculate(biasPct: 2.0, cvPct: 3.0, analyte: "tsh", qcMean: 5.0);
        r.Tea.Should().Be(20.0);
    }

    [Fact(DisplayName = "TSH: composite ±20% or ±0.2 mIU/L — absolute dominates at very low TSH")]
    public void TSH_CompositeLowConcentration()
    {
        // At TSH = 0.5 mIU/L: abs% = 0.2/0.5*100 = 40% > 20% → TEa = 40%
        var r = _calc.Calculate(biasPct: 2.0, cvPct: 3.0, analyte: "tsh", qcMean: 0.5);
        r.Tea.Should().BeApproximately(40.0, 0.1);
    }

    [Fact(DisplayName = "Vancomycin: 2025 composite ±15% or ±2 mcg/mL — absolute dominates at low conc")]
    public void Vancomycin_CompositeAbsoluteDominatesLow()
    {
        // At mean 10 mcg/mL: abs% = 2/10*100 = 20% > 15% → TEa = 20%
        var r = _calc.Calculate(biasPct: 2.0, cvPct: 5.0, analyte: "vancomycin", qcMean: 10.0);
        r.Tea.Should().Be(20.0);
        r.Criteria!.Type.Should().Be(CriteriaType.Composite);
    }

    [Fact(DisplayName = "Phenytoin: 2025 composite ±15% or ±2 mcg/mL (tightened from ±25%)")]
    public void Phenytoin_2025TightenedCriteria()
    {
        // At mean 10 mcg/mL: abs% = 2/10*100 = 20% > 15% → TEa = 20%
        var r = _calc.Calculate(biasPct: 2.0, cvPct: 3.0, analyte: "phenytoin", qcMean: 10.0);
        r.Tea.Should().BeApproximately(20.0, 0.1);
        r.Tea.Should().BeLessThan(25.0, "2025 criteria are tighter than prior ±25%");
    }

    // ── 2025 CLIA CRITERIA — ABSOLUTE ────────────────────────────────────────

    [Fact(DisplayName = "Potassium: absolute ±0.3 mmol/L — TEa% calculated from qcMean")]
    public void Potassium_AbsoluteCriteria()
    {
        // At K = 4.0 mmol/L: TEa% = 0.3/4.0*100 = 7.5%
        var r = _calc.Calculate(biasPct: 0.5, cvPct: 1.5, analyte: "potassium", qcMean: 4.0);
        r.Tea.Should().BeApproximately(7.5, 0.1);
        r.Criteria!.Type.Should().Be(CriteriaType.AbsoluteOnly);
    }

    [Fact(DisplayName = "Sodium: absolute ±4 mmol/L — TEa% calculated from qcMean")]
    public void Sodium_AbsoluteCriteria()
    {
        // At Na = 140 mmol/L: TEa% = 4/140*100 = 2.86%
        var r = _calc.Calculate(biasPct: 0.5, cvPct: 0.8, analyte: "sodium", qcMean: 140.0);
        r.Tea.Should().BeApproximately(2.86, 0.1);
    }

    // ── LC-MS/MS TDM — USER-SUPPLIED TEa ────────────────────────────────────

    [Theory(DisplayName = "LC-MS/MS TDM — user-supplied TEa, no CLIA PT criteria")]
    [InlineData("voriconazole", 30.0, 9.8, 5.0,  4.04, SigmaRuleRecommendation.ThreeRuleSet)]
    [InlineData("posaconazole", 30.0, 3.6, 6.0,  4.40, SigmaRuleRecommendation.ThreeRuleSet)]
    [InlineData("cyclosporine", 25.0, 3.0, 4.5,  4.89, SigmaRuleRecommendation.ThreeRuleSet)]
    [InlineData("tacrolimus",   20.0, 4.0, 5.0,  3.20, SigmaRuleRecommendation.FullMultiRule)]
    public void LdtAnalytes_UserSuppliedTea(
        string analyte, double tea, double bias, double cv,
        double expectedSigma, SigmaRuleRecommendation expectedRec)
    {
        var r = _calc.Calculate(biasPct: bias, cvPct: cv, teaPct: tea, analyte: analyte,
            teaSource: "Clinical decision limit / conservative estimate");
        r.Sigma.Should().BeApproximately(expectedSigma, 0.1);
        r.Recommendation.Should().Be(expectedRec);
        r.Criteria.Should().BeNull("LDT analytes have no CLIA PT criteria");
    }

    [Fact(DisplayName = "Analyte with no CLIA criteria and no user TEa — throws InvalidOperationException")]
    public void NoCriteriaNoTea_Throws()
    {
        var act = () => _calc.Calculate(biasPct: 1.0, cvPct: 2.0, analyte: "ngal");
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*No TEa provided*");
    }

    // ── LOOKUP API ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "HasCliaCriteria — true for CLIA-regulated analytes")]
    public void HasCliaCriteria_KnownAnalytes()
    {
        SigmaCalculator.HasCliaCriteria("glucose").Should().BeTrue();
        SigmaCalculator.HasCliaCriteria("hemoglobin").Should().BeTrue();
        SigmaCalculator.HasCliaCriteria("tsh").Should().BeTrue();
        SigmaCalculator.HasCliaCriteria("phenytoin").Should().BeTrue();
        SigmaCalculator.HasCliaCriteria("hba1c").Should().BeTrue();
        SigmaCalculator.HasCliaCriteria("bnp").Should().BeTrue();
        SigmaCalculator.HasCliaCriteria("platelets").Should().BeTrue();
    }

    [Fact(DisplayName = "HasCliaCriteria — false for LDT analytes without CLIA PT criteria")]
    public void HasCliaCriteria_LdtAnalytes_False()
    {
        SigmaCalculator.HasCliaCriteria("voriconazole").Should().BeFalse();
        SigmaCalculator.HasCliaCriteria("posaconazole").Should().BeFalse();
        SigmaCalculator.HasCliaCriteria("cyclosporine").Should().BeFalse();
        SigmaCalculator.HasCliaCriteria("tacrolimus").Should().BeFalse();
        SigmaCalculator.HasCliaCriteria("ngal").Should().BeFalse();
    }

    [Fact(DisplayName = "Lookup is case-insensitive")]
    public void Lookup_CaseInsensitive()
    {
        SigmaCalculator.HasCliaCriteria("Glucose").Should().BeTrue();
        SigmaCalculator.HasCliaCriteria("GLUCOSE").Should().BeTrue();
        SigmaCalculator.HasCliaCriteria("HbA1c").Should().BeTrue();
        SigmaCalculator.HasCliaCriteria("HEMATOCRIT").Should().BeTrue();
        SigmaCalculator.HasCliaCriteria("Vancomycin").Should().BeTrue();
    }

    [Fact(DisplayName = "Sigma >= 6 → SingleRule")]
    public void SigmaAbove6_SingleRule()
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

    [Fact(DisplayName = "Bias sign is ignored — absolute value used")]
    public void BiasSign_AbsoluteValueUsed()
    {
        var pos = _calc.Calculate(biasPct:  9.8, cvPct: 5.0, teaPct: 30.0);
        var neg = _calc.Calculate(biasPct: -9.8, cvPct: 5.0, teaPct: 30.0);
        pos.Sigma.Should().Be(neg.Sigma);
    }

    [Fact(DisplayName = "Zero CV — throws ArgumentOutOfRangeException")]
    public void ZeroCv_Throws()
    {
        var act = () => _calc.Calculate(biasPct: 1.0, cvPct: 0.0, teaPct: 10.0);
        act.Should().Throw<ArgumentOutOfRangeException>()
           .WithMessage("*CV must be greater than zero*");
    }
}
