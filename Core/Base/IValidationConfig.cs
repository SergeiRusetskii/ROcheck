using System.Collections.Generic;

namespace ROcheck
{
    public interface IValidationConfig
    {
        // Structure naming
        string[] TargetPrefixes { get; }
        string BodyStructureId { get; }

        // Exclusion lists
        HashSet<string> ExcludedStructures { get; }
        string[] ExcludedPatterns { get; }

        // Thresholds
        double PTVBodyProximityThresholdMm { get; }
        double HighResVolumeThresholdCc { get; }
        double HighResCriticalThresholdCc { get; }
        double SIBDosePercentThreshold { get; }

        // Clinic metadata
        string ClinicName { get; }
        string EclipseVersion { get; }
    }
}
