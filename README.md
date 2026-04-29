# ClinStrata QC Engine

**An open-source, CLSI EP23-aligned Westgard multi-rule QC engine and sigma metric calculator for clinical laboratory informatics.**

Built by a clinical laboratory scientist for clinical laboratory scientists. No Excel. No black boxes. Fully auditable rule logic you can document to CAP and CLIA.

[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com)

---

## The problem

Every CLIA-regulated laboratory implementing Westgard multi-rule QC is rebuilding the same logic in one of three ways — all problematic:

- **Spreadsheet workbooks**: unvalidated, rebuilt from scratch with every assay change, non-portable, non-auditable
- **Proprietary LIS modules**: rule logic is a black box — you cannot document to a CAP inspector exactly how a 2-2s violation is evaluated
- **Instrument manufacturer software**: instrument-specific, cannot aggregate QC across platforms

This library solves it: a clean, stateless, independently testable C#/.NET implementation of the six standard Westgard rules and the CLSI EP23-A sigma metric framework. Integrate it into your informatics system; it handles the math.

---

## What it implements

**Westgard multi-rules**

| Rule | N | Type | Detects |
|------|---|------|---------|
| 1-2s | 1 | Warning | Initiates multi-rule evaluation |
| 1-3s | 1 | Rejection | Random error |
| 2-2s | 2 | Rejection | Systematic error |
| R-4s | 2 | Rejection | Random error |
| 4-1s | 4 | Rejection | Systematic error |
| 10x\u0305 | 10 | Rejection | Systematic error |

Rules are evaluated sequentially per Westgard et al. (Clin Chem 1981). An exhaustive mode evaluates all rules and returns complete rule attribution for audit trail documentation.

**Sigma metric engine (CLSI EP23-A)**

\u03c3 = (TEa \u2212 |bias|) / CV

| \u03c3 | Recommended rule set |
|---|---|
| \u2265 6 | 1-3s only, N=2 |
| 4\u2013<6 | 1-3s / 2-2s / R-4s, N=2 |
| 3\u2013<4 | Full multi-rule, N=4 |
| < 3 | Method review required |

TEa defaults to CLIA PT acceptance criteria. Custom TEa is supported for LDTs and esoteric assays where CLIA PT criteria do not exist or are not clinically appropriate.

**Levey-Jennings output**

Structured data objects: mean, \u00b11s/2s/3s limits, per-observation violation flags with rule attribution. Chart rendering is delegated to your presentation layer.

---

## Quick start

```csharp
using ClinStrata.QC.Engine.Engine;
using ClinStrata.QC.Engine.Models;

var limits = new QcControlLimits { Mean = 100.0, Sd = 5.0 };

var observations = new List<QcObservation>
{
    new(Value: 111.0, RunId: "RUN-001"),
    new(Value: 111.5, RunId: "RUN-001")
};

var engine = new WestgardEngine();
var result = engine.Evaluate(observations, limits);

if (!result.IsAccepted)
{
    Console.WriteLine($"Run rejected: {result.PrimaryViolation!.RuleName}");
    Console.WriteLine(result.PrimaryViolation.Description);
    // Run rejected: 2-2s
    // Observations at indices 0 and 1 both exceeded +2s. Probable systematic error. Run rejected.
}
```

**Sigma metric**

```csharp
var calc = new SigmaCalculator();

// Using CLIA PT default TEa for cyclosporine (25%)
var sigma = calc.Calculate(biasPct: 3.0, cvPct: 4.5, analyte: "cyclosporine");
Console.WriteLine($"\u03c3 = {sigma.Sigma}");
Console.WriteLine(sigma.RecommendationDescription);

// Custom TEa for voriconazole LDT (no CLIA PT criteria exist)
var sigmaVori = calc.Calculate(
    biasPct: 9.8, cvPct: 5.0,
    teaPct: 30.0,
    analyte: "voriconazole",
    teaSource: "Conservative clinical estimate (therapeutic range 0.5\u20134 mg/L)");
```

**Levey-Jennings chart data**

```csharp
var builder = new LeveyJenningsBuilder();
var chart = builder.Build(observations, limits);

foreach (var point in chart.DataPoints)
{
    Console.WriteLine($"[{point.Index}] {point.Value:F2} \u2014 " +
        (point.IsViolation ? $"VIOLATION: {point.ViolatingRuleName}" : "OK"));
}
```

---

## Validation

The engine is validated against 24 deterministic test scenarios covering all implemented rules, boundary conditions, multi-rule combinations, and edge cases. All scenarios are cross-referenced against Westgard Web (Westgard QC, Inc.) and published worked examples from CLSI EP23-A.

```bash
dotnet test
```

The test file at `tests/ClinStrata.QC.Engine.Tests/WestgardValidationTests.cs` is the supplementary validation dataset referenced in the companion JALM technical note.

---

## Installation

```bash
dotnet add package ClinStrata.QC.Engine
```

Or clone directly:

```bash
git clone https://github.com/[PLACEHOLDER]/clinstrata-qc-engine
```

Requires .NET 9.0+. No external runtime dependencies.

---

## Limitations

**No CUSUM or EWMA.** These methods provide superior detection of small persistent systematic errors and are planned for a future release.

**TEa defaults are CLIA PT criteria.** For LDTs and esoteric assays where CLIA PT criteria do not exist or are not clinically appropriate, always provide a custom TEa. This is especially important for LC-MS/MS-based therapeutic drug monitoring analytes.

**Clinical judgment is required.** Sigma-based rule selection is evidence-based guidance, not a substitute for analytical and clinical judgment.

---

## Citation

If you use this engine in published work, please cite:

> Folagbayi JO. An open-source, CLSI EP23-aligned statistical quality control engine for clinical laboratory informatics: design, implementation, and validation. *Journal of Applied Laboratory Medicine*. [In submission, 2026].

---

## License

Apache 2.0. See [LICENSE](LICENSE).
# clinStrata-qc-engine
