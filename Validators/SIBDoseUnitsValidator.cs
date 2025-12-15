using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;

namespace ROcheck.Validators
{
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
    public class SIBDoseUnitsValidator : ValidatorBase
    {
        public override IEnumerable<ValidationResult> Validate(ScriptContext context)
        {
            var results = new List<ValidationResult>();

            var plan = context.PlanSetup;
            var structureSet = context.StructureSet;

            if (plan == null || structureSet == null)
                return results;

            // Retrieve clinical goals
            var clinicalGoals = ValidationHelpers.GetClinicalGoals(plan).ToList();
            var structureGoals = ValidationHelpers.BuildStructureGoalLookup(clinicalGoals);

            ValidateSIBDoseUnits(structureSet.Structures, structureGoals, plan, results);

            return results;
        }

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
    }
}
