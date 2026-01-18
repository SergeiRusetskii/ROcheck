using System.Collections.Generic;

namespace ROcheck
{
    /// <summary>
    /// Configuration for Clinic E (Eclipse 18.0).
    /// Defines clinic-specific thresholds, exclusions, and naming conventions.
    /// </summary>
    public class ClinicEConfig : IValidationConfig
    {
        public string[] TargetPrefixes => new[] { "PTV", "CTV", "GTV" };

        public string BodyStructureId => "BODY";

        public HashSet<string> ExcludedStructures => new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "Bones", "CouchInterior", "CouchSurface", "Clips", "Scar_Wire", "Sternum"
        };

        public string[] ExcludedPatterns => new[]
        {
            "z_*", "*wire*", "*Encompass*", "*Enc Marker*", "*Dose*",
            "Implant*", "Lymph*", "LN_*"
        };

        public double PTVBodyProximityThresholdMm => 4.0;

        public double HighResVolumeThresholdCc => 10.0;

        public double HighResCriticalThresholdCc => 5.0;

        public double SIBDosePercentThreshold => 6.0;

        public string ClinicName => "Clinic E";

        public string EclipseVersion => "18.0";
    }
}
