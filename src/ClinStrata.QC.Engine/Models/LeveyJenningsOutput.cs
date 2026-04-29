namespace ClinStrata.QC.Engine.Models;

/// <summary>
/// Structured output for Levey-Jennings chart rendering.
/// The engine produces structured data only — rendering is delegated to the consuming system's
/// presentation layer, enabling integration across web, desktop, and report contexts.
/// </summary>
public record LeveyJenningsOutput
{
    public required double Mean    { get; init; }
    public required double Plus1s  { get; init; }
    public required double Minus1s { get; init; }
    public required double Plus2s  { get; init; }
    public required double Minus2s { get; init; }
    public required double Plus3s  { get; init; }
    public required double Minus3s { get; init; }
    public required IReadOnlyList<LeveyJenningsDataPoint> DataPoints { get; init; }
}

public record LeveyJenningsDataPoint(
    int Index,
    double Value,
    bool IsViolation,
    string? ViolatingRuleName,
    ViolationType ViolationType,
    string? RunId,
    DateTimeOffset? Timestamp
);
