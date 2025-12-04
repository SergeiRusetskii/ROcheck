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
            "Bones", "CouchInterior", "CouchSurface"
        };

        public override IEnumerable<ValidationResult> Validate(ScriptContext context)
        {
            var results = new List<ValidationResult>();

            var plan = context.PlanSetup;
            var structureSet = context.StructureSet;

            if (plan == null || structureSet == null)
                return results;

            var clinicalGoals = GetClinicalGoals(plan).ToList();

            // DEBUG: Show first 3 clinical goal properties
            for (int i = 0; i < Math.Min(3, clinicalGoals.Count); i++)
            {
                var goal = clinicalGoals[i];
                results.Add(CreateResult(
                    "DEBUG - Clinical Goals",
                    $"=== Clinical Goal #{i + 1} Properties ===",
                    ValidationSeverity.Info));

                var goalType = goal.GetType();
                foreach (var prop in goalType.GetProperties())
                {
                    try
                    {
                        var value = prop.GetValue(goal);
                        results.Add(CreateResult(
                            "DEBUG - Clinical Goals",
                            $"  {prop.Name}: {value ?? "<null>"}",
                            ValidationSeverity.Info));
                    }
                    catch
                    {
                        results.Add(CreateResult(
                            "DEBUG - Clinical Goals",
                            $"  {prop.Name}: <error reading>",
                            ValidationSeverity.Info));
                    }
                }
            }

            var structureGoals = BuildStructureGoalLookup(clinicalGoals);

            ValidateClinicalGoalPresence(structureSet.Structures, structureGoals, results);
            ValidateTargetContainment(structureSet.Structures, structureSet.Image, results);
            ValidatePtvsOverlappingOars(structureSet.Structures, structureSet.Image, structureGoals, results);
            ValidateSmallVolumeResolution(structureSet.Structures, results);
            ValidateTargetStructureTypes(structureSet.Structures, results);

            return results;
        }

        private static void ValidateClinicalGoalPresence(IEnumerable<Structure> structures,
            Dictionary<string, List<object>> structureGoals,
            List<ValidationResult> results)
        {
            int initialCount = results.Count;
            int checkedCount = 0;

            // DEBUG: Display all clinical goal structure IDs in the window
            results.Add(CreateResult(
                "DEBUG - Clinical Goals",
                $"Total clinical goals found: {structureGoals.Count}",
                ValidationSeverity.Info));

            foreach (var kvp in structureGoals)
            {
                results.Add(CreateResult(
                    "DEBUG - Clinical Goals",
                    $"CG Structure ID: '{kvp.Key}' ({kvp.Value.Count} goals)",
                    ValidationSeverity.Info));
            }

            foreach (var structure in structures)
            {
                if (IsStructureExcluded(structure))
                    continue;

                checkedCount++;

                if (!structureGoals.ContainsKey(structure.Id))
                {
                    results.Add(CreateResult(
                        "Structure Coverage",
                        $"Structure '{structure.Id}' has no associated clinical goal.",
                        ValidationSeverity.Warning));
                }
            }

            // Add summary if all checked structures passed
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

        private static void ValidateTargetContainment(IEnumerable<Structure> structures, Image image, List<ValidationResult> results)
        {
            var structureList = structures.ToList();
            if (!structureList.Any())
                return;

            if (image == null)
                return;

            int initialCount = results.Count;
            int checkedPairs = 0;

            foreach (var ptv in structureList.Where(s => s.Id.StartsWith("PTV", StringComparison.OrdinalIgnoreCase)))
            {
                string suffix = GetTargetSuffix(ptv.Id, "PTV");

                foreach (var prefix in new[] { "CTV", "GTV" })
                {
                    var target = structureList.FirstOrDefault(s =>
                        s.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(GetTargetSuffix(s.Id, prefix), suffix, StringComparison.OrdinalIgnoreCase));

                    if (target != null)
                    {
                        checkedPairs++;
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

        private static void ValidatePtvsOverlappingOars(IEnumerable<Structure> structures,
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
            int checkedPtvs = 0;

            foreach (var ptv in structureList.Where(s => s.Id.StartsWith("PTV", StringComparison.OrdinalIgnoreCase)))
            {
                if (!structureGoals.TryGetValue(ptv.Id, out var ptvGoals))
                    continue;

                var lowerGoalDose = ptvGoals
                    .Where(IsLowerGoal)
                    .Select(GetGoalDoseGy)
                    .FirstOrDefault(d => d.HasValue);

                if (!lowerGoalDose.HasValue)
                    continue;

                checkedPtvs++;

                var overlappingOars = structureList
                    .Where(s => !s.Id.StartsWith("PTV", StringComparison.OrdinalIgnoreCase)
                                && !s.Id.StartsWith("CTV", StringComparison.OrdinalIgnoreCase)
                                && !s.Id.StartsWith("GTV", StringComparison.OrdinalIgnoreCase)
                                && structureGoals.ContainsKey(s.Id)
                                && StructuresOverlap(ptv, s, image));

                foreach (var oar in overlappingOars)
                {
                    var dmaxGoalDose = structureGoals[oar.Id]
                        .Where(IsDmaxGoal)
                        .Select(GetGoalDoseGy)
                        .FirstOrDefault(d => d.HasValue);

                    if (dmaxGoalDose.HasValue && dmaxGoalDose.Value < lowerGoalDose.Value)
                    {
                        results.Add(CreateResult(
                            "PTV-OAR Overlap",
                            $"{ptv.Id} overlaps with OAR '{oar.Id}' that has Dmax objective below the PTV lower goal; consider creating {ptv.Id}_eval and documenting in prescription.",
                            ValidationSeverity.Warning));
                    }
                }
            }

            // Add summary if all checked PTVs passed
            if (results.Count == initialCount && checkedPtvs > 0)
            {
                results.Add(CreateResult(
                    "PTV-OAR Overlap",
                    $"All {checkedPtvs} PTV(s) checked - no problematic PTV-OAR overlaps with conflicting dose constraints detected.",
                    ValidationSeverity.Info));
            }
        }

        private static void ValidateSmallVolumeResolution(IEnumerable<Structure> structures, List<ValidationResult> results)
        {
            var structureList = structures.ToList();
            int initialCount = results.Count;
            int checkedPtvs = 0;
            double? smallestPtvVolume = null;

            foreach (var ptv in structureList.Where(s => s.Id.StartsWith("PTV", StringComparison.OrdinalIgnoreCase)))
            {
                checkedPtvs++;
                double volume = ptv.Volume;

                if (!smallestPtvVolume.HasValue || volume < smallestPtvVolume.Value)
                    smallestPtvVolume = volume;

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

                bool allHighRes = new[] { ptv }.Concat(linkedTargets).All(s => s.IsHighResolution);

                if (volume < 10.0)
                {
                    if (!allHighRes)
                    {
                        results.Add(CreateResult(
                            "Target Resolution",
                            $"PTV '{ptv.Id}' volume is {volume:F1} cc (<10 cc) and not all related targets use high resolution.",
                            ValidationSeverity.Error));
                    }
                }
                else if (volume < 20.0 && !allHighRes)
                {
                    results.Add(CreateResult(
                        "Target Resolution",
                        $"PTV '{ptv.Id}' volume is {volume:F1} cc (<20 cc) and related targets are not all high resolution.",
                        ValidationSeverity.Warning));
                }
            }

            // Add summary if all checked PTVs passed
            if (results.Count == initialCount && checkedPtvs > 0)
            {
                string volumeInfo = smallestPtvVolume.HasValue ? $" - Smallest PTV: {smallestPtvVolume.Value:F1} cc" : "";
                results.Add(CreateResult(
                    "Target Resolution",
                    $"All {checkedPtvs} PTV(s) checked{volumeInfo}.",
                    ValidationSeverity.Info));
            }
        }

        private static void ValidateTargetStructureTypes(IEnumerable<Structure> structures, List<ValidationResult> results)
        {
            int initialCount = results.Count;
            int checkedStructures = 0;

            foreach (var structure in structures)
            {
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

        private static Dictionary<string, List<object>> BuildStructureGoalLookup(IEnumerable<object> clinicalGoals)
        {
            var lookup = new Dictionary<string, List<object>>(StringComparer.OrdinalIgnoreCase);

            foreach (var goal in clinicalGoals)
            {
                var structureId = GetStructureId(goal);
                if (string.IsNullOrWhiteSpace(structureId))
                    continue;

                if (!lookup.TryGetValue(structureId, out var list))
                {
                    list = new List<object>();
                    lookup[structureId] = list;
                }

                list.Add(goal);
            }

            return lookup;
        }

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

        private static string GetTargetSuffix(string structureId, string prefix)
        {
            if (!structureId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return structureId;

            return structureId.Substring(prefix.Length).TrimStart('_');
        }

        private static bool IsStructureExcluded(Structure structure)
        {
            if (structure.Id.StartsWith("z_", StringComparison.OrdinalIgnoreCase) ||
                structure.Id.StartsWith("GTV", StringComparison.OrdinalIgnoreCase) ||
                structure.Id.StartsWith("CTV", StringComparison.OrdinalIgnoreCase) ||
                structure.Id.StartsWith("PTV", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

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
