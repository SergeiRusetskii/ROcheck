using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;

namespace ROcheck.Validators
{
    /// <summary>
    /// Validates that small PTVs and their related targets (CTV/GTV) use high-resolution contouring.
    /// Small volumes require high resolution for accurate dose calculation and delivery.
    /// Error for <5cc, Warning for 5-10cc if any related target is not high resolution.
    /// </summary>
    public class TargetResolutionValidator : ValidatorBase
    {
        private readonly IValidationConfig _config;

        public TargetResolutionValidator(IValidationConfig config)
        {
            _config = config;
        }

        public override IEnumerable<ValidationResult> Validate(ScriptContext context)
        {
            var results = new List<ValidationResult>();

            var structureSet = context.StructureSet;
            if (structureSet == null)
                return results;

            ValidateSmallVolumeResolution(structureSet.Structures, results);

            return results;
        }

        private void ValidateSmallVolumeResolution(IEnumerable<Structure> structures, List<ValidationResult> results)
        {
            var structureList = structures.ToList();
            int initialCount = results.Count;
            int checkedPtvs = 0;
            int ptvCountBelow10cc = 0;
            double? smallestPtvVolume = null;

            foreach (var ptv in structureList.Where(s =>
                s.Id.StartsWith("PTV", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(s.DicomType, "MARKER", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(s.DicomType, "SUPPORT", StringComparison.OrdinalIgnoreCase)))
            {
                checkedPtvs++;
                double volume = ptv.Volume;

                // Track smallest PTV for info message
                if (!smallestPtvVolume.HasValue || volume < smallestPtvVolume.Value)
                    smallestPtvVolume = volume;

                // Count PTVs below 10cc threshold for summary message
                if (volume < _config.HighResVolumeThresholdCc)
                    ptvCountBelow10cc++;

                // Find related target structures by matching suffix
                string suffix = ValidationHelpers.GetTargetSuffix(ptv.Id, "PTV");
                var linkedTargets = new List<Structure>();

                var matchingCtv = structureList.FirstOrDefault(s =>
                    s.Id.StartsWith("CTV", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(ValidationHelpers.GetTargetSuffix(s.Id, "CTV"), suffix, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(s.DicomType, "MARKER", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(s.DicomType, "SUPPORT", StringComparison.OrdinalIgnoreCase));
                if (matchingCtv != null) linkedTargets.Add(matchingCtv);

                var matchingGtv = structureList.FirstOrDefault(s =>
                    s.Id.StartsWith("GTV", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(ValidationHelpers.GetTargetSuffix(s.Id, "GTV"), suffix, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(s.DicomType, "MARKER", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(s.DicomType, "SUPPORT", StringComparison.OrdinalIgnoreCase));
                if (matchingGtv != null) linkedTargets.Add(matchingGtv);

                // Check if all related targets use high resolution
                bool allHighRes = new[] { ptv }.Concat(linkedTargets).All(s => s.IsHighResolution);

                // Critical error for very small targets (<5cc) without high resolution
                if (volume < _config.HighResCriticalThresholdCc)
                {
                    if (!allHighRes)
                    {
                        results.Add(CreateResult(
                            "Target Resolution",
                            $"PTV '{ptv.Id}' volume is {volume:F1} cc (<{_config.HighResCriticalThresholdCc} cc) and not all related targets use high resolution.",
                            ValidationSeverity.Error));
                    }
                }
                // Warning for small targets (5-10cc) without high resolution
                else if (volume < _config.HighResVolumeThresholdCc && !allHighRes)
                {
                    results.Add(CreateResult(
                        "Target Resolution",
                        $"PTV '{ptv.Id}' volume is {volume:F1} cc (<{_config.HighResVolumeThresholdCc} cc) and related targets are not all high resolution.",
                        ValidationSeverity.Warning));
                }
            }

            // Add summary if all checked PTVs passed
            if (results.Count == initialCount && checkedPtvs > 0)
            {
                string message;
                if (ptvCountBelow10cc > 0)
                {
                    // If there are PTVs <10cc
                    message = $"All {checkedPtvs} PTV(s) checked, {ptvCountBelow10cc} PTV(s) have volume <10 cc, smallest PTV: {smallestPtvVolume:F1} cc.";
                }
                else
                {
                    // If all PTVs are ≥10cc
                    message = $"All {checkedPtvs} PTV(s) checked, smallest PTV: {smallestPtvVolume:F1} cc.";
                }

                results.Add(CreateResult(
                    "Target Resolution",
                    message,
                    ValidationSeverity.Info));
            }
        }
    }
}
