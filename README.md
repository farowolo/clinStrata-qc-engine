# ClinStrata QC Engine

**An open-source, CLSI EP23-aligned Westgard multi-rule QC engine and 2025 CLIA PT-based sigma metric calculator for clinical laboratory informatics.**

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

This library fills that gap: a clean, stateless, independently testable C#/.NET implementation of the six standard Westgard rules and the CLSI EP23-A sigma metric framework, backed by the full 2025 CLIA PT acceptance criteria database. Integrate it into your informatics system and it handles the math — transparently, auditably, and portably.

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

### 2025 CLIA PT acceptance criteria database

87 analytes across Chemistry, Immunology, Endocrinology, Toxicology, and Hematology, sourced from 42 CFR Part 493 (CMS-3355-F Final Rule, effective July 11, 2024, implemented January 1, 2025).

Three criterion types are supported:

- **Simple** — TV ± X% (e.g., Albumin ±8%, HbA1c ±8%, Triglycerides ±15%)
- **Composite** — TV ± X% or ± Y units, whichever is greater (e.g., Glucose: ±8% or ±6 mg/dL; Creatinine: ±10% or ±0.2 mg/dL; TSH: ±20% or ±0.2 mIU/L). TEa% is **concentration-dependent** for composite criteria — pass the QC material mean for correct calculation.
- **Absolute** — TV ± X units only (e.g., Potassium: ±0.3 mmol/L; Sodium: ±4 mmol/L; Blood gas pH: ±0.04). Requires QC mean for TEa% conversion.

For analytes without CLIA PT criteria — immunosuppressants, antifungal TDM, NGAL, 25-OH Vitamin D, and other LDTs — supply `teaPct` based on clinical decision limits or biological variation data.

### Levey-Jennings output

Structured data objects: mean, ±1s/2s/3s control limits, per-observation violation flags with rule attribution. Chart rendering is delegated to the consuming system's presentation layer.

---

## Quick start

### Run QC evaluation

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

if (result.WarningTriggered && result.IsAccepted)
    Console.WriteLine("1-2s warning — monitor closely but run is accepted.");
```

### Calculate sigma — simple criterion (percentage only)

```csharp
var calc = new SigmaCalculator();

// Albumin — 2025 CLIA ±8% (simple criterion, no qcMean needed)
var sigma = calc.Calculate(biasPct: 1.5, cvPct: 2.0, analyte: "albumin");
Console.WriteLine($"σ = {sigma.Sigma}");               // σ = 3.25
Console.WriteLine(sigma.RecommendationDescription);    // Full multi-rule, N=4
Console.WriteLine(sigma.Criteria!.Description);        // TV ±8%
```

### Calculate sigma — composite criterion (concentration-dependent TEa)

```csharp
// Glucose — 2025 CLIA ±8% or ±6 mg/dL (greater)
// Always pass qcMean for composite criteria — TEa% varies by concentration

// Low QC (mean ~75 mg/dL): abs% = 6/75*100 = 8.0% = fixed% → TEa = 8.0%
var sigmaLow = calc.Calculate(biasPct: 1.0, cvPct: 1.5, analyte: "glucose", qcMean: 75.0);
Console.WriteLine($"Low QC σ = {sigmaLow.Sigma}, TEa = {sigmaLow.Tea}%");

// High QC (mean ~250 mg/dL): abs% = 6/250*100 = 2.4% < 8% → TEa = 8.0%
var sigmaHigh = calc.Calculate(biasPct: 1.0, cvPct: 1.5, analyte: "glucose", qcMean: 250.0);
Console.WriteLine($"High QC σ = {sigmaHigh.Sigma}, TEa = {sigmaHigh.Tea}%");

// Creatinine — ±10% or ±0.2 mg/dL (greater)
// At low creatinine (0.5 mg/dL), absolute dominates: abs% = 0.2/0.5*100 = 40%
var sigmaCr = calc.Calculate(biasPct: 2.0, cvPct: 3.0, analyte: "creatinine", qcMean: 0.5);
Console.WriteLine($"Low creatinine TEa = {sigmaCr.Tea}%");  // 40% — not 10%
```

### Calculate sigma — absolute criterion

```csharp
// Potassium — ±0.3 mmol/L absolute (no fixed percentage component)
// TEa% depends entirely on the QC concentration
var sigmaK = calc.Calculate(biasPct: 0.2, cvPct: 0.8, analyte: "potassium", qcMean: 4.0);
Console.WriteLine($"K TEa = {sigmaK.Tea}%");  // 7.5% (= 0.3/4.0*100)
```

### Calculate sigma — LDT analyte (user-supplied TEa)

```csharp
// Voriconazole — no CLIA PT criteria; use clinical decision limit-based TEa
var sigmaVori = calc.Calculate(
    biasPct: 9.8,
    cvPct: 5.0,
    teaPct: 30.0,
    analyte: "voriconazole",
    teaSource: "Conservative clinical estimate (therapeutic range 0.5–4 mg/L)");
Console.WriteLine($"σ = {sigmaVori.Sigma}");   // 4.04 → Three-rule set
```

### Check CLIA criteria coverage

```csharp
// Check if an analyte has 2025 CLIA PT criteria
SigmaCalculator.HasCliaCriteria("glucose");      // true
SigmaCalculator.HasCliaCriteria("hba1c");        // true  (new in 2025)
SigmaCalculator.HasCliaCriteria("bnp");          // true  (new in 2025)
SigmaCalculator.HasCliaCriteria("vancomycin");   // true  (composite, tightened)
SigmaCalculator.HasCliaCriteria("cyclosporine"); // false (no CLIA PT criteria — use teaPct)
SigmaCalculator.HasCliaCriteria("ngal");         // false (no CLIA PT criteria — use teaPct)

// Inspect the resolved criterion
var criteria = SigmaCalculator.GetCriteria("tsh");
Console.WriteLine(criteria!.Description);        // TV ±20% or ±0.2 mIU/L (greater)
Console.WriteLine(criteria.Type);               // Composite
Console.WriteLine(criteria.RequiresQcMean);     // True
```

### Exhaustive mode for audit trail

```csharp
var result = engine.Evaluate(observations, limits, EvaluationMode.Exhaustive);
foreach (var v in result.AllViolations)
    Console.WriteLine($"{v.RuleName}: {v.Description}");
```

### Levey-Jennings chart data

```csharp
var builder = new LeveyJenningsBuilder();
var chart = builder.Build(observations, limits);

foreach (var point in chart.DataPoints)
    Console.WriteLine($"[{point.Index}] {point.Value:F2} — " +
        (point.IsViolation ? $"VIOLATION: {point.ViolatingRuleName}" : "OK"));
```

---

## 2025 CLIA PT criteria — key changes from prior version

| Analyte | Old | 2025 (Current) |
|---|---|---|
| Glucose | ±10% | ±8% or ±6 mg/dL (composite) |
| Creatinine | ±15% | ±10% or ±0.2 mg/dL (composite) |
| ALT / AST | ±20% | ±15% or ±6 U/L (composite) |
| Albumin | ±10% | ±8% |
| Total protein | ±10% | ±8% |
| Triglycerides | ±25% | ±15% |
| Alkaline phosphatase | ±30% | ±20% |
| Magnesium | ±25% | ±15% |
| HDL cholesterol | ±30% | ±20% or ±6 mg/dL (composite) |
| TSH | ±3 SD | ±20% or ±0.2 mIU/L (composite) |
| Phenytoin | ±25% | ±15% or ±2 mcg/mL (composite) |
| Vancomycin | not regulated | ±15% or ±2 mcg/mL (composite) |
| Lithium | ±20% | ±15% or ±0.3 mmol/L (composite) |
| HbA1c | not regulated | ±8% (new) |
| BNP / NT-proBNP | not regulated | ±30% (new) |
| Hemoglobin | ±7% | ±4% |
| WBC | ±15% | ±10% |
| RBC / Hematocrit | ±6% | ±4% |

---

## Validation

The engine is validated against 24 deterministic test scenarios covering all implemented rules, boundary conditions, multi-rule combinations, and edge cases. All scenarios are cross-referenced against Westgard Web (Westgard QC, Inc.) and published worked examples from CLSI EP23-A.

```bash
dotnet test
# 54 tests — 24 Westgard scenarios + sigma calculator suite
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

**No CUSUM or EWMA.** These methods provide superior detection of small persistent systematic errors — the kind that accumulate slowly across runs and are invisible to Westgard multi-rules. Planned for a future release.

**Composite and absolute TEa criteria require qcMean.** For analytes with concentration-dependent CLIA criteria (glucose, creatinine, TSH, vancomycin, etc.), always pass the QC material mean. Without it, the engine returns the fixed percentage component only, which may underestimate TEa at low concentrations and overestimate sigma.

**TEa is not automatically appropriate for all analytes.** CLIA PT criteria are regulatory acceptance limits, not necessarily optimal analytical quality specifications. For LDTs and esoteric assays — and especially for analytes at clinical decision thresholds — consider whether biological variation-derived or clinical decision limit-based TEa is more appropriate than the CLIA default.

**Stateless by design.** The engine evaluates discrete observation windows and does not maintain running statistics between calls. Inter-run trending is the responsibility of the consuming informatics system.

**Clinical judgment is always required.** Sigma-based rule selection is evidence-based guidance, not a prescriptive mandate. QC plan finalization requires a qualified laboratory professional.

---

## How this relates to Bio-Rad Unity

Bio-Rad Unity is the most widely deployed QC management platform in clinical laboratories and provides valuable interlaboratory peer comparison. ClinStrata QC Engine is not a replacement for Unity — it is architecturally different. Unity is a hosted service with proprietary rule logic and a peer group model built around commercial reagent lots. ClinStrata is an open-source computational library you embed in your own informatics system, with fully auditable rule logic, 2025 CLIA-aligned TEa, and no dependency on a peer population. For LC-MS/MS LDTs where no peer group exists and rule logic must be transparent and independently verifiable, ClinStrata fills a gap that Unity's architecture does not address.

---

## Citation

If you use this engine in published work, please cite:

> Arowolo FK. An open-source, CLSI EP23-aligned statistical quality control engine for clinical laboratory informatics: design, implementation, and validation. *Journal of Applied Laboratory Medicine*. [In submission, 2026].

---

## License

Apache 2.0. See [LICENSE](LICENSE).
