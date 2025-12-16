using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;

namespace ROcheck.Validators
{
    /// <summary>
    /// Validates that all applicable structures have at least one associated clinical goal.
    /// Uses prescription-aware filtering to only check active treatment targets.
    /// </summary>
    public class ClinicalGoalsCoverageValidator : ValidatorBase
    {
        private static readonly HashSet<string> ExcludedStructures = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Bones", "CouchInterior", "CouchSurface", "Clips", "Scar_Wire", "Sternum"
        };

        public override IEnumerable<ValidationResult> Validate(ScriptContext context)
        {
            var results = new List<ValidationResult>();

            var plan = context.PlanSetup;
            var structureSet = context.StructureSet;

            if (plan == null || structureSet == null)
                return results;

            // Retrieve clinical goals using multiple access methods for compatibility across ESAPI versions
            var clinicalGoals = ValidationHelpers.GetClinicalGoals(plan).ToList();

            // Build a lookup dictionary mapping structure IDs to their associated clinical goals
            var structureGoals = ValidationHelpers.BuildStructureGoalLookup(clinicalGoals);

            // Extract target structure IDs from prescription for smart target filtering
            var prescriptionTargetIds = ValidationHelpers.GetReviewedPrescriptionTargetIds(plan, out bool hasReviewedPrescriptions);

            // DEBUG: Output prescription information
            DebugPrescriptions(plan, results);

            ValidateClinicalGoalPresence(structureSet.Structures, structureGoals, prescriptionTargetIds, hasReviewedPrescriptions, results);

            return results;
        }

        private void ValidateClinicalGoalPresence(IEnumerable<Structure> structures,
            Dictionary<string, List<object>> structureGoals,
            HashSet<string> prescriptionTargetIds,
            bool hasReviewedPrescriptions,
            List<ValidationResult> results)
        {
            int initialCount = results.Count;
            int checkedCount = 0;

            foreach (var structure in structures)
            {
                // Skip structures based on exclusion logic (support structures, non-prescription targets, etc.)
                if (ValidationHelpers.IsStructureExcluded(structure, prescriptionTargetIds, ExcludedStructures))
                    continue;

                checkedCount++;

                // Warn if a structure that should have goals doesn't have any
                if (!structureGoals.ContainsKey(structure.Id))
                {
                    results.Add(CreateResult(
                        "Clinical Goals existence",
                        $"Structure '{structure.Id}' has no associated clinical goal.",
                    ValidationSeverity.Warning));
                }
            }

            // Add note when no reviewed prescriptions were found (targets skipped)
            if (!hasReviewedPrescriptions)
            {
                results.Add(CreateResult(
                    "Clinical Goals existence",
                    "No 'Reviewed' prescriptions found; all target structures were skipped.",
                    ValidationSeverity.Info));
            }

            // Add informational summary if all checked structures passed validation
            if (results.Count == initialCount && checkedCount > 0)
            {
                results.Add(CreateResult(
                    "Clinical Goals existence",
                    $"All {checkedCount} applicable structures have associated clinical goals.",
                    ValidationSeverity.Info));
            }
            else if (checkedCount == 0)
            {
                results.Add(CreateResult(
                    "Clinical Goals existence",
                    $"No structures found requiring clinical goals (all are excluded).",
                    ValidationSeverity.Info));
            }
        }

        private void DebugPrescriptions(PlanSetup plan, List<ValidationResult> results)
        {
            var course = plan?.Course;
            if (course == null)
            {
                results.Add(CreateResult(
                    "Clinical Goals existence",
                    "DEBUG: Course is null",
                    ValidationSeverity.Info));
                return;
            }

            results.Add(CreateResult(
                "Clinical Goals existence",
                $"DEBUG: Course = {course.Id}",
                ValidationSeverity.Info));

            // Try to get prescriptions using reflection
            int prescriptionCount = 0;
            var prescriptionsList = new System.Text.StringBuilder();

            // Try RTPrescriptions property
            var rtPrescriptionsProperty = course.GetType().GetProperty("RTPrescriptions");
            if (rtPrescriptionsProperty != null)
            {
                prescriptionsList.AppendLine("DEBUG: Found RTPrescriptions property");
                var rxCollection = rtPrescriptionsProperty.GetValue(course);
                if (rxCollection is System.Collections.IEnumerable enumerable)
                {
                    foreach (var rx in enumerable)
                    {
                        prescriptionCount++;
                        var rxInfo = GetPrescriptionDebugInfo(rx, prescriptionCount);
                        prescriptionsList.AppendLine(rxInfo);
                    }
                }
                else
                {
                    prescriptionsList.AppendLine("DEBUG: RTPrescriptions value is not enumerable");
                }
            }
            else
            {
                prescriptionsList.AppendLine("DEBUG: RTPrescriptions property NOT found");
            }

            // Try Prescriptions property
            var prescriptionsProperty = course.GetType().GetProperty("Prescriptions");
            if (prescriptionsProperty != null)
            {
                prescriptionsList.AppendLine("DEBUG: Found Prescriptions property");
                var rxCollection = prescriptionsProperty.GetValue(course);
                if (rxCollection is System.Collections.IEnumerable enumerable)
                {
                    foreach (var rx in enumerable)
                    {
                        prescriptionCount++;
                        var rxInfo = GetPrescriptionDebugInfo(rx, prescriptionCount);
                        prescriptionsList.AppendLine(rxInfo);
                    }
                }
                else
                {
                    prescriptionsList.AppendLine("DEBUG: Prescriptions value is not enumerable");
                }
            }
            else
            {
                prescriptionsList.AppendLine("DEBUG: Prescriptions property NOT found");
            }

            results.Add(CreateResult(
                "Clinical Goals existence",
                $"DEBUG: Total prescriptions found: {prescriptionCount}\n{prescriptionsList}",
                ValidationSeverity.Info));
        }

        private string GetPrescriptionDebugInfo(object prescription, int index)
        {
            if (prescription == null)
                return $"Rx #{index}: NULL";

            var info = new System.Text.StringBuilder();
            info.AppendLine($"Rx #{index}:");

            var type = prescription.GetType();
            info.AppendLine($"  Type: {type.FullName}");

            // Get all properties
            var properties = type.GetProperties();
            foreach (var prop in properties)
            {
                try
                {
                    var value = prop.GetValue(prescription);

                    // Special handling for Targets collection
                    if (prop.Name == "Targets" && value is System.Collections.IEnumerable targets)
                    {
                        var targetsList = new System.Text.StringBuilder();
                        int targetCount = 0;
                        foreach (var target in targets)
                        {
                            targetCount++;
                            var targetId = ValidationHelpers.GetPropertyValue(target, "TargetId");
                            targetsList.Append($"{targetId}, ");
                        }
                        info.AppendLine($"  {prop.Name} ({targetCount}): {targetsList}");
                    }
                    else
                    {
                        info.AppendLine($"  {prop.Name} = {value ?? "NULL"}");
                    }
                }
                catch (Exception ex)
                {
                    info.AppendLine($"  {prop.Name} = ERROR: {ex.Message}");
                }
            }

            return info.ToString();
        }
    }
}
