namespace ClinStrata.QC.Engine.Models;

/// <summary>
/// A single quality control measurement result.
/// </summary>
public record QcObservation(
    double Value,
    string? RunId = null,
    string? LotNumber = null,
    DateTimeOffset? Timestamp = null
);
