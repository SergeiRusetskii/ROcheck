using System;
using System.Collections.Generic;
using VMS.TPS.Common.Model.API;

namespace ROcheck.Validators
{
    /// <summary>
    /// Validates that target structures have the correct DICOM structure type set.
    /// Structures named PTV_* should have DicomType="PTV", CTV_* should be "CTV", etc.
    /// Correct DICOM typing ensures proper structure recognition by planning systems.
    /// </summary>
    public class StructureTypesValidator : ValidatorBase
    {
        public override IEnumerable<ValidationResult> Validate(ScriptContext context)
        {
            var results = new List<ValidationResult>();

            var structureSet = context.StructureSet;
            if (structureSet == null)
                return results;

            ValidateTargetStructureTypes(structureSet.Structures, results);

            return results;
        }

        private void ValidateTargetStructureTypes(IEnumerable<Structure> structures, List<ValidationResult> results)
        {
            int initialCount = results.Count;
            int checkedStructures = 0;

            foreach (var structure in structures)
            {
                // Skip MARKER and SUPPORT type structures
                if (string.Equals(structure.DicomType, "MARKER", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(structure.DicomType, "SUPPORT", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Check PTV structures have PTV DICOM type
                if (structure.Id.StartsWith("PTV", StringComparison.OrdinalIgnoreCase))
                {
                    checkedStructures++;
                    if (!string.Equals(structure.DicomType, "PTV", StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(CreateResult(
                            "Structure Types",
                            $"Structure '{structure.Id}' should be of type PTV but is '{structure.DicomType}'.",
                            ValidationSeverity.Error));
                    }
                }

                // Check CTV structures have CTV DICOM type
                if (structure.Id.StartsWith("CTV", StringComparison.OrdinalIgnoreCase))
                {
                    checkedStructures++;
                    if (!string.Equals(structure.DicomType, "CTV", StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(CreateResult(
                            "Structure Types",
                            $"Structure '{structure.Id}' should be of type CTV but is '{structure.DicomType}'.",
                            ValidationSeverity.Error));
                    }
                }

                // Check GTV structures have GTV DICOM type
                if (structure.Id.StartsWith("GTV", StringComparison.OrdinalIgnoreCase))
                {
                    checkedStructures++;
                    if (!string.Equals(structure.DicomType, "GTV", StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(CreateResult(
                            "Structure Types",
                            $"Structure '{structure.Id}' should be of type GTV but is '{structure.DicomType}'.",
                            ValidationSeverity.Error));
                    }
                }
            }

            // Add summary if all checked structures passed
            if (results.Count == initialCount && checkedStructures > 0)
            {
                results.Add(CreateResult(
                    "Structure Types",
                    $"All {checkedStructures} target structure(s) have correct DICOM types (PTV/CTV/GTV).",
                    ValidationSeverity.Info));
            }
        }
    }
}
