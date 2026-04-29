# ClinStrata QC Engine

**An open-source, CLSI EP23-aligned Westgard multi-rule QC engine and sigma metric calculator for clinical laboratory informatics.**

Built by a clinical laboratory scientist for clinical laboratory scientists. No Excel. No black boxes. Fully auditable rule logic you can document to CAP and CLIA.

[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com)

---

## The problem

Every CLIA-regulated laboratory implementing Westgard multi-rule QC is rebuilding the same logic in one of four ways — all with serious limitations:

- **Spreadsheet workbooks** — unvalidated, rebuilt from scratch with every assay change, non-portable, and impossible to audit
- **Proprietary LIS modules** — rule logic is a black box; you cannot document to a CAP inspector exactly how a 2-2s violation is evaluated
- **Instrument manufacturer software** — instrument-specific and cannot aggregate QC performance across platforms
- **Cloud-hosted QC management platforms** — peer group models assume commercial reagent lots with established interlaboratory populations; limited applicability to LC-MS/MS LDTs where no peer group exists and TEa must be defined independently

This library fills that gap: a clean, stateless, independently testable C#/.NET implementation of the six standard Westgard rules and the CLSI EP23-A sigma metric framework. Integrate it into your informatics system and it handles the math — transparently, auditably, and portably.

---

## What it implements

### Westgard multi-rules

| Rule | N | Type | Detects |
|------|---|------|---------|
| 1-2s | 1 | Warning | Initiates multi-rule evaluation |
| 1-3s | 1 | Rejection | Random error |
| 2-2s | 2 | Rejection | Systematic error |
| R-4s | 2 | Rejection | Random error |
| 4-1s | 4 | Rejection | Systematic error |
| 10x̄ | 10 | Rejection | Systematic error |

The 1-2s warning sets a flag but does not gate rejection rule evaluation — all six rules are evaluated on every call, consistent with the original Westgard specification (Clin Chem 1981). An exhaustive mode returns complete rule attribution across all triggered violations for audit trail documentation.

### Sigma metric engine (CLSI EP23-A)

σ = (TEa − |bias|) / CV

| σ | Recommended rule set |
|---|---|
| ≥ 6 | 1-3s only, N=2 |
| 4–<6 | 1-3s / 2-2s / R-4s, N=2 |
| 3–<4 | Full multi-rule, N=4 |
| < 3 | Method review required |

TEa defaults to CLIA PT acceptance criteria. Custom TEa is fully supported for LDTs and esoteric assays where CLIA PT criteria do not exist or are not clinically appropriate — especially relevant for LC-MS/MS-based therapeutic drug monitoring analytes.

### Levey-Jennings output

Structured data objects containing: mean, ±1s/2s/3s control limits, per-observation values with run identifiers, and violation flags with rule attribution. Chart rendering is delegated to the consuming system's presentation layer, enabling integration across web, desktop, and report-generation contexts without modifying the validated engine core.

---

## Quick start

### Run evaluation

```csharp
using ClinStrata.QC.Engine.Engine;
using ClinStrata.QC.Engine.Models;

// Define control limits from your established mean and SD
var limits = new QcControlLimits { Mean = 100.0, Sd = 5.0 };

// Build your observations for the run
var observations = new List<QcObservation>
{
    new(Value: 111.0, RunId: "RUN-001"),
    new(Value: 111.5, RunId: "RUN-001")
};

// Evaluate
var engine = new WestgardEngine();
var result = engine.Evaluate(observations, limits);

if (!result.IsAccepted)
{
    Console.WriteLine($"Run rejected: {result.PrimaryViolation!.RuleName}");
    Console.WriteLine(result.PrimaryViolation.Description);
    // Run rejected: 2-2s
    // Observations at indices 0 and 1 both exceeded +2s. Probable systematic error. Run rejected.
}

if (result.WarningTriggered && result.IsAccepted)
{
    Console.WriteLine("1-2s warning — monitor closely but run is accepted.");
}
```

### Calculate sigma metric

```csharp
var calc = new SigmaCalculator();

// CLIA PT default TEa for cyclosporine (25%)
var sigma = calc.Calculate(biasPct: 3.0, cvPct: 4.5, analyte: "cyclosporine");
Console.WriteLine($"σ = {sigma.Sigma}");
Console.WriteLine(sigma.RecommendationDescription);

// Custom TEa for voriconazole LDT (no CLIA PT criteria exist)
var sigmaVori = calc.Calculate(
    biasPct: 9.8,
    cvPct: 5.0,
    teaPct: 30.0,
    analyte: "voriconazole",
    teaSource: "Conservative clinical estimate (therapeutic range 0.5–4 mg/L)");

Console.WriteLine($"σ = {sigmaVori.Sigma}"); // 4.04 → Three-rule set
```

### Generate Levey-Jennings chart data

```csharp
var builder = new LeveyJenningsBuilder();
var chart = builder.Build(observations, limits);

// Pass chart.Mean, chart.Plus2s, etc. to your chart rendering layer
foreach (var point in chart.DataPoints)
{
    Console.WriteLine($"[{point.Index}] {point.Value:F2} — " +
        (point.IsViolation ? $"VIOLATION: {point.ViolatingRuleName}" : "OK"));
}
```

### Exhaustive mode for audit trail documentation

```csharp
// Returns all violations detected, not just the first rejection
var result = engine.Evaluate(observations, limits, EvaluationMode.Exhaustive);

foreach (var violation in result.AllViolations)
{
    Console.WriteLine($"{violation.RuleName}: {violation.Description}");
}
```

---

## Validation

The engine is validated against 24 deterministic test scenarios covering all implemented rules, boundary conditions, multi-rule combinations, and edge cases. All scenarios are cross-referenced against Westgard Web (Westgard QC, Inc.) and published worked examples from CLSI EP23-A.

```bash
dotnet test
# 35 tests — 24 Westgard scenarios + sigma calculator suite
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

**No CUSUM or EWMA.** These methods provide superior detection of small persistent systematic errors and are planned for a future release. Westgard multi-rules can miss slow progressive shifts in method performance that CUSUM or EWMA would flag.

**TEa defaults to CLIA PT criteria.** For LDTs and esoteric assays where CLIA PT criteria do not exist or are not clinically appropriate, always specify a custom TEa. This is especially important for LC-MS/MS-based therapeutic drug monitoring analytes such as antifungals and immunosuppressants.

**Stateless by design.** The engine evaluates discrete observation windows and does not maintain running statistics between calls. Accumulation of inter-run trending data is the responsibility of the consuming informatics system.

**Clinical judgment is always required.** Sigma-based rule selection is evidence-based guidance, not a prescriptive mandate. QC plan finalization requires a qualified laboratory professional to assess the clinical and analytical context.

---

## How this relates to Bio-Rad Unity

Bio-Rad Unity is the most widely deployed QC management platform in clinical laboratories and provides valuable interlaboratory peer comparison. ClinStrata QC Engine is not a replacement for Unity — it is architecturally different. Unity is a hosted service with proprietary rule logic and a peer group model built around commercial reagent lots. ClinStrata is an open-source computational library you embed in your own informatics system, with fully auditable rule logic, configurable TEa, and no dependency on a peer population. For LC-MS/MS LDTs where no peer group exists and rule logic must be transparent and independently verifiable, ClinStrata fills a gap that Unity's architecture does not address.

---

## Citation

If you use this engine in published work, please cite:

> Folagbayi JO. An open-source, CLSI EP23-aligned statistical quality control engine for clinical laboratory informatics: design, implementation, and validation. *Journal of Applied Laboratory Medicine*. [In submission, 2026].

---

## License

Apache 2.0. See [LICENSE](LICENSE).
