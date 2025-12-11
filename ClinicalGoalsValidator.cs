using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace ROcheck
{
    /// <summary>
    /// Clinical Goals Validator - Core ROcheck functionality.
    /// Validates structure setup and clinical goal configuration.
    /// </summary>
    public class ClinicalGoalsValidator : ValidatorBase
    {
        private static readonly HashSet<string> ExcludedStructures = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Bones", "CouchInterior", "CouchSurface", "Clips", "Scar_Wire", "Sternum"
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
            var clinicalGoals = ValidationHelpers.GetClinicalGoals(plan).ToList();

            // Build a lookup dictionary mapping structure IDs to their associated clinical goals
            var structureGoals = ValidationHelpers.BuildStructureGoalLookup(clinicalGoals);

            // Extract target structure IDs from prescription for smart target filtering
            var prescriptionTargetIds = ValidationHelpers.GetPrescriptionTargetIds(plan);

            // Execute all validation checks
            ValidateClinicalGoalPresence(structureSet.Structures, structureGoals, prescriptionTargetIds, results);
            ValidateTargetContainment(structureSet.Structures, structureSet.Image, results);
            ValidateTargetsOverlappingOars(structureSet.Structures, structureSet.Image, structureGoals, plan, results);
            ValidateSmallVolumeResolution(structureSet.Structures, results);
            ValidateTargetStructureTypes(structureSet.Structures, results);
            ValidateSIBDoseUnits(structureSet.Structures, structureGoals, plan, results);

            return results;
        }

        /// <summary>
        /// Validates that all applicable structures have at least one associated clinical goal.
        /// Uses prescription-aware filtering to only check active treatment targets.
        /// </summary>
        private void ValidateClinicalGoalPresence(IEnumerable<Structure> structures,
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
        private void ValidateTargetContainment(IEnumerable<Structure> structures, Image image, List<ValidationResult> results)
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
                string suffix = ValidationHelpers.GetTargetSuffix(ptv.Id, "PTV");

                // Check both CTV and GTV for this PTV
                foreach (var prefix in new[] { "CTV", "GTV" })
                {
                    // Find matching target by prefix and suffix
                    var target = structureList.FirstOrDefault(s =>
                        s.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(ValidationHelpers.GetTargetSuffix(s.Id, prefix), suffix, StringComparison.OrdinalIgnoreCase));

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
        /// </summary>
        private void ValidateTargetsOverlappingOars(IEnumerable<Structure> structures,
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

            // Step 1: Find all structures with lower objectives (targets)
            var targetsWithLowerGoals = new List<(Structure structure, double lowerGoalDose)>();

            foreach (var structure in structureList)
            {
                if (!structureGoals.TryGetValue(structure.Id, out var goals))
                    continue;

                var lowerGoalDose = goals
                    .Where(ValidationHelpers.IsLowerGoal)
                    .Select(goal => ValidationHelpers.GetGoalDoseGy(goal, plan))
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
                    .Where(ValidationHelpers.IsDmaxGoal)
                    .Select(goal => ValidationHelpers.GetGoalDoseGy(goal, plan))
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
        private void ValidateSmallVolumeResolution(IEnumerable<Structure> structures, List<ValidationResult> results)
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
                string suffix = ValidationHelpers.GetTargetSuffix(ptv.Id, "PTV");
                var linkedTargets = new List<Structure>();

                var matchingCtv = structureList.FirstOrDefault(s =>
                    s.Id.StartsWith("CTV", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(ValidationHelpers.GetTargetSuffix(s.Id, "CTV"), suffix, StringComparison.OrdinalIgnoreCase));
                if (matchingCtv != null) linkedTargets.Add(matchingCtv);

                var matchingGtv = structureList.FirstOrDefault(s =>
                    s.Id.StartsWith("GTV", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(ValidationHelpers.GetTargetSuffix(s.Id, "GTV"), suffix, StringComparison.OrdinalIgnoreCase));
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
        private void ValidateTargetStructureTypes(IEnumerable<Structure> structures, List<ValidationResult> results)
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

        /// <summary>
        /// Validates dose units in SIB (Simultaneously Integrated Boost) plans.
        ///
        /// SIB Detection: If target clinical goals differ by >6% of the higher dose, plan is considered SIB.
        /// SIB Requirement: ALL clinical goals (targets + OARs) must use Gy units only, no percentages.
        ///
        /// Note: Uses clinical goals ONLY for SIB detection. RTPrescription is not accessible at script launch.
        ///
        /// Silent pass: Only shows Error messages when percentage units found in SIB plans.
        /// </summary>
        private void ValidateSIBDoseUnits(IEnumerable<Structure> structures,
            Dictionary<string, List<object>> structureGoals,
            PlanSetup plan,
            List<ValidationResult> results)
        {
            var structureList = structures.ToList();
            if (!structureList.Any())
                return;

            // Step 1: Identify target structures (PTV, CTV, GTV) with clinical goals and extract doses
            var targetDoses = new List<(string structureId, double doseGy)>();

            foreach (var structure in structureList)
            {
                // Check if this is a target structure
                bool isTarget = structure.Id.StartsWith("PTV", StringComparison.OrdinalIgnoreCase) ||
                                structure.Id.StartsWith("CTV", StringComparison.OrdinalIgnoreCase) ||
                                structure.Id.StartsWith("GTV", StringComparison.OrdinalIgnoreCase);

                if (!isTarget)
                    continue;

                // Check if structure has clinical goals
                if (!structureGoals.TryGetValue(structure.Id, out var goals))
                    continue;

                // Extract representative dose from goals
                var dose = ValidationHelpers.ExtractTargetDose(goals, plan);
                if (dose.HasValue)
                {
                    targetDoses.Add((structure.Id, dose.Value));
                }
            }

            // Step 2: Check if this is a SIB plan (>6% dose difference between targets)
            bool isSIB = false;
            for (int i = 0; i < targetDoses.Count; i++)
            {
                for (int j = i + 1; j < targetDoses.Count; j++)
                {
                    double dose1 = targetDoses[i].doseGy;
                    double dose2 = targetDoses[j].doseGy;
                    double higherDose = Math.Max(dose1, dose2);
                    double percentDiff = Math.Abs(dose1 - dose2) / higherDose * 100.0;

                    if (percentDiff > 6.0)
                    {
                        isSIB = true;
                        break;
                    }
                }

                if (isSIB)
                    break;
            }

            // Step 3: If SIB detected, validate that ALL clinical goals use Gy units (no percentages)
            if (isSIB)
            {
                // Check ALL clinical goals (targets and OARs)
                foreach (var kvp in structureGoals)
                {
                    string structureId = kvp.Key;
                    var goals = kvp.Value;

                    foreach (var goal in goals)
                    {
                        if (ValidationHelpers.HasPercentageDose(goal))
                        {
                            // Found percentage dose in SIB plan - ERROR
                            results.Add(CreateResult(
                                "SIB Dose Units",
                                $"SIB plan detected: clinical goal for '{structureId}' uses percentage dose units.\nSIB plans require Gy units for all clinical goals (targets and OARs).",
                                ValidationSeverity.Error));
                        }
                    }
                }
            }

            // Silent pass: No messages if not SIB or if SIB with all Gy units
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
        /// Determines if a structure should be excluded from clinical goal validation.
        /// Implements prescription-aware exclusion logic for target structures.
        ///
        /// Exclusion rules:
        /// 1. SUPPORT structures (DICOM type = SUPPORT)
        /// 2. Structures with specific patterns: z_*, *wire*, *Encompass*, *Enc Marker*, *Dose*, Implant*, Lymph*, LN_*
        /// 3. Structures in ExcludedStructures list (Bones, CouchInterior, Clips, Scar_Wire, Sternum)
        /// 4. GTV/CTV/PTV structures NOT in the dose prescription (evaluation/backup targets)
        /// </summary>
        private static bool IsStructureExcluded(Structure structure, HashSet<string> prescriptionTargetIds)
        {
            // Exclude structures with DICOM type 'SUPPORT'
            if (string.Equals(structure.DicomType, "SUPPORT", StringComparison.OrdinalIgnoreCase))
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
            return ExcludedStructures.Contains(structure.Id);
        }
    }
}
