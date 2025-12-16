using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;

namespace ROcheck.Validators
{
    /// <summary>
    /// Validates that target volumes with lower dose goals don't overlap with OARs
    /// that have Dmax objectives below the target's lower goal dose.
    /// </summary>
    public class TargetOAROverlapValidator : ValidatorBase
    {
        public override IEnumerable<ValidationResult> Validate(ScriptContext context)
        {
            var results = new List<ValidationResult>();

            var plan = context.PlanSetup;
            var structureSet = context.StructureSet;

            if (plan == null || structureSet?.Image == null)
                return results;

            // Retrieve clinical goals
            var clinicalGoals = ValidationHelpers.GetClinicalGoals(plan).ToList();
            var structureGoals = ValidationHelpers.BuildStructureGoalLookup(clinicalGoals);

            ValidateTargetsOverlappingOars(structureSet.Structures, structureSet.Image, structureGoals, plan, results);

            return results;
        }

        private void ValidateTargetsOverlappingOars(IEnumerable<Structure> structures,
            Image image,
            Dictionary<string, List<object>> structureGoals,
            PlanSetup plan,
            List<ValidationResult> results)
        {
            var structureList = structures.ToList();
            if (!structureList.Any())
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
                if (ValidationHelpers.StructuresOverlap(pair.target, pair.oar, image))
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
                    $"No spatial overlaps detected between targets and OARs with conflicting Dmax constraints.",
                    ValidationSeverity.Info));
            }
            else
            {
                // No dose conflicts at all
                results.Add(CreateResult(
                    "Target-OAR Overlap",
                    $"No target with OAR Dmax conflicts detected.",
                    ValidationSeverity.Info));
            }
        }
    }
}
