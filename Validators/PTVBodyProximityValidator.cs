using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace ROcheck.Validators
{
    /// <summary>
    /// Validator that checks proximity of PTV structures to Body surface.
    /// Warns when PTVs are too close to skin, suggesting use of EVAL structures for optimization.
    /// </summary>
    public class PTVBodyProximityValidator : ValidatorBase
    {
        private const double PROXIMITY_THRESHOLD_MM = 4.0; // 4mm threshold

        public override IEnumerable<ValidationResult> Validate(ScriptContext context)
        {
            var results = new List<ValidationResult>();

            // Check prerequisites
            if (context.StructureSet?.Image == null)
                return results;

            // Find BODY structure
            var bodyStructure = context.StructureSet.Structures
                .FirstOrDefault(s => s.Id.Equals("BODY", StringComparison.OrdinalIgnoreCase)
                                  && s.DicomType == "EXTERNAL");

            if (bodyStructure == null)
            {
                results.Add(CreateResult(
                    "PlanningStructures.PTV-Body Proximity",
                    "Cannot validate PTV-Body proximity: BODY structure (type EXTERNAL) not found",
                    ValidationSeverity.Warning
                ));
                return results;
            }

            // Find all PTV structures (only by name prefix, not DICOM type)
            var ptvStructures = context.StructureSet.Structures
                .Where(s => s.Id.StartsWith("PTV", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!ptvStructures.Any())
            {
                // No PTVs found - this is informational, not an error
                return results;
            }

            // Track all PTVs and find closest one
            var ptvProximities = new List<(Structure PTV, double Distance)>();

            foreach (var ptv in ptvStructures)
            {
                var proximityResult = CalculateMinimumDistanceToBodySurface(
                    ptv, bodyStructure, context.StructureSet.Image);

                if (proximityResult.HasValue)
                {
                    double minDistanceMm = proximityResult.Value.MinDistance;
                    ptvProximities.Add((ptv, minDistanceMm));
                }
            }

            // Check proximity results
            if (ptvProximities.Any())
            {
                // Find all PTVs within threshold
                var ptvsTooClose = ptvProximities.Where(p => p.Distance <= PROXIMITY_THRESHOLD_MM).ToList();

                if (ptvsTooClose.Any())
                {
                    // Warning: Show all PTVs that are too close to body surface
                    foreach (var ptv in ptvsTooClose)
                    {
                        results.Add(CreateResult(
                            "PlanningStructures.PTV-Body Proximity",
                            $"PTV {ptv.PTV.Id} is {ptv.Distance:F1} mm from Body surface. Consider creating EVAL structure",
                            ValidationSeverity.Warning
                        ));
                    }
                }
                else
                {
                    // Info: All PTVs have acceptable distance - show only closest
                    var closestPTV = ptvProximities.OrderBy(p => p.Distance).First();
                    results.Add(CreateResult(
                        "PlanningStructures.PTV-Body Proximity",
                        $"Closest PTV {closestPTV.PTV.Id} is {closestPTV.Distance:F1} mm from Body surface",
                        ValidationSeverity.Info
                    ));
                }
            }

            return results;
        }

        /// <summary>
        /// Calculate minimum distance from PTV to Body surface
        /// </summary>
        private (double MinDistance, int SliceIndex, VVector Location)? CalculateMinimumDistanceToBodySurface(
            Structure ptv, Structure body, Image image)
        {
            double minDistance = double.MaxValue;
            int minDistanceSlice = -1;
            VVector minDistanceLocation = new VVector();

            // Iterate through all slices
            for (int sliceIndex = 0; sliceIndex < image.ZSize; sliceIndex++)
            {
                var ptvContours = ptv.GetContoursOnImagePlane(sliceIndex);
                var bodyContours = body.GetContoursOnImagePlane(sliceIndex);

                // Skip if either structure has no contours on this slice
                if (!ptvContours.Any() || !bodyContours.Any())
                    continue;

                // For each PTV contour point
                foreach (var ptvContour in ptvContours)
                {
                    foreach (var ptvPoint in ptvContour)
                    {
                        // Calculate distance to closest point on ANY body contour
                        // (handles multiple contours: outer skin + internal cavities)
                        foreach (var bodyContour in bodyContours)
                        {
                            foreach (var bodyPoint in bodyContour)
                            {
                                // Calculate 3D distance
                                double distance = Math.Sqrt(
                                    Math.Pow(ptvPoint.x - bodyPoint.x, 2) +
                                    Math.Pow(ptvPoint.y - bodyPoint.y, 2) +
                                    Math.Pow(ptvPoint.z - bodyPoint.z, 2)
                                );

                                if (distance < minDistance)
                                {
                                    minDistance = distance;
                                    minDistanceSlice = sliceIndex;
                                    minDistanceLocation = ptvPoint;
                                }
                            }
                        }
                    }
                }
            }

            // Return null if no valid distance was found
            if (minDistanceSlice == -1)
                return null;

            return (minDistance, minDistanceSlice, minDistanceLocation);
        }
    }
}
