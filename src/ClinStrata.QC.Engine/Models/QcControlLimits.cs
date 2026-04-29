namespace ClinStrata.QC.Engine.Models;

/// <summary>
/// Statistical control limits derived from a QC material's established mean and standard deviation.
/// </summary>
public record QcControlLimits
{
    public required double Mean { get; init; }
    public required double Sd   { get; init; }

    public double Plus1s   => Mean + 1 * Sd;
    public double Minus1s  => Mean - 1 * Sd;
    public double Plus2s   => Mean + 2 * Sd;
    public double Minus2s  => Mean - 2 * Sd;
    public double Plus3s   => Mean + 3 * Sd;
    public double Minus3s  => Mean - 3 * Sd;

    public void Validate()
    {
        if (Sd <= 0)
            throw new ArgumentOutOfRangeException(nameof(Sd), "Standard deviation must be greater than zero.");
        if (!double.IsFinite(Mean))
            throw new ArgumentOutOfRangeException(nameof(Mean), "Mean must be a finite number.");
    }
}
