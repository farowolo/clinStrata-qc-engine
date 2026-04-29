using FluentAssertions;
using ClinStrata.QC.Engine.Engine;
using ClinStrata.QC.Engine.Models;
using Xunit;

namespace ClinStrata.QC.Engine.Tests;

/// <summary>
/// Deterministic validation suite for the ClinStrata Westgard QC Engine.
///
/// 24 scenarios covering:
///   S-01–S-02:  True negatives
///   S-03, S-17: 1-2s warning only, no rejection
///   S-04–S-05:  1-3s rejection
///   S-06:       2-2s rejection
///   S-07–S-08, S-18–S-19: R-4s rejection and boundary
///   S-09–S-11:  4-1s rejection
///   S-12–S-14:  10x̄ rejection
///   S-15–S-16:  Sequential multi-rule combinations
///   S-20–S-21:  Edge cases (streak broken, insufficient N)
///   S-22:       Exhaustive evaluation mode
///   S-23:       Custom TEa override
///   S-24:       Zero CV guard
///
/// Expected outcomes cross-referenced against:
///   Westgard Web (Westgard QC, Inc.)
///   CLSI EP23-A worked examples
///   Westgard JO et al., Clin Chem. 1981;27(3):493-501
/// </summary>
public class WestgardValidationTests
{
    private static readonly QcControlLimits Limits = new() { Mean = 100.0, Sd = 5.0 };
    private static readonly WestgardEngine Engine = new();
    private static List<QcObservation> Obs(params double[] vals) =>
        vals.Select(v => new QcObservation(v)).ToList();

    // ── TRUE NEGATIVES ────────────────────────────────────────────────────────

    [Fact(DisplayName = "S-01: All values within ±1s — no violation, run accepted")]
    public void S01_AllWithin1s_TrueNegative()
    {
        var r = Engine.Evaluate(Obs(101.0, 99.5), Limits);
        r.IsAccepted.Should().BeTrue();
        r.WarningTriggered.Should().BeFalse();
        r.PrimaryViolation.Should().BeNull();
    }

    [Fact(DisplayName = "S-02: Values at ±1.8s (below 1-2s threshold) — no violation")]
    public void S02_Below2sThreshold_TrueNegative()
    {
        var r = Engine.Evaluate(Obs(109.0, 91.0), Limits);
        r.IsAccepted.Should().BeTrue();
        r.WarningTriggered.Should().BeFalse();
    }

    // ── 1-2s WARNING ONLY ────────────────────────────────────────────────────

    [Fact(DisplayName = "S-03: One value at +2.1s — warning triggered, run not rejected")]
    public void S03_Plus21s_WarningOnly()
    {
        var r = Engine.Evaluate(Obs(110.5, 99.0), Limits);
        r.WarningTriggered.Should().BeTrue();
        r.IsAccepted.Should().BeTrue("1-2s alone does not reject");
        r.PrimaryViolation.Should().BeNull();
    }

    [Fact(DisplayName = "S-17: 1-2s warning with no subsequent rejection rule — accepted with warning")]
    public void S17_WarningNoRejection_AcceptedWithWarning()
    {
        var r = Engine.Evaluate(Obs(111.0, 100.5), Limits);
        r.WarningTriggered.Should().BeTrue();
        r.IsAccepted.Should().BeTrue();
        r.PrimaryViolation.Should().BeNull();
    }

    // ── 1-3s REJECTION ───────────────────────────────────────────────────────

    [Fact(DisplayName = "S-04: One value at +3.1s — 1-3s rejection, random error")]
    public void S04_Plus31s_Rejection1_3s()
    {
        var r = Engine.Evaluate(Obs(115.5, 98.0), Limits);
        r.IsAccepted.Should().BeFalse();
        r.PrimaryViolation!.RuleName.Should().Be("1-3s");
        r.PrimaryViolation.ViolationType.Should().Be(ViolationType.RandomError);
    }

    [Fact(DisplayName = "S-05: One value at exactly -3.0s (inclusive boundary) — 1-3s rejection")]
    public void S05_ExactlyMinus3s_BoundaryInclusive()
    {
        var r = Engine.Evaluate(Obs(85.0, 101.0), Limits);
        r.IsAccepted.Should().BeFalse();
        r.PrimaryViolation!.RuleName.Should().Be("1-3s");
    }

    // ── 2-2s REJECTION ───────────────────────────────────────────────────────

    [Fact(DisplayName = "S-06: Two consecutive values at +2.2s — 2-2s rejection, systematic error")]
    public void S06_TwoConsecutivePlus22s_Rejection2_2s()
    {
        var r = Engine.Evaluate(Obs(111.0, 111.0), Limits);
        r.IsAccepted.Should().BeFalse();
        r.PrimaryViolation!.RuleName.Should().Be("2-2s");
        r.PrimaryViolation.ViolationType.Should().Be(ViolationType.SystematicError);
    }

    // ── R-4s REJECTION ───────────────────────────────────────────────────────

    [Fact(DisplayName = "S-07: +2.2s and -2.1s on opposite sides — R-4s fires, not 2-2s")]
    public void S07_OppositeSides_R4sFires()
    {
        var r = Engine.Evaluate(Obs(111.0, 89.5), Limits);
        r.IsAccepted.Should().BeFalse();
        r.PrimaryViolation!.RuleName.Should().Be("R-4s");
    }

    [Fact(DisplayName = "S-08: Range = 4.2s — R-4s rejection")]
    public void S08_Range42s_R4sRejection()
    {
        var r = Engine.Evaluate(Obs(110.5, 89.5), Limits);
        r.IsAccepted.Should().BeFalse();
        r.PrimaryViolation!.RuleName.Should().Be("R-4s");
        r.PrimaryViolation.ViolationType.Should().Be(ViolationType.RandomError);
    }

    [Fact(DisplayName = "S-18: Range exactly 4.0s — R-4s does NOT fire (strict boundary)")]
    public void S18_RangeExactly4s_NoBoundaryViolation()
    {
        // +2s=110.0, -2s=90.0 → range=20.0 which is NOT > 20.0
        var r = Engine.Evaluate(Obs(110.0, 90.0), Limits);
        r.PrimaryViolation?.RuleName.Should().NotBe("R-4s");
    }

    [Fact(DisplayName = "S-19: Range = 4.01s (just over boundary) — R-4s rejection")]
    public void S19_Range401s_JustOver_R4sRejection()
    {
        var r = Engine.Evaluate(Obs(110.0, 89.95), Limits);
        r.IsAccepted.Should().BeFalse();
        r.PrimaryViolation!.RuleName.Should().Be("R-4s");
    }

    // ── 4-1s REJECTION ───────────────────────────────────────────────────────

    [Fact(DisplayName = "S-09: Four consecutive values above +1s — 4-1s rejection")]
    public void S09_FourAbovePlus1s_Rejection4_1s()
    {
        var r = Engine.Evaluate(Obs(106.0, 107.0, 105.5, 108.0), Limits);
        r.IsAccepted.Should().BeFalse();
        r.PrimaryViolation!.RuleName.Should().Be("4-1s");
        r.PrimaryViolation.ViolationType.Should().Be(ViolationType.SystematicError);
    }

    [Fact(DisplayName = "S-10: Four consecutive values below -1s — 4-1s rejection")]
    public void S10_FourBelowMinus1s_Rejection4_1s()
    {
        var r = Engine.Evaluate(Obs(94.0, 93.0, 94.5, 92.0), Limits);
        r.IsAccepted.Should().BeFalse();
        r.PrimaryViolation!.RuleName.Should().Be("4-1s");
    }

    [Fact(DisplayName = "S-11: Three above +1s then one at -0.5s — 4-1s does NOT fire (streak broken)")]
    public void S11_StreakBrokenAtThree_No4_1s()
    {
        var r = Engine.Evaluate(Obs(106.0, 107.0, 105.5, 97.5), Limits);
        r.PrimaryViolation?.RuleName.Should().NotBe("4-1s");
    }

    // ── 10x̄ REJECTION ────────────────────────────────────────────────────────

    [Fact(DisplayName = "S-12: Ten consecutive values above mean — 10x̄ rejection")]
    public void S12_TenAboveMean_Rejection10x()
    {
        var r = Engine.Evaluate(Obs(101,102,100.5,103,101.5,104,100.1,102.5,101.2,100.8), Limits);
        r.IsAccepted.Should().BeFalse();
        r.PrimaryViolation!.RuleName.Should().Be("10x\u0305");
        r.PrimaryViolation.ViolationType.Should().Be(ViolationType.SystematicError);
    }

    [Fact(DisplayName = "S-13: Ten consecutive values below mean — 10x̄ rejection")]
    public void S13_TenBelowMean_Rejection10x()
    {
        var r = Engine.Evaluate(Obs(99,98,99.5,97,98.5,96,99.9,97.5,98.8,99.2), Limits);
        r.IsAccepted.Should().BeFalse();
        r.PrimaryViolation!.RuleName.Should().Be("10x\u0305");
    }

    [Fact(DisplayName = "S-14: Nine above, one below mean — 10x̄ does NOT fire (streak broken)")]
    public void S14_NineAboveOneBelowMean_No10x()
    {
        var r = Engine.Evaluate(Obs(101,102,100.5,103,101.5,104,100.1,102.5,101.2,99.5), Limits);
        r.PrimaryViolation?.RuleName.Should().NotBe("10x\u0305");
    }

    // ── SEQUENTIAL MULTI-RULE COMBINATIONS ───────────────────────────────────

    [Fact(DisplayName = "S-15: 1-2s warning then 1-3s — 1-3s rejection (first rejection rule wins)")]
    public void S15_WarningThen1_3s_Rejection1_3s()
    {
        var r = Engine.Evaluate(Obs(111.0, 115.5), Limits);
        r.IsAccepted.Should().BeFalse();
        r.PrimaryViolation!.RuleName.Should().Be("1-3s");
    }

    [Fact(DisplayName = "S-16: 1-2s warning with two consecutive above +2s — 2-2s fires first")]
    public void S16_WarningWith2_2s_TwoTwoSFires()
    {
        var r = Engine.Evaluate(Obs(111.0, 111.5), Limits);
        r.IsAccepted.Should().BeFalse();
        r.PrimaryViolation!.RuleName.Should().Be("2-2s");
    }

    // ── EDGE CASES ────────────────────────────────────────────────────────────

    [Fact(DisplayName = "S-20: Ten observations with one below mean mid-stream — 10x̄ does NOT fire")]
    public void S20_TenWithOneBelowMeanMidStream_No10x()
    {
        var r = Engine.Evaluate(Obs(101,102,103,101.5,99.0,101,102,100.5,101.2,100.8), Limits);
        r.PrimaryViolation?.RuleName.Should().NotBe("10x\u0305");
    }

    [Fact(DisplayName = "S-21: Only 3 observations — 4-1s skipped gracefully (insufficient N)")]
    public void S21_InsufficientNFor4_1s_RuleSkipped()
    {
        var act = () => Engine.Evaluate(Obs(106.0, 107.0, 105.5), Limits);
        act.Should().NotThrow();
        var r = Engine.Evaluate(Obs(106.0, 107.0, 105.5), Limits);
        r.PrimaryViolation?.RuleName.Should().NotBe("4-1s");
    }

    // ── EXHAUSTIVE MODE ───────────────────────────────────────────────────────

    [Fact(DisplayName = "S-22: Exhaustive mode — all applicable violations returned")]
    public void S22_ExhaustiveMode_AllViolationsReturned()
    {
        var r = Engine.Evaluate(Obs(111.0, 111.5), Limits, EvaluationMode.Exhaustive);
        r.IsAccepted.Should().BeFalse();
        r.AllViolations.Should().Contain(v => v.RuleName == "2-2s");
        r.AllViolations.Should().Contain(v => v.RuleName == "1-2s");
        r.AllViolations.Count.Should().BeGreaterThan(1,
            "exhaustive mode returns all triggered violations, not just the first");
    }

    // ── CUSTOM TEa ────────────────────────────────────────────────────────────

    [Fact(DisplayName = "S-23: Custom TEa overrides CLIA default — sigma calculated with provided value")]
    public void S23_CustomTea_OverridesCliaDefault()
    {
        var calc = new SigmaCalculator();
        var r = calc.Calculate(biasPct: 1.5, cvPct: 2.0, teaPct: 6.0,
            analyte: "glucose", teaSource: "Clinical decision limit");

        r.Tea.Should().Be(6.0, "custom TEa must override CLIA default");
        r.TeaSource.Should().Be("Clinical decision limit");
        r.Sigma.Should().Be(Math.Round((6.0 - 1.5) / 2.0, 2));
    }

    // ── ZERO CV GUARD ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "S-24: Zero CV — SigmaCalculator throws ArgumentOutOfRangeException")]
    public void S24_ZeroCv_Throws()
    {
        var calc = new SigmaCalculator();
        var act = () => calc.Calculate(biasPct: 1.0, cvPct: 0.0, teaPct: 10.0);
        act.Should().Throw<ArgumentOutOfRangeException>()
           .WithMessage("*CV must be greater than zero*");
    }
}
