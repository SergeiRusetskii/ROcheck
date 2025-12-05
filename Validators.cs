using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace ROcheck
{
    public enum ValidationSeverity
    {
        Error,
        Warning,
        Info
    }

    // Base validator class
    public abstract class ValidatorBase
    {
        public abstract IEnumerable<ValidationResult> Validate(ScriptContext context);

        protected ValidationResult CreateResult(string category, string message, ValidationSeverity severity, bool isFieldResult = false)
        {
            return new ValidationResult
            {
                Category = category,
                Message = message,
                Severity = severity,
                IsFieldResult = isFieldResult
            };
        }
    }

    // Composite validator that can contain child validators
    public abstract class CompositeValidator : ValidatorBase
    {
        protected List<ValidatorBase> Validators { get; } = new List<ValidatorBase>();

        public void AddValidator(ValidatorBase validator)
        {
            Validators.Add(validator);
        }

        public override IEnumerable<ValidationResult> Validate(ScriptContext context)
        {
            var results = new List<ValidationResult>();
            foreach (var validator in Validators)
            {
                results.AddRange(validator.Validate(context));
            }
            return results;
        }
    }

    // Root validator that coordinates all validation
    public class RootValidator : CompositeValidator
    {
        public RootValidator()
        {
            AddValidator(new ClinicalGoalsValidator());
        }
    }

    // Clinical Goals Validator - Core ROcheck functionality
    public class ClinicalGoalsValidator : ValidatorBase
    {
        private static readonly HashSet<string> ExcludedStructures = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Bones", "CouchInterior", "CouchSurface", "Clips", "Scar_Wire"
        };

        /// <summary>
        /// Main validation entry point. Orchestrates all clinical goal and structure validation checks.
        /// </summary>
        /// <param name="context">The Eclipse script context containing plan and structure set information</param>
        /// <returns>Collection of validation results with category, message, and severity</returns>
        public override IEnumerable<ValidationResult> Validate(ScriptContext context)
        {
            var results = new List<ValidationResult>();

            var plan = context.PlanSetup;
            var structureSet = context.StructureSet;

            // Ensure we have both a plan and structure set to validate
            if (plan == null || structureSet == null)
                return results;

            // Retrieve clinical goals using multiple access methods for compatibility across ESAPI versions
            var clinicalGoals = GetClinicalGoals(plan).ToList();

            // Build a lookup dictionary mapping structure IDs to their associated clinical goals
            var structureGoals = BuildStructureGoalLookup(clinicalGoals);

            // Extract target structure IDs from prescription for smart target filtering
            var prescriptionTargetIds = GetPrescriptionTargetIds(plan);

            // Execute all validation checks
            ValidateClinicalGoalPresence(structureSet.Structures, structureGoals, prescriptionTargetIds, results);
            ValidateTargetContainment(structureSet.Structures, structureSet.Image, results);
            ValidateTargetsOverlappingOars(structureSet.Structures, structureSet.Image, structureGoals, plan, results);
            ValidateSmallVolumeResolution(structureSet.Structures, results);
            ValidateTargetStructureTypes(structureSet.Structures, results);

            return results;
        }

        /// <summary>
        /// Validates that all applicable structures have at least one associated clinical goal.
        /// Uses prescription-aware filtering to only check active treatment targets.
        /// </summary>
        /// <param name="structures">Collection of structures in the structure set</param>
        /// <param name="structureGoals">Dictionary mapping structure IDs to their clinical goals</param>
        /// <param name="prescriptionTargetIds">Set of target IDs from the prescription</param>
        /// <param name="results">List to append validation results to</param>
        private static void ValidateClinicalGoalPresence(IEnumerable<Structure> structures,
            Dictionary<string, List<object>> structureGoals,
            HashSet<string> prescriptionTargetIds,
            List<ValidationResult> results)
        {
            int initialCount = results.Count;
            int checkedCount = 0;

            foreach (var structure in structures)
            {
                // Skip structures based on exclusion logic (support structures, non-prescription targets, etc.)
                if (IsStructureExcluded(structure, prescriptionTargetIds))
                    continue;

                checkedCount++;

                // Warn if a structure that should have goals doesn't have any
                if (!structureGoals.ContainsKey(structure.Id))
                {
                    results.Add(CreateResult(
                        "Structure Coverage",
                        $"Structure '{structure.Id}' has no associated clinical goal.",
                        ValidationSeverity.Warning));
                }
            }

            // Add informational summary if all checked structures passed validation
            if (results.Count == initialCount && checkedCount > 0)
            {
                results.Add(CreateResult(
                    "Structure Coverage",
                    $"All {checkedCount} applicable structures have associated clinical goals.",
                    ValidationSeverity.Info));
            }
            else if (checkedCount == 0)
            {
                results.Add(CreateResult(
                    "Structure Coverage",
                    $"No structures found requiring clinical goals (all are excluded or targets).",
                    ValidationSeverity.Info));
            }
        }

        /// <summary>
        /// Validates that GTV and CTV volumes are fully contained within their corresponding PTV volumes.
        /// Matches structures by suffix (e.g., PTV_59.4 should contain CTV_59.4 and GTV_59.4).
        /// Uses voxel-based spatial overlap detection.
        /// </summary>
        /// <param name="structures">Collection of structures to validate</param>
        /// <param name="image">Image context for spatial calculations</param>
        /// <param name="results">List to append validation results to</param>
        private static void ValidateTargetContainment(IEnumerable<Structure> structures, Image image, List<ValidationResult> results)
        {
            var structureList = structures.ToList();
            if (!structureList.Any())
                return;

            if (image == null)
                return;

            int initialCount = results.Count;
            int checkedPairs = 0;

            // For each PTV, check if corresponding CTV and GTV are fully contained
            foreach (var ptv in structureList.Where(s => s.Id.StartsWith("PTV", StringComparison.OrdinalIgnoreCase)))
            {
                // Extract suffix from PTV name (e.g., "59.4" from "PTV_59.4")
                string suffix = GetTargetSuffix(ptv.Id, "PTV");

                // Check both CTV and GTV for this PTV
                foreach (var prefix in new[] { "CTV", "GTV" })
                {
                    // Find matching target by prefix and suffix
                    var target = structureList.FirstOrDefault(s =>
                        s.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(GetTargetSuffix(s.Id, prefix), suffix, StringComparison.OrdinalIgnoreCase));

                    if (target != null)
                    {
                        checkedPairs++;
                        // Verify target is fully contained within PTV
                        if (!IsStructureContained(target, ptv, image))
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

        /// <summary>
        /// Validates that target volumes with lower dose goals don't overlap with OARs
        /// that have Dmax objectives below the target's lower goal dose.
        ///
        /// Optimized algorithm:
        /// 1. Find all structures with lower objectives (targets)
        /// 2. Find all OARs with Dmax goals
        /// 3. Create all pairs
        /// 4. Filter by dose comparison (target lower goal > OAR Dmax) - cheap operation
        /// 5. Check spatial overlap ONLY for dose-conflicting pairs - expensive operation
        /// </summary>
        private static void ValidateTargetsOverlappingOars(IEnumerable<Structure> structures,
            Image image,
            Dictionary<string, List<object>> structureGoals,
            PlanSetup plan,
            List<ValidationResult> results)
        {
            var structureList = structures.ToList();
            if (!structureList.Any())
                return;

            if (image == null)
                return;

            int initialCount = results.Count;

            // Step 1: Find all structures with lower objectives (targets)
            var targetsWithLowerGoals = new List<(Structure structure, double lowerGoalDose)>();

            foreach (var structure in structureList)
            {
                if (!structureGoals.TryGetValue(structure.Id, out var goals))
                    continue;

                var lowerGoalDose = goals
                    .Where(IsLowerGoal)
                    .Select(goal => GetGoalDoseGy(goal, plan))
                    .FirstOrDefault(d => d.HasValue);

                if (lowerGoalDose.HasValue)
                {
                    targetsWithLowerGoals.Add((structure, lowerGoalDose.Value));
                }
            }

            // Step 2: Find all OARs with Dmax goals
            var oarsWithDmax = new List<(Structure structure, double dmaxDose)>();

            foreach (var structure in structureList)
            {
                if (!structureGoals.TryGetValue(structure.Id, out var goals))
                    continue;

                var dmaxDose = goals
                    .Where(IsDmaxGoal)
                    .Select(goal => GetGoalDoseGy(goal, plan))
                    .FirstOrDefault(d => d.HasValue);

                if (dmaxDose.HasValue)
                {
                    oarsWithDmax.Add((structure, dmaxDose.Value));
                }
            }

            // Step 3 & 4: Create pairs and filter by dose comparison (cheap operation)
            var doseConflictPairs = new List<(Structure target, double targetDose, Structure oar, double oarDmax)>();

            foreach (var target in targetsWithLowerGoals)
            {
                foreach (var oar in oarsWithDmax)
                {
                    // Skip if target and OAR are the same structure
                    if (target.structure.Id.Equals(oar.structure.Id, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Filter by dose: target lower goal > OAR Dmax = potential conflict
                    if (target.lowerGoalDose > oar.dmaxDose)
                    {
                        doseConflictPairs.Add((target.structure, target.lowerGoalDose, oar.structure, oar.dmaxDose));
                    }
                }
            }

            // Step 5: Check spatial overlap ONLY for dose-conflicting pairs (expensive operation)
            int overlapCount = 0;
            foreach (var pair in doseConflictPairs)
            {
                if (StructuresOverlap(pair.target, pair.oar, image))
                {
                    results.Add(CreateResult(
                        "Target-OAR Overlap",
                        $"{pair.target.Id} (lower goal: {pair.targetDose:F2} Gy) overlaps with OAR '{pair.oar.Id}' (Dmax: {pair.oarDmax:F2} Gy).",
                        ValidationSeverity.Warning));
                    overlapCount++;
                }
            }

            // Add recommendation if any overlaps were found
            if (overlapCount > 0)
            {
                results.Add(CreateResult(
                    "Target-OAR Overlap",
                    $"Consider creating _eval structures and leaving comment in prescription.",
                    ValidationSeverity.Info));
            }
            else if (doseConflictPairs.Count > 0)
            {
                // Dose conflicts exist but no spatial overlaps
                results.Add(CreateResult(
                    "Target-OAR Overlap",
                    $"No spatial overlaps detected between targets and OARs with conflicting dose constraints.",
                    ValidationSeverity.Info));
            }
            else
            {
                // No dose conflicts at all
                results.Add(CreateResult(
                    "Target-OAR Overlap",
                    $"No target-OAR dose conflicts detected.",
                    ValidationSeverity.Info));
            }
        }

        /// <summary>
        /// Validates that small PTVs and their related targets (CTV/GTV) use high-resolution contouring.
        /// Small volumes require high resolution for accurate dose calculation and delivery.
        /// Error for <5cc, Warning for 5-10cc if any related target is not high resolution.
        /// </summary>
        /// <param name="structures">Collection of structures to validate</param>
        /// <param name="results">List to append validation results to</param>
        private static void ValidateSmallVolumeResolution(IEnumerable<Structure> structures, List<ValidationResult> results)
        {
            var structureList = structures.ToList();
            int initialCount = results.Count;
            int checkedPtvs = 0;
            int ptvCountBelow10cc = 0;
            double? smallestPtvVolume = null;

            foreach (var ptv in structureList.Where(s => s.Id.StartsWith("PTV", StringComparison.OrdinalIgnoreCase)))
            {
                checkedPtvs++;
                double volume = ptv.Volume;

                // Track smallest PTV for info message
                if (!smallestPtvVolume.HasValue || volume < smallestPtvVolume.Value)
                    smallestPtvVolume = volume;

                // Count PTVs below 10cc threshold for summary message
                if (volume < 10.0)
                    ptvCountBelow10cc++;

                // Find related target structures by matching suffix
                string suffix = GetTargetSuffix(ptv.Id, "PTV");
                var linkedTargets = new List<Structure>();

                var matchingCtv = structureList.FirstOrDefault(s =>
                    s.Id.StartsWith("CTV", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(GetTargetSuffix(s.Id, "CTV"), suffix, StringComparison.OrdinalIgnoreCase));
                if (matchingCtv != null) linkedTargets.Add(matchingCtv);

                var matchingGtv = structureList.FirstOrDefault(s =>
                    s.Id.StartsWith("GTV", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(GetTargetSuffix(s.Id, "GTV"), suffix, StringComparison.OrdinalIgnoreCase));
                if (matchingGtv != null) linkedTargets.Add(matchingGtv);

                // Check if all related targets use high resolution
                bool allHighRes = new[] { ptv }.Concat(linkedTargets).All(s => s.IsHighResolution);

                // Critical error for very small targets (<5cc) without high resolution
                if (volume < 5.0)
                {
                    if (!allHighRes)
                    {
                        results.Add(CreateResult(
                            "Target Resolution",
                            $"PTV '{ptv.Id}' volume is {volume:F1} cc (<5 cc) and not all related targets use high resolution.",
                            ValidationSeverity.Error));
                    }
                }
                // Warning for small targets (5-10cc) without high resolution
                else if (volume < 10.0 && !allHighRes)
                {
                    results.Add(CreateResult(
                        "Target Resolution",
                        $"PTV '{ptv.Id}' volume is {volume:F1} cc (<10 cc) and related targets are not all high resolution.",
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

        /// <summary>
        /// Validates that target structures have the correct DICOM structure type set.
        /// Structures named PTV_* should have DicomType="PTV", CTV_* should be "CTV", etc.
        /// Correct DICOM typing ensures proper structure recognition by planning systems.
        /// </summary>
        /// <param name="structures">Collection of structures to validate</param>
        /// <param name="results">List to append validation results to</param>
        private static void ValidateTargetStructureTypes(IEnumerable<Structure> structures, List<ValidationResult> results)
        {
            int initialCount = results.Count;
            int checkedStructures = 0;

            foreach (var structure in structures)
            {
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

        private static bool IsStructureContained(Structure inner, Structure outer, Image image)
        {
            if (inner == null || outer == null || inner.IsEmpty || outer.IsEmpty)
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

        private static bool StructuresOverlap(Structure a, Structure b, Image image)
        {
            if (a == null || b == null || a.IsEmpty || b.IsEmpty)
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
        /// Builds a dictionary mapping structure IDs to their associated clinical goals.
        /// Enables fast lookup of all goals for a given structure during validation.
        /// </summary>
        /// <param name="clinicalGoals">Collection of clinical goal objects from the plan</param>
        /// <returns>Dictionary with structure IDs as keys and lists of goals as values</returns>
        private static Dictionary<string, List<object>> BuildStructureGoalLookup(IEnumerable<object> clinicalGoals)
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

        /// <summary>
        /// Retrieves clinical goals from the plan using multiple access methods for cross-version compatibility.
        /// Tries 5 different approaches to handle variations across Eclipse API versions.
        /// Returns unique goals using HashSet to prevent duplicates.
        /// </summary>
        /// <param name="plan">The plan setup containing clinical goals</param>
        /// <returns>Enumerable collection of clinical goal objects</returns>
        private static IEnumerable<object> GetClinicalGoals(PlanSetup plan)
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
            if (property?.GetValue(plan) is IEnumerable enumerable)
            {
                foreach (var goal in enumerable)
                    yield return goal;
            }
        }

        private static IEnumerable<object> GetClinicalGoalsFromMethod(PlanSetup plan, string methodName)
        {
            var method = plan.GetType().GetMethod(methodName, Type.EmptyTypes);
            if (method?.Invoke(plan, null) is IEnumerable enumerable)
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
            if (property?.GetValue(course) is IEnumerable enumerable)
            {
                foreach (var goal in enumerable)
                    yield return goal;
            }
        }

        private static string GetStructureId(object goal)
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
        private static bool IsLowerGoal(object goal)
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
        private static bool IsDmaxGoal(object goal)
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
        private static double? GetGoalDoseGy(object goal, PlanSetup plan)
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
        private static double? ParseDoseFromObjectiveString(string objectiveString, PlanSetup plan)
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

        private static double? NormalizeDoseValueGy(DoseValue doseValue, PlanSetup plan = null)
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

        private static double? ConvertPercentToGy(double percentValue, PlanSetup plan)
        {
            var totalDoseGy = GetPlanTotalDoseGy(plan);
            if (!totalDoseGy.HasValue)
                return null;

            return totalDoseGy.Value * percentValue / 100.0;
        }

        private static double? GetPlanTotalDoseGy(PlanSetup plan)
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

        private static object GetPropertyValue(object obj, string propertyName)
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
        private static HashSet<string> GetPrescriptionTargetIds(PlanSetup plan)
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
        /// Example: "PTV_59.4" with prefix "PTV" returns "59.4"
        /// Used to match related structures (PTV_59.4, CTV_59.4, GTV_59.4).
        /// </summary>
        /// <param name="structureId">The full structure ID</param>
        /// <param name="prefix">The prefix to remove (PTV, CTV, GTV)</param>
        /// <returns>The suffix after the prefix, with leading underscores removed</returns>
        private static string GetTargetSuffix(string structureId, string prefix)
        {
            if (!structureId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return structureId;

            return structureId.Substring(prefix.Length).TrimStart('_');
        }

        /// <summary>
        /// Determines if a structure should be excluded from clinical goal validation.
        /// Implements prescription-aware exclusion logic for target structures.
        ///
        /// Exclusion rules:
        /// 1. SUPPORT structures (DICOM type = SUPPORT)
        /// 2. Structures with specific patterns: z_*, *wire*, *Encompass*, *Enc Marker*, *Dose*
        /// 3. Structures in ExcludedStructures list (Bones, CouchInterior, etc.)
        /// 4. GTV/CTV/PTV structures NOT in the dose prescription (evaluation/backup targets)
        /// </summary>
        /// <param name="structure">The structure to check</param>
        /// <param name="prescriptionTargetIds">Set of structure IDs from prescription</param>
        /// <returns>True if structure should be excluded from validation</returns>
        private static bool IsStructureExcluded(Structure structure, HashSet<string> prescriptionTargetIds)
        {
            // Exclude structures with DICOM type 'SUPPORT'
            if (string.Equals(structure.DicomType, "SUPPORT", StringComparison.OrdinalIgnoreCase))
                return true;

            // Exclude structures with certain patterns in their names
            if (structure.Id.StartsWith("z_", StringComparison.OrdinalIgnoreCase) ||
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
            return ExcludedStructures.Contains(structure.Id);
        }

        /// <summary>
        /// Diagnostic method to print ALL available information from clinical goals.
        /// Prints ToString(), type name, all properties, and all fields.
        /// </summary>
        private static void PrintClinicalGoalsDiagnostic(List<object> clinicalGoals, List<ValidationResult> results)
        {
            results.Add(CreateResult(
                "DIAGNOSTIC - Clinical Goals",
                $"Total clinical goals found: {clinicalGoals.Count}",
                ValidationSeverity.Info));

            int goalNumber = 0;
            foreach (var goal in clinicalGoals)
            {
                goalNumber++;
                var goalType = goal.GetType();

                results.Add(CreateResult(
                    "DIAGNOSTIC - Clinical Goals",
                    $"========== Goal #{goalNumber} ==========",
                    ValidationSeverity.Info));

                // Print ToString() representation
                try
                {
                    var toStringValue = goal.ToString();
                    results.Add(CreateResult(
                        "DIAGNOSTIC - Clinical Goals",
                        $"  ToString(): {toStringValue}",
                        ValidationSeverity.Info));
                }
                catch (Exception ex)
                {
                    results.Add(CreateResult(
                        "DIAGNOSTIC - Clinical Goals",
                        $"  ToString(): <error: {ex.Message}>",
                        ValidationSeverity.Info));
                }

                // Print exact type name
                results.Add(CreateResult(
                    "DIAGNOSTIC - Clinical Goals",
                    $"  Type: {goalType.FullName}",
                    ValidationSeverity.Info));

                // Print ALL properties
                var properties = goalType.GetProperties(System.Reflection.BindingFlags.Public |
                                                       System.Reflection.BindingFlags.Instance);
                results.Add(CreateResult(
                    "DIAGNOSTIC - Clinical Goals",
                    $"  Properties ({properties.Length}):",
                    ValidationSeverity.Info));

                foreach (var prop in properties)
                {
                    try
                    {
                        var val = prop.GetValue(goal);
                        var propType = prop.PropertyType.Name;
                        results.Add(CreateResult(
                            "DIAGNOSTIC - Clinical Goals",
                            $"    {prop.Name} ({propType}): {val ?? "<null>"}",
                            ValidationSeverity.Info));
                    }
                    catch (Exception ex)
                    {
                        results.Add(CreateResult(
                            "DIAGNOSTIC - Clinical Goals",
                            $"    {prop.Name}: <error: {ex.Message}>",
                            ValidationSeverity.Info));
                    }
                }

                // Print ALL fields
                var fields = goalType.GetFields(System.Reflection.BindingFlags.Public |
                                               System.Reflection.BindingFlags.Instance);
                if (fields.Length > 0)
                {
                    results.Add(CreateResult(
                        "DIAGNOSTIC - Clinical Goals",
                        $"  Fields ({fields.Length}):",
                        ValidationSeverity.Info));

                    foreach (var field in fields)
                    {
                        try
                        {
                            var val = field.GetValue(goal);
                            var fieldType = field.FieldType.Name;
                            results.Add(CreateResult(
                                "DIAGNOSTIC - Clinical Goals",
                                $"    {field.Name} ({fieldType}): {val ?? "<null>"}",
                                ValidationSeverity.Info));
                        }
                        catch (Exception ex)
                        {
                            results.Add(CreateResult(
                                "DIAGNOSTIC - Clinical Goals",
                                $"    {field.Name}: <error: {ex.Message}>",
                                ValidationSeverity.Info));
                        }
                    }
                }
            }
        }

        private static ValidationResult CreateResult(string category, string message, ValidationSeverity severity)
        {
            return new ValidationResult
            {
                Category = category,
                Message = message,
                Severity = severity,
                IsFieldResult = false
            };
        }
    }
}
