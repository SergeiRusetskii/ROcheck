using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace ROcheck
{
    /// <summary>
    /// Static helper methods for validation operations.
    /// </summary>
    public static class ValidationHelpers
    {
        /// <summary>
        /// Retrieves clinical goals from the plan using the documented ESAPI.
        /// Uses PlanningItem.GetClinicalGoals() method from ESAPI documentation.
        /// Reference: VMS.TPS.Common.Model.API.xml - M:VMS.TPS.Common.Model.API.PlanningItem.GetClinicalGoals
        /// </summary>
        /// <param name="plan">The plan setup containing clinical goals</param>
        /// <returns>Enumerable collection of clinical goal objects</returns>
        public static IEnumerable<object> GetClinicalGoals(PlanSetup plan)
        {
            if (plan == null)
                yield break;

            // Use documented API method: PlanningItem.GetClinicalGoals()
            // PlanSetup inherits from PlanningItem
            var goals = plan.GetClinicalGoals();
            if (goals != null)
            {
                foreach (var goal in goals)
                    yield return goal;
            }
        }


        /// <summary>
        /// Builds a dictionary mapping structure IDs to their associated clinical goals.
        /// Enables fast lookup of all goals for a given structure during validation.
        /// </summary>
        /// <param name="clinicalGoals">Collection of clinical goal objects from the plan</param>
        /// <returns>Dictionary with structure IDs as keys and lists of goals as values</returns>
        public static Dictionary<string, List<object>> BuildStructureGoalLookup(IEnumerable<object> clinicalGoals)
        {
            var lookup = new Dictionary<string, List<object>>(StringComparer.OrdinalIgnoreCase);

            foreach (var goal in clinicalGoals)
            {
                // Extract structure ID from goal object using reflection
                var structureId = GetStructureId(goal);
                if (string.IsNullOrWhiteSpace(structureId))
                    continue;

                // Add goal to the list for this structure (create list if first goal)
                if (!lookup.TryGetValue(structureId, out var list))
                {
                    list = new List<object>();
                    lookup[structureId] = list;
                }

                list.Add(goal);
            }

            return lookup;
        }

        public static string GetStructureId(object goal)
        {
            // Try various property names for structure identification
            var structureId = GetPropertyValue(goal, "StructureId") as string;
            if (!string.IsNullOrWhiteSpace(structureId))
                return structureId;

            // Try getting Structure object directly
            var structureObj = GetPropertyValue(goal, "Structure");
            if (structureObj is Structure structure)
                return structure.Id;

            // Try StructureName property
            var structureName = GetPropertyValue(goal, "StructureName") as string;
            if (!string.IsNullOrWhiteSpace(structureName))
                return structureName;

            // Try Name property
            var name = GetPropertyValue(goal, "Name") as string;
            if (!string.IsNullOrWhiteSpace(name))
                return name;

            return null;
        }

        /// <summary>
        /// Determines if a clinical goal is a lower bound (minimum dose) constraint.
        /// Checks ObjectiveAsString for presence of ≥ (U+2265) or > which indicates
        /// "at least" constraints typical of target minimum dose goals.
        /// Examples: "D 95.5 % ≥ 56.43 Gy", "V 50.50 Gy > 95.0 %"
        /// </summary>
        public static bool IsLowerGoal(object goal)
        {
            var objectiveString = GetPropertyValue(goal, "ObjectiveAsString")?.ToString();

            if (string.IsNullOrWhiteSpace(objectiveString))
                return false;

            // Lower goals use ≥ (Unicode U+2265) or > operators (at least / greater than)
            // Also check for ASCII ">=" as fallback
            return objectiveString.Contains("≥") ||
                   objectiveString.Contains(">") ||
                   objectiveString.Contains(">=");
        }

        /// <summary>
        /// Determines if a clinical goal is a Dmax (maximum dose) constraint.
        /// Checks MeasureType for any "Max" type AND ObjectiveAsString contains "Dmax".
        /// Example: MeasureType=MeasureTypeGoalMax or MeasureTypeDoseMax, ObjectiveAsString="Dmax < 54.00 Gy"
        /// </summary>
        public static bool IsDmaxGoal(object goal)
        {
            var measureType = GetPropertyValue(goal, "MeasureType")?.ToString();
            var objectiveString = GetPropertyValue(goal, "ObjectiveAsString")?.ToString();

            if (string.IsNullOrWhiteSpace(objectiveString))
                return false;

            // Check if ObjectiveAsString contains "Dmax" (case insensitive)
            // This is the most reliable indicator of a maximum dose constraint
            bool isDmax = objectiveString.IndexOf("Dmax", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          objectiveString.IndexOf("D max", StringComparison.OrdinalIgnoreCase) >= 0;

            // Optional: Also verify MeasureType contains "Max" for additional validation
            // But prioritize the ObjectiveAsString check as it's more explicit
            if (isDmax)
                return true;

            // Fallback: If MeasureType contains "Max" and objectiveString has < or ≤
            bool hasMaxMeasure = measureType != null && measureType.IndexOf("Max", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasLessOperator = objectiveString.Contains("<") || objectiveString.Contains("≤");

            return hasMaxMeasure && hasLessOperator;
        }

        /// <summary>
        /// Extracts the dose value in Gy from a clinical goal object.
        /// First attempts to extract from Objective property, then falls back to
        /// parsing ObjectiveAsString for dose values.
        /// Handles formats like "D 95.5 % >= 56.43 Gy", "Dmax < 54.00 Gy", "V 50.50 Gy >= 95.0 %"
        /// </summary>
        public static double? GetGoalDoseGy(object goal, PlanSetup plan)
        {
            // First try to get Objective property and extract dose from it
            var objective = GetPropertyValue(goal, "Objective");
            if (objective != null)
            {
                // Try to get DoseValue from Objective
                var doseProperty = objective.GetType().GetProperty("DoseValue");
                if (doseProperty != null)
                {
                    var doseValue = doseProperty.GetValue(objective);
                    if (doseValue is DoseValue dv)
                        return NormalizeDoseValueGy(dv, plan);
                }

                // Try other potential dose properties on Objective
                var doseAltProperty = objective.GetType().GetProperty("Dose");
                if (doseAltProperty != null)
                {
                    var doseAlt = doseAltProperty.GetValue(objective);
                    if (doseAlt is DoseValue dvAlt)
                        return NormalizeDoseValueGy(dvAlt, plan);
                }
            }

            // Fall back to parsing ObjectiveAsString
            var objectiveString = GetPropertyValue(goal, "ObjectiveAsString")?.ToString();
            if (string.IsNullOrWhiteSpace(objectiveString))
                return null;

            return ParseDoseFromObjectiveString(objectiveString, plan);
        }

        /// <summary>
        /// Parses dose value from ObjectiveAsString.
        /// Examples: "D 95.5 % >= 56.43 Gy" → 56.43
        ///           "Dmax < 54.00 Gy" → 54.00
        ///           "V 50.50 Gy >= 95.0 %" → 50.50
        /// </summary>
        public static double? ParseDoseFromObjectiveString(string objectiveString, PlanSetup plan)
        {
            if (string.IsNullOrWhiteSpace(objectiveString))
                return null;

            // Use regex to find numeric values followed by dose units
            var gyMatch = System.Text.RegularExpressions.Regex.Match(
                objectiveString,
                @"(\d+\.?\d*)\s*(Gy|cGy)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (gyMatch.Success)
            {
                if (double.TryParse(gyMatch.Groups[1].Value, out double dose))
                {
                    var unit = gyMatch.Groups[2].Value;

                    // Convert cGy to Gy if needed
                    if (unit.Equals("cGy", StringComparison.OrdinalIgnoreCase))
                        return dose / 100.0;

                    return dose;
                }
            }

            // Try to find percentage values and convert to Gy
            var percentMatch = System.Text.RegularExpressions.Regex.Match(
                objectiveString,
                @"(\d+\.?\d*)\s*%");

            if (percentMatch.Success)
            {
                if (double.TryParse(percentMatch.Groups[1].Value, out double percent))
                {
                    return ConvertPercentToGy(percent, plan);
                }
            }

            return null;
        }

        public static double? NormalizeDoseValueGy(DoseValue doseValue, PlanSetup plan = null)
        {
            var unitText = doseValue.Unit.ToString();

            if (unitText.Equals("Gy", StringComparison.OrdinalIgnoreCase))
                return doseValue.Dose;

            if (unitText.Equals("cGy", StringComparison.OrdinalIgnoreCase))
                return doseValue.Dose / 100.0;

            if (unitText.IndexOf("Percent", StringComparison.OrdinalIgnoreCase) >= 0)
                return ConvertPercentToGy(doseValue.Dose, plan);

            var presentationProperty = typeof(DoseValue).GetProperty("Presentation");
            if (presentationProperty != null)
            {
                var presentationValue = presentationProperty.GetValue(doseValue)?.ToString();
                if (presentationValue?.IndexOf("Relative", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ConvertPercentToGy(doseValue.Dose, plan);
            }

            return doseValue.Dose;
        }

        public static double? ConvertPercentToGy(double percentValue, PlanSetup plan)
        {
            var totalDoseGy = GetPlanTotalDoseGy(plan);
            if (!totalDoseGy.HasValue)
                return null;

            return totalDoseGy.Value * percentValue / 100.0;
        }

        public static double? GetPlanTotalDoseGy(PlanSetup plan)
        {
            var totalDose = plan?.TotalDose;
            if (totalDose == null)
                return null;

            var unitText = totalDose.Value.Unit.ToString();

            if (unitText.Equals("Gy", StringComparison.OrdinalIgnoreCase))
                return totalDose.Value.Dose;

            if (unitText.Equals("cGy", StringComparison.OrdinalIgnoreCase))
                return totalDose.Value.Dose / 100.0;

            return null;
        }

        public static object GetPropertyValue(object obj, string propertyName)
        {
            var property = obj?.GetType().GetProperty(propertyName);
            return property?.GetValue(obj);
        }

        /// <summary>
        /// Extracts target structure IDs from ALL reviewed prescriptions in the course.
        /// Uses documented ESAPI path: Course → TreatmentPhases → Prescriptions
        /// Reference: VMS.TPS.Common.Model.API.xml - P:VMS.TPS.Common.Model.API.Course.TreatmentPhases
        /// Reference: VMS.TPS.Common.Model.API.xml - P:VMS.TPS.Common.Model.API.TreatmentPhase.Prescriptions
        /// Reference: VMS.TPS.Common.Model.API.xml - T:VMS.TPS.Common.Model.API.RTPrescription
        /// Reference: VMS.TPS.Common.Model.API.xml - T:VMS.TPS.Common.Model.API.RTPrescriptionTarget
        /// </summary>
        /// <param name="plan">The plan setup containing course information</param>
        /// <param name="hasReviewedPrescriptions">Returns true if any reviewed prescription was found</param>
        /// <returns>HashSet of structure IDs that are prescription targets</returns>
        public static HashSet<string> GetReviewedPrescriptionTargetIds(PlanSetup plan, out bool hasReviewedPrescriptions)
        {
            var targetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            hasReviewedPrescriptions = false;

            if (plan?.Course == null)
                return targetIds;

            try
            {
                // Use documented API: Course.TreatmentPhases property
                var treatmentPhases = plan.Course.TreatmentPhases;
                if (treatmentPhases == null)
                    return targetIds;

                // Iterate through all treatment phases in the course
                foreach (var phase in treatmentPhases)
                {
                    // Use documented API: TreatmentPhase.Prescriptions property
                    var prescriptions = phase.Prescriptions;
                    if (prescriptions == null)
                        continue;

                    // Check each prescription in this phase
                    foreach (var prescription in prescriptions)
                    {
                        // Check if prescription status is "Reviewed"
                        var status = prescription.Status?.ToString();
                        if (!string.Equals(status, "Reviewed", StringComparison.OrdinalIgnoreCase))
                            continue;

                        hasReviewedPrescriptions = true;

                        // Get targets from prescription using documented API
                        var targets = prescription.Targets;
                        if (targets != null)
                        {
                            foreach (var target in targets)
                            {
                                // Use documented API: RTPrescriptionTarget.TargetId property
                                var targetId = target.TargetId;
                                if (!string.IsNullOrEmpty(targetId))
                                {
                                    targetIds.Add(targetId);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // If prescription access fails, return empty set (all targets will be excluded)
            }

            return targetIds;
        }

        /// <summary>
        /// Extracts the suffix from a target structure ID after removing the prefix.
        /// Examples:
        ///   - "PTV_59.4" with prefix "PTV" returns "_59.4"
        ///   - "PTV59.4" with prefix "PTV" returns "59.4"
        ///   - "PTV1" with prefix "PTV" returns "1"
        /// Used to match related structures (PTV_59.4, CTV_59.4, GTV_59.4 OR PTV1, CTV1, GTV1).
        /// </summary>
        /// <param name="structureId">The full structure ID</param>
        /// <param name="prefix">The prefix to remove (PTV, CTV, GTV)</param>
        /// <returns>The suffix after the prefix (remainder of the string)</returns>
        public static string GetTargetSuffix(string structureId, string prefix)
        {
            if (!structureId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return structureId;

            return structureId.Substring(prefix.Length);
        }

        /// <summary>
        /// Extracts a representative dose value from a target structure's clinical goals.
        /// Uses the same logic as ValidateTargetsOverlappingOars: finds lower goals (≥ or >)
        /// which represent minimum dose constraints typical of target prescriptions.
        /// Used for SIB (Simultaneously Integrated Boost) detection.
        /// </summary>
        /// <param name="goals">List of clinical goals for a target structure</param>
        /// <param name="plan">The plan setup for dose normalization</param>
        /// <returns>Representative dose in Gy from lower goal, or null if no lower goals found</returns>
        public static double? ExtractTargetDose(List<object> goals, PlanSetup plan)
        {
            if (goals == null || !goals.Any())
                return null;

            // Use the same logic as ValidateTargetsOverlappingOars:
            // Find lower goals (minimum dose/volume constraints) which represent target prescription
            var lowerGoalDose = goals
                .Where(IsLowerGoal)
                .Select(goal => GetGoalDoseGy(goal, plan))
                .FirstOrDefault(d => d.HasValue);

            return lowerGoalDose;
        }

        /// <summary>
        /// Determines if a clinical goal uses percentage dose units.
        /// Distinguishes between dose in percentage (NOT allowed in SIB) vs volume in percentage (allowed).
        /// Examples:
        ///   - "D 95% ≥ 56%" → true (dose in percent, BAD in SIB)
        ///   - "D 95% ≥ 56.43 Gy" → false (dose in Gy, OK)
        ///   - "V 50 Gy ≥ 95%" → false (volume percent is OK, dose in Gy)
        /// </summary>
        /// <param name="goal">The clinical goal to check</param>
        /// <returns>True if the goal's dose value is expressed in percentage</returns>
        public static bool HasPercentageDose(object goal)
        {
            var objectiveString = GetPropertyValue(goal, "ObjectiveAsString")?.ToString();
            if (string.IsNullOrWhiteSpace(objectiveString))
                return false;

            // Look for patterns like "≥ 56%" or "> 95%" or "< 54.5%"
            // Pattern: operator followed by number and %
            var percentDosePattern = @"[<>≤≥]\s*\d+\.?\d*\s*%";
            var match = System.Text.RegularExpressions.Regex.Match(objectiveString, percentDosePattern);

            if (!match.Success)
                return false;

            // Additional check: Make sure it's dose, not volume
            // If string starts with "V " it's volume constraint (OK to have %)
            if (objectiveString.StartsWith("V ", StringComparison.OrdinalIgnoreCase))
                return false; // Volume percentage is OK

            return true; // Found dose in percentage
        }

        /// <summary>
        /// Checks if one structure is fully contained within another structure using voxel-based sampling.
        /// Samples points within the inner structure and verifies all are inside the outer structure.
        /// </summary>
        /// <param name="inner">The structure that should be contained</param>
        /// <param name="outer">The structure that should contain the inner structure</param>
        /// <param name="image">The image for coordinate system and sampling resolution</param>
        /// <returns>True if inner is fully contained within outer, false if any voxel extends outside</returns>
        public static bool IsStructureContained(Structure inner, Structure outer, Image image)
        {
            if (inner == null || outer == null || inner.IsEmpty || outer.IsEmpty)
                return true;

            // Structures must have segment models for spatial containment checks
            if (!inner.HasSegment || !outer.HasSegment)
                return true;

            if (image == null)
                return true;

            int sampleStep = Math.Max(1, image.XSize / 120);
            int zStep = Math.Max(1, image.ZSize / 60);
            var origin = image.Origin;
            var buffer = new int[image.XSize, image.YSize];

            for (int z = 0; z < image.ZSize; z += zStep)
            {
                image.GetVoxels(z, buffer);

                if (!inner.GetContoursOnImagePlane(z).Any())
                    continue;

                double zPos = origin.z + z * image.ZRes;
                for (int x = 0; x < image.XSize; x += sampleStep)
                {
                    double xPos = origin.x + x * image.XRes;
                    for (int y = 0; y < image.YSize; y += sampleStep)
                    {
                        double yPos = origin.y + y * image.YRes;
                        var point = new VVector(xPos, yPos, zPos);
                        if (inner.IsPointInsideSegment(point) && !outer.IsPointInsideSegment(point))
                            return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if two structures spatially overlap using voxel-based sampling.
        /// Samples points and looks for any voxel that is inside both structures simultaneously.
        /// </summary>
        /// <param name="a">First structure</param>
        /// <param name="b">Second structure</param>
        /// <param name="image">The image for coordinate system and sampling resolution</param>
        /// <returns>True if structures overlap, false otherwise</returns>
        public static bool StructuresOverlap(Structure a, Structure b, Image image)
        {
            if (a == null || b == null || a.IsEmpty || b.IsEmpty)
                return false;

            // Structures must have segment models for spatial overlap checks
            if (!a.HasSegment || !b.HasSegment)
                return false;

            if (image == null)
                return false;

            int sampleStep = Math.Max(1, image.XSize / 120);
            int zStep = Math.Max(1, image.ZSize / 60);
            var origin = image.Origin;
            var buffer = new int[image.XSize, image.YSize];

            for (int z = 0; z < image.ZSize; z += zStep)
            {
                image.GetVoxels(z, buffer);

                if (!a.GetContoursOnImagePlane(z).Any() || !b.GetContoursOnImagePlane(z).Any())
                    continue;

                double zPos = origin.z + z * image.ZRes;
                for (int x = 0; x < image.XSize; x += sampleStep)
                {
                    double xPos = origin.x + x * image.XRes;
                    for (int y = 0; y < image.YSize; y += sampleStep)
                    {
                        double yPos = origin.y + y * image.YRes;
                        var point = new VVector(xPos, yPos, zPos);
                        if (a.IsPointInsideSegment(point) && b.IsPointInsideSegment(point))
                            return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Determines if a structure should be excluded from clinical goal validation.
        /// Implements prescription-aware exclusion logic for target structures.
        ///
        /// Exclusion rules:
        /// 1. SUPPORT and MARKER structures (DICOM type = SUPPORT or MARKER)
        /// 2. Structures with specific patterns: z_*, *wire*, *Encompass*, *Enc Marker*, *Dose*, Implant*, Lymph*, LN_*
        /// 3. Structures in ExcludedStructures list (Bones, CouchInterior, Clips, Scar_Wire, Sternum)
        /// 4. GTV/CTV/PTV structures NOT in the dose prescription (evaluation/backup targets)
        /// </summary>
        public static bool IsStructureExcluded(Structure structure, HashSet<string> prescriptionTargetIds, HashSet<string> excludedStructures)
        {
            // Exclude structures with DICOM type 'SUPPORT' or 'MARKER'
            if (string.Equals(structure.DicomType, "SUPPORT", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(structure.DicomType, "MARKER", StringComparison.OrdinalIgnoreCase))
                return true;

            // Exclude structures with certain patterns in their names
            if (structure.Id.StartsWith("z_", StringComparison.OrdinalIgnoreCase) ||
                structure.Id.StartsWith("Implant", StringComparison.OrdinalIgnoreCase) ||
                structure.Id.StartsWith("Lymph", StringComparison.OrdinalIgnoreCase) ||
                structure.Id.StartsWith("LN_", StringComparison.OrdinalIgnoreCase) ||
                structure.Id.IndexOf("wire", StringComparison.OrdinalIgnoreCase) >= 0 ||
                structure.Id.IndexOf("Encompass", StringComparison.OrdinalIgnoreCase) >= 0 ||
                structure.Id.IndexOf("Enc Marker", StringComparison.OrdinalIgnoreCase) >= 0 ||
                structure.Id.IndexOf("Dose", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            // Prescription-aware exclusion for target volumes (GTV/CTV/PTV)
            // Only validate targets that are actually in the prescription
            if (structure.Id.StartsWith("GTV", StringComparison.OrdinalIgnoreCase) ||
                structure.Id.StartsWith("CTV", StringComparison.OrdinalIgnoreCase) ||
                structure.Id.StartsWith("PTV", StringComparison.OrdinalIgnoreCase))
            {
                // Exclude if NOT in prescription (evaluation or backup structures)
                return !prescriptionTargetIds.Contains(structure.Id);
            }

            // Check against explicit exclusion list
            return excludedStructures.Contains(structure.Id);
        }
    }
}
