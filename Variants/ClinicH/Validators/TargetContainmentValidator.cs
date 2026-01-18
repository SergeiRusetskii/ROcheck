using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;

namespace ROcheck.Validators
{
    /// <summary>
    /// Validates that GTV and CTV volumes are fully contained within their corresponding PTV volumes.
    /// Matches structures by suffix (e.g., PTV_59.4 should contain CTV_59.4 and GTV_59.4).
    /// Uses voxel-based spatial overlap detection.
    /// </summary>
    public class TargetContainmentValidator : ValidatorBase
    {
        public override IEnumerable<ValidationResult> Validate(ScriptContext context)
        {
            var results = new List<ValidationResult>();

            var structureSet = context.StructureSet;
            if (structureSet?.Image == null)
                return results;

            ValidateTargetContainment(structureSet.Structures, structureSet.Image, results);

            return results;
        }

        private void ValidateTargetContainment(IEnumerable<Structure> structures, Image image, List<ValidationResult> results)
        {
            var structureList = structures.ToList();
            if (!structureList.Any())
                return;

            int initialCount = results.Count;
            int checkedPairs = 0;

            // For each PTV, check if corresponding CTV and GTV are fully contained
            // Exclude MARKER and SUPPORT types
            foreach (var ptv in structureList.Where(s =>
                s.Id.StartsWith("PTV", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(s.DicomType, "MARKER", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(s.DicomType, "SUPPORT", StringComparison.OrdinalIgnoreCase)))
            {
                // Extract suffix from PTV name (e.g., "59.4" from "PTV_59.4")
                string suffix = ValidationHelpers.GetTargetSuffix(ptv.Id, "PTV");

                // Check both CTV and GTV for this PTV
                foreach (var prefix in new[] { "CTV", "GTV" })
                {
                    // Find matching target by prefix and suffix, exclude MARKER/SUPPORT types
                    var target = structureList.FirstOrDefault(s =>
                        s.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(ValidationHelpers.GetTargetSuffix(s.Id, prefix), suffix, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(s.DicomType, "MARKER", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(s.DicomType, "SUPPORT", StringComparison.OrdinalIgnoreCase));

                    if (target != null)
                    {
                        checkedPairs++;
                        // Verify target is fully contained within PTV
                        if (!ValidationHelpers.IsStructureContained(target, ptv, image))
                        {
                            results.Add(CreateResult(
                                "Target Containment",
                                $"{prefix} '{target.Id}' extends outside PTV '{ptv.Id}'.",
                                ValidationSeverity.Error));
                        }
                    }
                }
            }

            // Add summary if all checked pairs passed
            if (results.Count == initialCount && checkedPairs > 0)
            {
                results.Add(CreateResult(
                    "Target Containment",
                    $"All {checkedPairs} target volume(s) are properly contained within their PTVs.",
                    ValidationSeverity.Info));
            }
        }
    }
}
