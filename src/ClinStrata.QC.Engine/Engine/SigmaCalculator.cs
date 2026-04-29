using ClinStrata.QC.Engine.Models;

namespace ClinStrata.QC.Engine.Engine;

/// <summary>
/// Calculates the sigma metric per CLSI EP23-A.
/// Formula: σ = (TEa − |bias|) / CV
///
/// TEa is resolved from the 2025 CLIA PT acceptance criteria (42 CFR §493,
/// CMS-3355-F Final Rule, effective July 11, 2024, implemented by PT programs
/// January 1, 2025).
///
/// Many 2025 CLIA criteria are composite: "TV ± X units OR ± Y%, whichever is greater."
/// For composite criteria, the QC material mean must be provided so the absolute
/// allowable error can be converted to a percentage at the actual QC concentration.
/// This is the only scientifically valid approach — TEa% is concentration-dependent
/// for composite criteria.
///
/// For analytes with no CLIA PT criteria (LDTs, immunosuppressants, antifungal TDM,
/// NGAL, 25-OH Vitamin D, etc.), specify teaPct explicitly and document the clinical
/// decision limit or biological variation basis for the chosen TEa.
/// </summary>
public sealed class SigmaCalculator
{
    private static CliaTeaCriteria S(string n, double pct)                            => new(n, pct,  null,   null,    CriteriaType.Simple);
    private static CliaTeaCriteria C(string n, double pct, double abs, string unit)   => new(n, pct,  abs,    unit,    CriteriaType.Composite);
    private static CliaTeaCriteria A(string n,             double abs, string unit)   => new(n, 0.0,  abs,    unit,    CriteriaType.AbsoluteOnly);

    // ─────────────────────────────────────────────────────────────────────────
    // 2025 CLIA PT ACCEPTANCE CRITERIA — 42 CFR Part 493 Subpart I
    // Source: CMS-3355-F Final Rule (effective July 11, 2024)
    // Implemented by PT programs: January 1, 2025
    // Coverage: 87 analytes with quantitative TEa criteria across
    //           Chemistry, Immunology, Endocrinology, Toxicology, Hematology
    // ─────────────────────────────────────────────────────────────────────────
    private static readonly Dictionary<string, CliaTeaCriteria> Db =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // ── CHEMISTRY ────────────────────────────────────────────────────────
        ["alt"]                          = C("ALT/SGPT",              15.0,  6.0,  "U/L"),
        ["alt/sgpt"]                     = C("ALT/SGPT",              15.0,  6.0,  "U/L"),
        ["alanine aminotransferase"]     = C("ALT/SGPT",              15.0,  6.0,  "U/L"),
        ["sgpt"]                         = C("ALT/SGPT",              15.0,  6.0,  "U/L"),
        ["albumin"]                      = S("Albumin",               8.0),
        ["alkaline phosphatase"]         = S("Alkaline Phosphatase",  20.0),
        ["alp"]                          = S("Alkaline Phosphatase",  20.0),
        ["alk phos"]                     = S("Alkaline Phosphatase",  20.0),
        ["amylase"]                      = S("Amylase",               20.0),
        ["ast"]                          = C("AST",                   15.0,  6.0,  "U/L"),
        ["ast/sgot"]                     = C("AST",                   15.0,  6.0,  "U/L"),
        ["aspartate aminotransferase"]   = C("AST",                   15.0,  6.0,  "U/L"),
        ["sgot"]                         = C("AST",                   15.0,  6.0,  "U/L"),
        ["bilirubin"]                    = C("Bilirubin, total",      20.0,  0.4,  "mg/dL"),
        ["total bilirubin"]              = C("Bilirubin, total",      20.0,  0.4,  "mg/dL"),
        ["bilirubin total"]              = C("Bilirubin, total",      20.0,  0.4,  "mg/dL"),
        ["tbili"]                        = C("Bilirubin, total",      20.0,  0.4,  "mg/dL"),
        ["pco2"]                         = C("Blood gas pCO2",        8.0,   5.0,  "mmHg"),
        ["blood gas pco2"]               = C("Blood gas pCO2",        8.0,   5.0,  "mmHg"),
        ["po2"]                          = C("Blood gas pO2",         15.0,  15.0, "mmHg"),
        ["blood gas po2"]                = C("Blood gas pO2",         15.0,  15.0, "mmHg"),
        ["ph"]                           = A("Blood gas pH",          0.04,  "pH units"),
        ["blood gas ph"]                 = A("Blood gas pH",          0.04,  "pH units"),
        ["bnp"]                          = S("BNP",                   30.0),
        ["b-natriuretic peptide"]        = S("BNP",                   30.0),
        ["brain natriuretic peptide"]    = S("BNP",                   30.0),
        ["nt-probnp"]                    = S("NT-proBNP",             30.0),
        ["probnp"]                       = S("NT-proBNP",             30.0),
        ["pro b-natriuretic peptide"]    = S("NT-proBNP",             30.0),
        ["pro bnp"]                      = S("NT-proBNP",             30.0),
        ["calcium"]                      = A("Calcium, total",        1.0,   "mg/dL"),
        ["total calcium"]                = A("Calcium, total",        1.0,   "mg/dL"),
        ["ca"]                           = A("Calcium, total",        1.0,   "mg/dL"),
        ["carbon dioxide"]               = S("Carbon dioxide",        20.0),
        ["co2"]                          = S("Carbon dioxide",        20.0),
        ["bicarbonate"]                  = S("Carbon dioxide",        20.0),
        ["hco3"]                         = S("Carbon dioxide",        20.0),
        ["tco2"]                         = S("Carbon dioxide",        20.0),
        ["chloride"]                     = S("Chloride",              5.0),
        ["cl"]                           = S("Chloride",              5.0),
        ["cholesterol"]                  = S("Cholesterol, total",    10.0),
        ["total cholesterol"]            = S("Cholesterol, total",    10.0),
        ["hdl"]                          = C("HDL Cholesterol",       20.0,  6.0,  "mg/dL"),
        ["hdl cholesterol"]              = C("HDL Cholesterol",       20.0,  6.0,  "mg/dL"),
        ["high density lipoprotein"]     = C("HDL Cholesterol",       20.0,  6.0,  "mg/dL"),
        ["ldl"]                          = S("LDL Cholesterol",       20.0),
        ["ldl cholesterol"]              = S("LDL Cholesterol",       20.0),
        ["low density lipoprotein"]      = S("LDL Cholesterol",       20.0),
        ["creatine kinase"]              = S("Creatine kinase",       20.0),
        ["ck"]                           = S("Creatine kinase",       20.0),
        ["cpk"]                          = S("Creatine kinase",       20.0),
        ["ck-mb"]                        = C("CK-MB",                 25.0,  3.0,  "ng/mL"),
        ["ckmb"]                         = C("CK-MB",                 25.0,  3.0,  "ng/mL"),
        ["creatinine"]                   = C("Creatinine",            10.0,  0.2,  "mg/dL"),
        ["creat"]                        = C("Creatinine",            10.0,  0.2,  "mg/dL"),
        ["scr"]                          = C("Creatinine",            10.0,  0.2,  "mg/dL"),
        ["ferritin"]                     = S("Ferritin",              20.0),
        ["ggt"]                          = C("GGT",                   15.0,  5.0,  "U/L"),
        ["gamma glutamyl transferase"]   = C("GGT",                   15.0,  5.0,  "U/L"),
        ["gamma-glutamyltransferase"]    = C("GGT",                   15.0,  5.0,  "U/L"),
        ["glucose"]                      = C("Glucose",               8.0,   6.0,  "mg/dL"),
        ["hba1c"]                        = S("Hemoglobin A1c",        8.0),
        ["hemoglobin a1c"]               = S("Hemoglobin A1c",        8.0),
        ["a1c"]                          = S("Hemoglobin A1c",        8.0),
        ["iron"]                         = S("Iron, total",           15.0),
        ["total iron"]                   = S("Iron, total",           15.0),
        ["serum iron"]                   = S("Iron, total",           15.0),
        ["ldh"]                          = S("LDH",                   15.0),
        ["lactate dehydrogenase"]        = S("LDH",                   15.0),
        ["magnesium"]                    = S("Magnesium",             15.0),
        ["mg"]                           = S("Magnesium",             15.0),
        ["phosphorus"]                   = C("Phosphorus",            10.0,  0.3,  "mg/dL"),
        ["phosphate"]                    = C("Phosphorus",            10.0,  0.3,  "mg/dL"),
        ["phos"]                         = C("Phosphorus",            10.0,  0.3,  "mg/dL"),
        ["potassium"]                    = A("Potassium",             0.3,   "mmol/L"),
        ["k"]                            = A("Potassium",             0.3,   "mmol/L"),
        ["psa"]                          = C("PSA, total",            20.0,  0.2,  "ng/mL"),
        ["prostate specific antigen"]    = C("PSA, total",            20.0,  0.2,  "ng/mL"),
        ["total psa"]                    = C("PSA, total",            20.0,  0.2,  "ng/mL"),
        ["sodium"]                       = A("Sodium",                4.0,   "mmol/L"),
        ["na"]                           = A("Sodium",                4.0,   "mmol/L"),
        ["tibc"]                         = S("TIBC",                  20.0),
        ["total iron binding capacity"]  = S("TIBC",                  20.0),
        ["total protein"]                = S("Total Protein",         8.0),
        ["protein total"]                = S("Total Protein",         8.0),
        ["tp"]                           = S("Total Protein",         8.0),
        ["triglycerides"]                = S("Triglycerides",         15.0),
        ["trig"]                         = S("Triglycerides",         15.0),
        ["trigs"]                        = S("Triglycerides",         15.0),
        ["troponin i"]                   = C("Troponin I",            30.0,  0.9,  "ng/mL"),
        ["tni"]                          = C("Troponin I",            30.0,  0.9,  "ng/mL"),
        ["troponin t"]                   = C("Troponin T",            30.0,  0.2,  "ng/mL"),
        ["tnt"]                          = C("Troponin T",            30.0,  0.2,  "ng/mL"),
        ["hstnt"]                        = C("Troponin T",            30.0,  0.2,  "ng/mL"),
        ["bun"]                          = C("Urea Nitrogen",         9.0,   2.0,  "mg/dL"),
        ["urea nitrogen"]                = C("Urea Nitrogen",         9.0,   2.0,  "mg/dL"),
        ["blood urea nitrogen"]          = C("Urea Nitrogen",         9.0,   2.0,  "mg/dL"),
        ["uric acid"]                    = S("Uric Acid",             10.0),
        ["urate"]                        = S("Uric Acid",             10.0),

        // ── IMMUNOLOGY ───────────────────────────────────────────────────────
        ["alpha-1 antitrypsin"]          = S("Alpha-1 antitrypsin",   20.0),
        ["a1at"]                         = S("Alpha-1 antitrypsin",   20.0),
        ["alpha-fetoprotein"]            = S("AFP",                   20.0),
        ["afp"]                          = S("AFP",                   20.0),
        ["complement c3"]                = S("Complement C3",         15.0),
        ["c3"]                           = S("Complement C3",         15.0),
        ["complement c4"]                = C("Complement C4",         20.0,  5.0,  "mg/dL"),
        ["c4"]                           = C("Complement C4",         20.0,  5.0,  "mg/dL"),
        ["crp"]                          = C("CRP (hs)",              30.0,  1.0,  "mg/L"),
        ["c-reactive protein"]           = C("CRP (hs)",              30.0,  1.0,  "mg/L"),
        ["hs-crp"]                       = C("CRP (hs)",              30.0,  1.0,  "mg/L"),
        ["hscrp"]                        = C("CRP (hs)",              30.0,  1.0,  "mg/L"),
        ["high sensitivity crp"]         = C("CRP (hs)",              30.0,  1.0,  "mg/L"),
        ["iga"]                          = S("IgA",                   20.0),
        ["immunoglobulin a"]             = S("IgA",                   20.0),
        ["ige"]                          = S("IgE",                   20.0),
        ["immunoglobulin e"]             = S("IgE",                   20.0),
        ["igg"]                          = S("IgG",                   20.0),
        ["immunoglobulin g"]             = S("IgG",                   20.0),
        ["igm"]                          = S("IgM",                   20.0),
        ["immunoglobulin m"]             = S("IgM",                   20.0),

        // ── ENDOCRINOLOGY ────────────────────────────────────────────────────
        ["ca-125"]                       = S("CA-125",                20.0),
        ["ca 125"]                       = S("CA-125",                20.0),
        ["cancer antigen 125"]           = S("CA-125",                20.0),
        ["cea"]                          = C("CEA",                   15.0,  1.0,  "ng/dL"),
        ["carcinoembryonic antigen"]     = C("CEA",                   15.0,  1.0,  "ng/dL"),
        ["cortisol"]                     = S("Cortisol",              20.0),
        ["estradiol"]                    = S("Estradiol",             30.0),
        ["e2"]                           = S("Estradiol",             30.0),
        ["folate"]                       = C("Folate, serum",         30.0,  1.0,  "ng/mL"),
        ["folic acid"]                   = C("Folate, serum",         30.0,  1.0,  "ng/mL"),
        ["serum folate"]                 = C("Folate, serum",         30.0,  1.0,  "ng/mL"),
        ["fsh"]                          = C("FSH",                   18.0,  2.0,  "IU/L"),
        ["follicle stimulating hormone"] = C("FSH",                   18.0,  2.0,  "IU/L"),
        ["free t4"]                      = C("Free T4",               15.0,  0.3,  "ng/dL"),
        ["ft4"]                          = C("Free T4",               15.0,  0.3,  "ng/dL"),
        ["free thyroxine"]               = C("Free T4",               15.0,  0.3,  "ng/dL"),
        ["hcg"]                          = C("HCG",                   18.0,  3.0,  "mIU/mL"),
        ["human chorionic gonadotropin"] = C("HCG",                   18.0,  3.0,  "mIU/mL"),
        ["beta hcg"]                     = C("HCG",                   18.0,  3.0,  "mIU/mL"),
        ["b-hcg"]                        = C("HCG",                   18.0,  3.0,  "mIU/mL"),
        ["lh"]                           = S("LH",                    20.0),
        ["luteinizing hormone"]          = S("LH",                    20.0),
        ["pth"]                          = S("PTH",                   30.0),
        ["parathyroid hormone"]          = S("PTH",                   30.0),
        ["intact pth"]                   = S("PTH",                   30.0),
        ["progesterone"]                 = S("Progesterone",          25.0),
        ["prolactin"]                    = S("Prolactin",             20.0),
        ["prl"]                          = S("Prolactin",             20.0),
        ["testosterone"]                 = C("Testosterone",          30.0,  20.0, "ng/dL"),
        ["total testosterone"]           = C("Testosterone",          30.0,  20.0, "ng/dL"),
        ["t3 uptake"]                    = S("T3 uptake",             18.0),
        ["t3u"]                          = S("T3 uptake",             18.0),
        ["t3"]                           = S("T3",                    30.0),
        ["triiodothyronine"]             = S("T3",                    30.0),
        ["total t3"]                     = S("T3",                    30.0),
        ["tsh"]                          = C("TSH",                   20.0,  0.2,  "mIU/L"),
        ["thyroid stimulating hormone"]  = C("TSH",                   20.0,  0.2,  "mIU/L"),
        ["thyrotropin"]                  = C("TSH",                   20.0,  0.2,  "mIU/L"),
        ["t4"]                           = C("Thyroxine (T4)",        20.0,  1.0,  "mcg/dL"),
        ["thyroxine"]                    = C("Thyroxine (T4)",        20.0,  1.0,  "mcg/dL"),
        ["total t4"]                     = C("Thyroxine (T4)",        20.0,  1.0,  "mcg/dL"),
        ["vitamin b12"]                  = C("Vitamin B12",           25.0,  30.0, "pg/mL"),
        ["b12"]                          = C("Vitamin B12",           25.0,  30.0, "pg/mL"),
        ["cobalamin"]                    = C("Vitamin B12",           25.0,  30.0, "pg/mL"),

        // ── TOXICOLOGY ───────────────────────────────────────────────────────
        ["acetaminophen"]                = C("Acetaminophen",         15.0,  3.0,  "mcg/mL"),
        ["apap"]                         = C("Acetaminophen",         15.0,  3.0,  "mcg/mL"),
        ["paracetamol"]                  = C("Acetaminophen",         15.0,  3.0,  "mcg/mL"),
        ["alcohol"]                      = S("Alcohol, blood",        20.0),
        ["ethanol"]                      = S("Alcohol, blood",        20.0),
        ["etoh"]                         = S("Alcohol, blood",        20.0),
        ["blood alcohol"]                = S("Alcohol, blood",        20.0),
        ["blood lead"]                   = C("Blood lead",            10.0,  2.0,  "mcg/dL"),
        ["lead"]                         = C("Blood lead",            10.0,  2.0,  "mcg/dL"),
        ["pb"]                           = C("Blood lead",            10.0,  2.0,  "mcg/dL"),
        ["carbamazepine"]                = C("Carbamazepine",         20.0,  1.0,  "mcg/mL"),
        ["cbz"]                          = C("Carbamazepine",         20.0,  1.0,  "mcg/mL"),
        ["tegretol"]                     = C("Carbamazepine",         20.0,  1.0,  "mcg/mL"),
        ["digoxin"]                      = C("Digoxin",               15.0,  0.2,  "ng/mL"),
        ["gentamicin"]                   = S("Gentamicin",            25.0),
        ["gent"]                         = S("Gentamicin",            25.0),
        ["lithium"]                      = C("Lithium",               15.0,  0.3,  "mmol/L"),
        ["li"]                           = C("Lithium",               15.0,  0.3,  "mmol/L"),
        ["phenobarbital"]                = C("Phenobarbital",         15.0,  2.0,  "mcg/mL"),
        ["phenobarb"]                    = C("Phenobarbital",         15.0,  2.0,  "mcg/mL"),
        ["phenytoin"]                    = C("Phenytoin",             15.0,  2.0,  "mcg/mL"),
        ["dilantin"]                     = C("Phenytoin",             15.0,  2.0,  "mcg/mL"),
        ["salicylate"]                   = C("Salicylate",            15.0,  2.0,  "mcg/mL"),
        ["aspirin"]                      = C("Salicylate",            15.0,  2.0,  "mcg/mL"),
        ["asa"]                          = C("Salicylate",            15.0,  2.0,  "mcg/mL"),
        ["theophylline"]                 = S("Theophylline",          20.0),
        ["aminophylline"]                = S("Theophylline",          20.0),
        ["tobramycin"]                   = S("Tobramycin",            20.0),
        ["tobi"]                         = S("Tobramycin",            20.0),
        ["valproic acid"]                = S("Valproic acid",         20.0),
        ["valproate"]                    = S("Valproic acid",         20.0),
        ["vpa"]                          = S("Valproic acid",         20.0),
        ["depakote"]                     = S("Valproic acid",         20.0),
        ["vancomycin"]                   = C("Vancomycin",            15.0,  2.0,  "mcg/mL"),
        ["vanc"]                         = C("Vancomycin",            15.0,  2.0,  "mcg/mL"),

        // ── HEMATOLOGY ───────────────────────────────────────────────────────
        ["rbc"]                          = S("Erythrocyte count",     4.0),
        ["erythrocyte count"]            = S("Erythrocyte count",     4.0),
        ["red blood cell count"]         = S("Erythrocyte count",     4.0),
        ["hematocrit"]                   = S("Hematocrit",            4.0),
        ["hct"]                          = S("Hematocrit",            4.0),
        ["hemoglobin"]                   = S("Hemoglobin",            4.0),
        ["hgb"]                          = S("Hemoglobin",            4.0),
        ["hb"]                           = S("Hemoglobin",            4.0),
        ["wbc"]                          = S("Leukocyte count",       10.0),
        ["leukocyte count"]              = S("Leukocyte count",       10.0),
        ["white blood cell count"]       = S("Leukocyte count",       10.0),
        ["platelets"]                    = S("Platelet count",        25.0),
        ["platelet count"]               = S("Platelet count",        25.0),
        ["plt"]                          = S("Platelet count",        25.0),
        ["thrombocyte count"]            = S("Platelet count",        25.0),
        ["fibrinogen"]                   = S("Fibrinogen",            20.0),
        ["ptt"]                          = S("PTT",                   15.0),
        ["aptt"]                         = S("PTT",                   15.0),
        ["partial thromboplastin time"]  = S("PTT",                   15.0),
        ["pt"]                           = S("PT",                    15.0),
        ["prothrombin time"]             = S("PT",                    15.0),
        ["inr"]                          = S("PT",                    15.0),
    };

    // ─────────────────────────────────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Calculates the sigma metric for an analyte.
    /// </summary>
    /// <param name="biasPct">Method bias as percent of target. Sign is ignored; absolute value is used.</param>
    /// <param name="cvPct">Between-day (within-lab) CV (%) from EP05 precision study.</param>
    /// <param name="teaPct">
    /// Total allowable error (%). If null, resolved automatically from the 2025 CLIA
    /// PT database. For composite CLIA criteria, qcMean is required for correct TEa% calculation.
    /// </param>
    /// <param name="analyte">Analyte name for CLIA TEa lookup and documentation.</param>
    /// <param name="qcMean">
    /// QC material mean value in native units. Required for composite and absolute CLIA criteria.
    /// Ignored for simple percentage criteria.
    /// </param>
    /// <param name="teaSource">Description of TEa source for documentation.</param>
    public SigmaResult Calculate(
        double biasPct,
        double cvPct,
        double? teaPct    = null,
        string? analyte   = null,
        double? qcMean    = null,
        string? teaSource = null)
    {
        if (cvPct <= 0)
            throw new ArgumentOutOfRangeException(nameof(cvPct),
                "CV must be greater than zero. Division by zero would occur.");

        double tea;
        string resolvedSource;
        CliaTeaCriteria? criteria = null;

        if (teaPct.HasValue)
        {
            if (teaPct.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(teaPct),
                    "TEa must be greater than zero.");
            tea = teaPct.Value;
            resolvedSource = teaSource ?? "User-specified";
        }
        else if (analyte is not null && Db.TryGetValue(analyte, out criteria))
        {
            tea = criteria.CalculateTeaPct(qcMean);

            resolvedSource = criteria.Type switch
            {
                CriteriaType.Simple =>
                    $"2025 CLIA PT criteria (42 CFR §493) — ±{criteria.FixedPct}%",
                CriteriaType.Composite when qcMean.HasValue =>
                    $"2025 CLIA PT criteria (42 CFR §493) — composite ±{criteria.FixedPct}% " +
                    $"or ±{criteria.AbsoluteValue} {criteria.AbsoluteUnit} (greater), " +
                    $"evaluated at QC mean {qcMean:F2} → TEa = {tea:F1}%",
                CriteriaType.Composite =>
                    $"2025 CLIA PT criteria (42 CFR §493) — composite ±{criteria.FixedPct}% " +
                    $"or ±{criteria.AbsoluteValue} {criteria.AbsoluteUnit} (greater); " +
                    "provide qcMean for concentration-specific TEa",
                CriteriaType.AbsoluteOnly when qcMean.HasValue =>
                    $"2025 CLIA PT criteria (42 CFR §493) — absolute ±{criteria.AbsoluteValue} " +
                    $"{criteria.AbsoluteUnit}, converted to {tea:F1}% at QC mean {qcMean:F2}",
                _ =>
                    $"2025 CLIA PT criteria (42 CFR §493)"
            };
        }
        else
        {
            throw new InvalidOperationException(
                $"No TEa provided and no 2025 CLIA PT criteria found for " +
                $"'{analyte ?? "(null)"}'. " +
                "Analytes without CLIA PT criteria (e.g., immunosuppressants, " +
                "antifungal TDM, NGAL, 25-OH Vitamin D) require a user-supplied teaPct " +
                "based on clinical decision limits or biological variation data.");
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
            Analyte        = analyte,
            Criteria       = criteria
        };
    }

    /// <summary>Returns the 2025 CLIA criteria for a given analyte, or null if not found.</summary>
    public static CliaTeaCriteria? GetCriteria(string analyte) =>
        Db.TryGetValue(analyte, out var c) ? c : null;

    /// <summary>Returns true if the analyte has 2025 CLIA PT criteria.</summary>
    public static bool HasCliaCriteria(string analyte) =>
        Db.ContainsKey(analyte);

    /// <summary>Returns all lookup keys in the 2025 CLIA database.</summary>
    public static IReadOnlyCollection<string> AllAnalyteKeys => Db.Keys;
}
