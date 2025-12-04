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
            ValidateTargetsOverlappingOars(structureSet.Structures, structureSet.Image, structureGoals, results);
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
        /// Validates that target volumes (GTV/CTV/PTV) with lower dose goals don't overlap with OARs
        /// that have Dmax objectives below the target's lower goal dose. This identifies potential
        /// dose conflicts where an OAR maximum dose constraint is lower than the minimum target dose.
        /// </summary>
        private static void ValidateTargetsOverlappingOars(IEnumerable<Structure> structures,
            Image image,
            Dictionary<string, List<object>> structureGoals,
            List<ValidationResult> results)
        {
            var structureList = structures.ToList();
            if (!structureList.Any())
                return;

            if (image == null)
                return;

            int initialCount = results.Count;
            int checkedTargets = 0;

            // Check all target volumes (GTV, CTV, PTV) for overlaps with OARs
            foreach (var target in structureList.Where(s =>
                s.Id.StartsWith("PTV", StringComparison.OrdinalIgnoreCase) ||
                s.Id.StartsWith("CTV", StringComparison.OrdinalIgnoreCase) ||
                s.Id.StartsWith("GTV", StringComparison.OrdinalIgnoreCase)))
            {
                // Skip if this target doesn't have clinical goals
                if (!structureGoals.TryGetValue(target.Id, out var targetGoals))
                    continue;

                // Find the lower dose goal (minimum dose constraint) for this target
                var lowerGoalDose = targetGoals
                    .Where(IsLowerGoal)
                    .Select(GetGoalDoseGy)
                    .FirstOrDefault(d => d.HasValue);

                // Skip if no lower goal found
                if (!lowerGoalDose.HasValue)
                    continue;

                checkedTargets++;

                // Find all non-target structures (potential OARs) that overlap with this target
                var overlappingOars = structureList
                    .Where(s => !s.Id.StartsWith("PTV", StringComparison.OrdinalIgnoreCase)
                                && !s.Id.StartsWith("CTV", StringComparison.OrdinalIgnoreCase)
                                && !s.Id.StartsWith("GTV", StringComparison.OrdinalIgnoreCase)
                                && structureGoals.ContainsKey(s.Id)
                                && StructuresOverlap(target, s, image));

                // Check each overlapping OAR for conflicting dose constraints
                foreach (var oar in overlappingOars)
                {
                    // Find the Dmax goal for this OAR
                    var dmaxGoalDose = structureGoals[oar.Id]
                        .Where(IsDmaxGoal)
                        .Select(GetGoalDoseGy)
                        .FirstOrDefault(d => d.HasValue);

                    // Report if OAR Dmax is less than target lower goal (dose conflict)
                    if (dmaxGoalDose.HasValue && dmaxGoalDose.Value < lowerGoalDose.Value)
                    {
                        results.Add(CreateResult(
                            "Target-OAR Overlap",
                            $"{target.Id} (lower goal: {lowerGoalDose.Value:F2} Gy) overlaps with OAR '{oar.Id}' (Dmax: {dmaxGoalDose.Value:F2} Gy). Consider creating {target.Id}_eval and documenting in prescription.",
                            ValidationSeverity.Warning));
                    }
                }
            }

            // Add summary if all checked targets passed
            if (results.Count == initialCount && checkedTargets > 0)
            {
                results.Add(CreateResult(
                    "Target-OAR Overlap",
                    $"All {checkedTargets} target(s) checked - no problematic target-OAR overlaps with conflicting dose constraints detected.",
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

        private static bool IsLowerGoal(object goal)
        {
            var criteriaText = (GetPropertyValue(goal, "GoalCriteria") ??
                                GetPropertyValue(goal, "GoalOperator") ??
                                GetPropertyValue(goal, "Operator"))?.ToString();

            if (string.IsNullOrWhiteSpace(criteriaText))
                return false;

            return criteriaText.IndexOf("lower", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   criteriaText.IndexOf("greater", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   criteriaText.IndexOf("atleast", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   criteriaText.Contains(">=");
        }

        private static bool IsDmaxGoal(object goal)
        {
            var typeText = (GetPropertyValue(goal, "GoalType") ?? GetPropertyValue(goal, "Type"))?.ToString();
            var nameText = (GetPropertyValue(goal, "Name") ?? GetPropertyValue(goal, "Id"))?.ToString();
            return (typeText != null && typeText.IndexOf("max", StringComparison.OrdinalIgnoreCase) >= 0)
                   || (nameText != null && nameText.IndexOf("dmax", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static double? GetGoalDoseGy(object goal)
        {
            var doseObject = GetPropertyValue(goal, "Dose")
                             ?? GetPropertyValue(goal, "DoseGoal")
                             ?? GetPropertyValue(goal, "TargetDose")
                             ?? GetPropertyValue(goal, "Goal")
                             ?? GetPropertyValue(goal, "ObjectiveValue");

            if (doseObject is DoseValue doseValue)
                return NormalizeDoseValueGy(doseValue);

            if (doseObject is double doubleDose)
                return doubleDose;

            if (doseObject is float floatDose)
                return floatDose;

            var property = doseObject?.GetType().GetProperty("Dose");
            if (property != null)
            {
                var innerDose = property.GetValue(doseObject);
                if (innerDose is DoseValue innerDoseValue)
                    return NormalizeDoseValueGy(innerDoseValue);

                if (innerDose is double innerDouble)
                    return innerDouble;

                if (innerDose is float innerFloat)
                    return innerFloat;
            }

            return null;
        }

        private static double? NormalizeDoseValueGy(DoseValue doseValue)
        {
            var unitText = doseValue.Unit.ToString();

            if (unitText.Equals("Gy", StringComparison.OrdinalIgnoreCase))
                return doseValue.Dose;

            if (unitText.Equals("cGy", StringComparison.OrdinalIgnoreCase))
                return doseValue.Dose / 100.0;

            if (unitText.IndexOf("Percent", StringComparison.OrdinalIgnoreCase) >= 0)
                return null;

            var presentationProperty = typeof(DoseValue).GetProperty("Presentation");
            if (presentationProperty != null)
            {
                var presentationValue = presentationProperty.GetValue(doseValue)?.ToString();
                if (presentationValue?.IndexOf("Relative", StringComparison.OrdinalIgnoreCase) >= 0)
                    return null;
            }

            return doseValue.Dose;
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
        /// 2. Structures with specific patterns: z_*, *wire*, *Encompass*, *Enc*, *Dose*
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
                structure.Id.IndexOf("Enc", StringComparison.OrdinalIgnoreCase) >= 0 ||
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
