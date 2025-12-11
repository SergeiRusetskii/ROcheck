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
        /// Retrieves clinical goals from the plan using multiple access methods for cross-version compatibility.
        /// Tries 5 different approaches to handle variations across Eclipse API versions.
        /// Returns unique goals using HashSet to prevent duplicates.
        /// </summary>
        /// <param name="plan">The plan setup containing clinical goals</param>
        /// <returns>Enumerable collection of clinical goal objects</returns>
        public static IEnumerable<object> GetClinicalGoals(PlanSetup plan)
        {
            if (plan == null)
                yield break;

            var seen = new HashSet<object>();

            foreach (var goal in GetClinicalGoalsFromProperty(plan))
            {
                if (seen.Add(goal))
                    yield return goal;
            }

            foreach (var goal in GetClinicalGoalsFromMethod(plan, "GetClinicalGoals"))
            {
                if (seen.Add(goal))
                    yield return goal;
            }

            foreach (var goal in GetClinicalGoalsFromMethod(plan, "GetPlanningGoals"))
            {
                if (seen.Add(goal))
                    yield return goal;
            }

            foreach (var goal in GetClinicalGoalsFromMethod(plan, "GetGoals"))
            {
                if (seen.Add(goal))
                    yield return goal;
            }

            foreach (var goal in GetClinicalGoalsFromCourse(plan))
            {
                if (seen.Add(goal))
                    yield return goal;
            }
        }

        private static IEnumerable<object> GetClinicalGoalsFromProperty(PlanSetup plan)
        {
            var property = plan.GetType().GetProperty("ClinicalGoals");
            if (property?.GetValue(plan) is System.Collections.IEnumerable enumerable)
            {
                foreach (var goal in enumerable)
                    yield return goal;
            }
        }

        private static IEnumerable<object> GetClinicalGoalsFromMethod(PlanSetup plan, string methodName)
        {
            var method = plan.GetType().GetMethod(methodName, Type.EmptyTypes);
            if (method?.Invoke(plan, null) is System.Collections.IEnumerable enumerable)
            {
                foreach (var goal in enumerable)
                    yield return goal;
            }
        }

        private static IEnumerable<object> GetClinicalGoalsFromCourse(PlanSetup plan)
        {
            var course = plan.Course;
            if (course == null)
                yield break;

            var property = course.GetType().GetProperty("ClinicalGoals");
            if (property?.GetValue(course) is System.Collections.IEnumerable enumerable)
            {
                foreach (var goal in enumerable)
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
        /// Extracts target structure IDs from the plan's dose prescription.
        /// Used for prescription-aware validation to distinguish active treatment targets
        /// from evaluation/backup structures.
        /// </summary>
        /// <param name="plan">The plan setup containing prescription information</param>
        /// <returns>HashSet of structure IDs that are prescription targets</returns>
        public static HashSet<string> GetPrescriptionTargetIds(PlanSetup plan)
        {
            var targetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (plan?.RTPrescription != null)
            {
                try
                {
                    // Iterate through prescription targets to extract structure IDs
                    foreach (var target in plan.RTPrescription.Targets)
                    {
                        // Use reflection to get TargetId property (handles API variations)
                        var targetId = GetPropertyValue(target, "TargetId") as string;
                        if (!string.IsNullOrEmpty(targetId))
                        {
                            targetIds.Add(targetId);
                        }
                    }
                }
                catch
                {
                    // If prescription access fails, return empty set (all targets will be excluded)
                }
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
    }
}
