using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace PlanCrossCheck
{
    public enum ValidationSeverity
    {
        Error,
        Warning,
        Info
    }

    // Utility methods to avoid duplicated code
    public static class PlanUtilities
    {
        public static bool IsEdgeMachine(string machineId) => machineId == "TrueBeamSN6368";
        public static bool IsHalcyonMachine(string machineId) =>
            machineId?.StartsWith("Halcyon", StringComparison.OrdinalIgnoreCase) ?? false;
        public static bool IsArcBeam(Beam beam) =>
            beam.ControlPoints.First().GantryAngle != beam.ControlPoints.Last().GantryAngle;
        public static bool HasAnyFieldWithCouch(IEnumerable<Beam> beams) =>
            beams?.Any(b => Math.Abs(b.ControlPoints.First().PatientSupportAngle) > 0.1) ?? false;
        public static bool ContainsSRS(string technique) =>
            technique?.Contains("SRS") ?? false;

        // Constants for collision assessment
        private const double ANGLE_TOLERANCE_DEGREES = 0.1;
        private const double STATIC_FIELD_SECTOR_DEGREES = 10.0;

        // Arc analysis helpers for collision assessment

        /// <summary>
        /// Calculate the angular span (in degrees) covered by an arc beam
        /// </summary>
        public static double GetArcSpanDegrees(Beam beam)
        {
            double startAngle = beam.ControlPoints.First().GantryAngle;
            double endAngle = beam.ControlPoints.Last().GantryAngle;

            // If start == end, it's a static field (0 degrees)
            if (Math.Abs(startAngle - endAngle) < ANGLE_TOLERANCE_DEGREES)
                return 0;

            // Calculate span based on gantry direction
            // NOTE: Gantry cannot go through 180° in either direction
            double span;
            if (beam.GantryDirection == GantryDirection.Clockwise)
            {
                // CW: angles INCREASE (0→90→270→0, skipping 180)
                // Example: 181 CW 179 = 181→270→0→179 = 358 degrees
                // Example: 200 CW 220 = 200→210→220 = 20 degrees
                span = (endAngle - startAngle + 360) % 360;
            }
            else // CounterClockwise
            {
                // CCW: angles DECREASE (0→270→90→0, skipping 180)
                // Example: 220 CCW 200 = 220→210→200 = 20 degrees
                // Example: 10 CCW 350 = 10→0→350 = 20 degrees
                span = (startAngle - endAngle + 360) % 360;
            }

            // Handle the case where span is 0 (full 360)
            if (span < ANGLE_TOLERANCE_DEGREES)
                span = 360;

            return span;
        }

        /// <summary>
        /// Determine if treatment beams provide full arc coverage (>= 180 degrees)
        /// </summary>
        public static bool IsFullArcCoverage(IEnumerable<Beam> treatmentBeams)
        {
            if (treatmentBeams == null || !treatmentBeams.Any())
                return false;

            // Check if any single arc spans >= 180 degrees
            foreach (var beam in treatmentBeams)
            {
                double span = GetArcSpanDegrees(beam);
                if (span >= 180)
                    return true;
            }

            // Check if combined coverage >= 180 degrees
            var sectors = GetCoveredAngularSectors(treatmentBeams);
            double totalCoverage = CalculateTotalCoverage(sectors);
            return totalCoverage >= 180;
        }

        /// <summary>
        /// Build list of angular sectors covered by treatment beams
        /// Returns list of normalized (startAngle, endAngle) pairs where start <= end
        /// Wraparound sectors are split into multiple non-wrapping sectors
        /// </summary>
        public static List<(double start, double end)> GetCoveredAngularSectors(IEnumerable<Beam> treatmentBeams)
        {
            var sectors = new List<(double start, double end)>();

            foreach (var beam in treatmentBeams)
            {
                double startAngle = beam.ControlPoints.First().GantryAngle;
                double endAngle = beam.ControlPoints.Last().GantryAngle;

                // Normalize angles to 0-360
                startAngle = (startAngle + 360) % 360;
                endAngle = (endAngle + 360) % 360;

                if (Math.Abs(startAngle - endAngle) < ANGLE_TOLERANCE_DEGREES)
                {
                    // Static field: add +/- STATIC_FIELD_SECTOR_DEGREES
                    double staticStart = startAngle - STATIC_FIELD_SECTOR_DEGREES;
                    double staticEnd = startAngle + STATIC_FIELD_SECTOR_DEGREES;

                    // Normalize and add (may create wraparound sector)
                    staticStart = (staticStart + 360) % 360;
                    staticEnd = (staticEnd + 360) % 360;

                    // Add normalized sectors (split if wraparound)
                    sectors.AddRange(NormalizeSector(staticStart, staticEnd));
                }
                else
                {
                    // Arc: respect gantry direction when building sectors
                    if (beam.GantryDirection == GantryDirection.Clockwise)
                    {
                        // CW: angles INCREASE from startAngle to endAngle
                        if (startAngle > endAngle)
                        {
                            // Wraparound case: e.g., 181 CW 179 → [(181,360), (0,179)]
                            sectors.AddRange(NormalizeSector(startAngle, endAngle));
                        }
                        else
                        {
                            // Normal case: e.g., 200 CW 220 → [(200,220)]
                            sectors.Add((startAngle, endAngle));
                        }
                    }
                    else // CounterClockwise
                    {
                        // CCW: angles DECREASE from startAngle to endAngle
                        // Sector bounds are REVERSED (endAngle to startAngle)
                        if (startAngle > endAngle)
                        {
                            // Normal case: e.g., 220 CCW 200 → [(200,220)]
                            sectors.Add((endAngle, startAngle));
                        }
                        else
                        {
                            // Wraparound case: e.g., 10 CCW 350 → [(350,360), (0,10)]
                            sectors.AddRange(NormalizeSector(endAngle, startAngle));
                        }
                    }
                }
            }

            // Normalize all sectors and merge overlapping ones
            return MergeSectors(sectors);
        }

        /// <summary>
        /// Check if a given angle falls within any of the covered sectors
        /// </summary>
        public static bool IsAngleInSectors(double angle, List<(double start, double end)> sectors)
        {
            // Null/empty check
            if (sectors == null || !sectors.Any())
                return false;

            // Normalize angle to 0-360
            angle = (angle + 360) % 360;

            foreach (var sector in sectors)
            {
                if (IsAngleInSector(angle, sector.start, sector.end))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Normalize a sector by splitting wraparound sectors into non-wrapping parts
        /// Example: (350, 10) becomes [(350, 360), (0, 10)]
        /// Example: (90, 270) stays as [(90, 270)]
        /// </summary>
        private static List<(double start, double end)> NormalizeSector(double start, double end)
        {
            var normalized = new List<(double start, double end)>();

            // Ensure angles are in 0-360 range
            start = (start + 360) % 360;
            end = (end + 360) % 360;

            if (start <= end)
            {
                // Normal sector - no wraparound
                normalized.Add((start, end));
            }
            else
            {
                // Wraparound sector - split into two parts
                // Part 1: from start to 360
                normalized.Add((start, 360));
                // Part 2: from 0 to end
                normalized.Add((0, end));
            }

            return normalized;
        }

        /// <summary>
        /// Check if angle is within a single normalized sector (where start <= end, no wraparound)
        /// </summary>
        private static bool IsAngleInSector(double angle, double start, double end)
        {
            // Since sectors are normalized, start <= end always
            // No need to re-normalize angle if already normalized by caller
            return angle >= start && angle <= end;
        }

        /// <summary>
        /// Merge overlapping or adjacent normalized sectors
        /// Assumes all sectors have start <= end (no wraparound)
        /// </summary>
        private static List<(double start, double end)> MergeSectors(List<(double start, double end)> sectors)
        {
            if (sectors.Count <= 1)
                return sectors;

            // Sort by start angle, then by end angle
            var sorted = sectors.OrderBy(s => s.start).ThenBy(s => s.end).ToList();
            var merged = new List<(double start, double end)>();
            var current = sorted[0];

            for (int i = 1; i < sorted.Count; i++)
            {
                var next = sorted[i];

                // Check if sectors overlap or are adjacent (within 1 degree tolerance)
                // Since all sectors are normalized (start <= end), comparison is simple
                if (next.start <= current.end + 1.0)
                {
                    // Merge sectors - extend current to include next
                    current = (current.start, Math.Max(current.end, next.end));
                }
                else
                {
                    // No overlap, add current to merged list and move to next
                    merged.Add(current);
                    current = next;
                }
            }

            // Add the last sector
            merged.Add(current);

            // Special case: check if first and last sectors should merge across 0/360 boundary
            // If we have sectors like [(0, 30), (340, 360)], they represent a wraparound
            // and should stay as two separate sectors (already normalized)
            // No additional merging needed across the boundary

            return merged;
        }

        /// <summary>
        /// Calculate total angular coverage from normalized sectors
        /// Assumes all sectors have start <= end (no wraparound)
        /// </summary>
        private static double CalculateTotalCoverage(List<(double start, double end)> sectors)
        {
            double total = 0;
            foreach (var sector in sectors)
            {
                // Since sectors are normalized, simply add the span
                total += sector.end - sector.start;
            }
            return total;
        }
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
            AddValidator(new CourseValidator());
            AddValidator(new PlanValidator());
        }
    }

    // 1. Course validation
    public class CourseValidator : ValidatorBase
    {
        public override IEnumerable<ValidationResult> Validate(ScriptContext context)
        {
            var results = new List<ValidationResult>();

            if (context.Course != null)
            {
                bool isValid = Regex.IsMatch(context.Course.Id, @"^RT\d*_");
                results.Add(CreateResult(
                    "Course",
                    isValid ? $"Course ID '{context.Course.Id}' follows the required format (RT[n]_*)"
                           : $"Course ID '{context.Course.Id}' does not start with (RT[n]_*)",
                    isValid ? ValidationSeverity.Info : ValidationSeverity.Error
                ));
            }

            return results;
        }
    }

    // 2. Plan validation (parent)
    public class PlanValidator : CompositeValidator
    {
        public PlanValidator()
        {
            AddValidator(new CTAndPatientValidator());
            AddValidator(new UserOriginMarkerValidator());
            AddValidator(new DoseValidator());
            AddValidator(new FieldsValidator());
            AddValidator(new ReferencePointValidator());
            AddValidator(new FixationValidator());
            AddValidator(new OptimizationValidator());
            AddValidator(new PlanningStructuresValidator());
        }

        public override IEnumerable<ValidationResult> Validate(ScriptContext context)
        {
            var results = new List<ValidationResult>();

            // Run all child validators
            results.AddRange(base.Validate(context));

            if (context.PlanSetup != null)
            {
                // Treatment orientation
                string treatmentOrientation = context.PlanSetup.TreatmentOrientationAsString;
                bool isHFS = treatmentOrientation.Equals("Head First-Supine", StringComparison.OrdinalIgnoreCase);
                results.Add(CreateResult(
                    "Plan.Info",
                    $"Treatment orientation: {treatmentOrientation}" + (!isHFS ? " (non-standard orientation)" : ""),
                    isHFS ? ValidationSeverity.Info : ValidationSeverity.Warning
                ));

                // Gated validation for Edge machines with DIBH in CT ID
                if (context.PlanSetup.Beams.Any() &&
                    PlanUtilities.IsEdgeMachine(context.PlanSetup.Beams.First().TreatmentUnit.Id))
                {
                    var ss = context.StructureSet;
                    if ((ss.Image?.Id?.IndexOf("DIBH", StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (ss.Id?.IndexOf("DIBH", StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (ss.Image?.Series?.Comment?.IndexOf("DIBH", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        bool isGated = context.PlanSetup.UseGating;
                        results.Add(CreateResult(
                            "Plan.Info",
                            isGated ? "Gating is correctly enabled for DIBH plan"
                                    : "Gating should be enabled for DIBH plan",
                            isGated ? ValidationSeverity.Info : ValidationSeverity.Error
                        ));
                    }
                }
            }

            return results;
        }
    }

    // 2.1 Structure Set validator
    public class CTAndPatientValidator : ValidatorBase
    {
        public override IEnumerable<ValidationResult> Validate(ScriptContext context)
        {
            var results = new List<ValidationResult>();

            // User Origin validation
            if (context.StructureSet?.Image != null)
            {
                var userOrigin = context.StructureSet.Image.UserOrigin;

                // X coordinate check
                double xOffset = Math.Abs(userOrigin.x / 10.0); // mm to cm
                bool isXvalid = xOffset <= 0.5;

                results.Add(CreateResult(
                        "CT.UserOrigin",
                        isXvalid ? $"User Origin X coordinate ({userOrigin.x / 10:F1} cm) is within 0.5 cm limits"
                            : $"User Origin X coordinate ({userOrigin.x / 10:F1} cm) is outside acceptable limits",
                        isXvalid ? ValidationSeverity.Info : ValidationSeverity.Warning
                    ));

                // Z coordinate is shown as Y in Eclipse UI
                double zOffset = Math.Abs(userOrigin.z / 10.0); // mm to cm
                bool isZvalid = zOffset <= 0.5;

                results.Add(CreateResult(
                        "CT.UserOrigin",
                        isZvalid ? $"User Origin Y coordinate ({userOrigin.z / 10:F1} cm) is within 0.5 cm limits"
                            : $"User Origin Y coordinate ({userOrigin.z / 10:F1} cm) is outside acceptable limits",
                        isZvalid ? ValidationSeverity.Info : ValidationSeverity.Warning
                    ));

                // Y coordinate is shown as Z in Eclipse UI (with negative sign)
                bool isYValid = userOrigin.y >= -500 && userOrigin.y <= -80;
                results.Add(CreateResult(
                    "CT.UserOrigin",
                    isYValid ? $"User Origin Z coordinate ({-userOrigin.y / 10:F1} cm) is within limits"
                             : $"User Origin Z coordinate ({-userOrigin.y / 10:F1} cm) is outside limits (8 to 50 cm)",
                    isYValid ? ValidationSeverity.Info : ValidationSeverity.Warning
                ));

                // CT imaging device information
                // Get CT series description and imaging device
                string ctSeriesDescription = context.StructureSet.Image.Series.Comment;
                string imagingDevice = context.StructureSet.Image.Series.ImagingDeviceId;

                // Determine expected imaging device based on CT series description
                bool isHeadScan = false;
                if (!string.IsNullOrEmpty(ctSeriesDescription))
                {
                    isHeadScan = ctSeriesDescription.StartsWith("Head", StringComparison.OrdinalIgnoreCase) &&
                                !ctSeriesDescription.StartsWith("Head and Neck", StringComparison.OrdinalIgnoreCase) &&
                                !ctSeriesDescription.StartsWith("Head & Neck", StringComparison.OrdinalIgnoreCase);
                }

                string expectedDevice = isHeadScan ? "CT130265 HEAD" : "CT130265";
                bool isCorrectDevice = imagingDevice == expectedDevice;

                results.Add(CreateResult(
                    "CT.Curve",
                    isCorrectDevice
                        ? $"Correct imaging device '{imagingDevice}' used for {(isHeadScan ? "head" : "non-head")} CT series"
                        : $"Incorrect imaging device '{imagingDevice}' used. " +
                        $"Expected: '{expectedDevice}' for {(isHeadScan ? "head" : "non-head")} " +
                        $"scan (CT series: '{ctSeriesDescription})'",
                    isCorrectDevice ? ValidationSeverity.Info : ValidationSeverity.Error
                ));
            }

            return results;
        }
    }

    // 2.1.1 User Origin CT Marker validator
    public class UserOriginMarkerValidator : ValidatorBase
    {
        // Configuration constants
        private const double THRESHOLD_HU = 1000.0;
        private const double RADIUS_MM = 10.0;

        public override IEnumerable<ValidationResult> Validate(ScriptContext context)
        {
            var results = new List<ValidationResult>();

            // Check prerequisites
            if (context.StructureSet?.Image == null)
                return results;

            var image = context.StructureSet.Image;
            var userOrigin = image.UserOrigin;

            // Find BODY structure
            var bodyStructure = context.StructureSet.Structures
                .FirstOrDefault(s => s.Id.Equals("BODY", StringComparison.OrdinalIgnoreCase)
                                  && s.DicomType == "EXTERNAL");

            if (bodyStructure == null)
            {
                results.Add(CreateResult(
                    "CT.UserOrigin.Markers",
                    "Cannot validate user origin markers: BODY structure (type EXTERNAL) not found",
                    ValidationSeverity.Warning
                ));
                return results;
            }

            // Get treatment orientation
            string orientation = context.PlanSetup?.TreatmentOrientationAsString ?? "Head First-Supine";
            bool isSupine = orientation.Contains("Supine");

            // Calculate slice index
            int sliceIndex = GetSliceIndex(userOrigin.z, image);

            // Check slice bounds
            if (sliceIndex < 0 || sliceIndex >= image.ZSize)
            {
                results.Add(CreateResult(
                    "CT.UserOrigin.Markers",
                    $"User origin is outside CT image bounds (slice {sliceIndex}, image has {image.ZSize} slices)",
                    ValidationSeverity.Error
                ));
                return results;
            }

            // Calculate slice search span based on radius and slice thickness
            double sliceThickness = image.ZRes;
            int sliceSpan = (int)Math.Ceiling(RADIUS_MM / sliceThickness);

            // Search for markers
            var detectedMarkers = new List<string>();
            var detectedPositions = new List<VVector>();

            // Try to find horizontal intersections (left and right)
            var horizontalPoints = FindHorizontalSkinIntersections(
                bodyStructure, userOrigin, sliceIndex, image);

            if (horizontalPoints != null && horizontalPoints.Count == 2)
            {
                // Check left marker
                if (HasMarker(horizontalPoints[0], sliceIndex, sliceSpan, image))
                {
                    detectedMarkers.Add("Left");
                    detectedPositions.Add(horizontalPoints[0]);
                }

                // Check right marker
                if (HasMarker(horizontalPoints[1], sliceIndex, sliceSpan, image))
                {
                    detectedMarkers.Add("Right");
                    detectedPositions.Add(horizontalPoints[1]);
                }
            }

            // Try to find vertical intersection (upper)
            var verticalPoint = FindVerticalSkinIntersection(
                bodyStructure, userOrigin, sliceIndex, image, isSupine);

            if (verticalPoint != null)
            {
                if (HasMarker(verticalPoint.Value, sliceIndex, sliceSpan, image))
                {
                    detectedMarkers.Add("Upper");
                    detectedPositions.Add(verticalPoint.Value);
                }
            }

            // Determine severity and create message
            int markersDetected = detectedMarkers.Count;
            ValidationSeverity severity;
            string message;

            if (markersDetected == 3)
            {
                severity = ValidationSeverity.Info;
                message = $"User origin markers detected (3/3): {string.Join(", ", detectedMarkers)}. " +
                         $"Positions (cm): {string.Join("; ", detectedPositions.Select(p => $"({p.x/10:F1}, {p.y/10:F1}, {p.z/10:F1})"))}. " +
                         $"Threshold={THRESHOLD_HU} HU, radius={RADIUS_MM} mm, slice {sliceIndex}±{sliceSpan}";
            }
            else if (markersDetected == 2)
            {
                severity = ValidationSeverity.Warning;
                message = $"Partial marker detection (2/3): {string.Join(", ", detectedMarkers)}. " +
                         $"Check imaging artifact or marker placement on slice {sliceIndex}±{sliceSpan}.";
            }
            else
            {
                severity = ValidationSeverity.Error;
                message = $"User origin markers not detected ({markersDetected}/3). " +
                         $"Check marker placement and imaging (threshold={THRESHOLD_HU} HU, radius={RADIUS_MM} mm).";
            }

            results.Add(CreateResult("CT.UserOrigin.Markers", message, severity));

            return results;
        }

        private int GetSliceIndex(double userOriginZ, Image image)
        {
            return (int)Math.Round((userOriginZ - image.Origin.z) / image.ZRes);
        }

        private List<VVector> FindHorizontalSkinIntersections(
            Structure body, VVector userOrigin, int sliceIndex, Image image)
        {
            // Get contours on the slice containing user origin
            var contours = body.GetContoursOnImagePlane(sliceIndex);

            if (!contours.Any())
                return null;

            // Find the contour that contains the user origin (or closest)
            var targetContour = FindContourContainingPoint(contours, userOrigin.x, userOrigin.y);

            if (targetContour == null)
                return null;

            // Find intersections of horizontal line y = userOrigin.y with the contour
            double y0 = userOrigin.y;
            double x0 = userOrigin.x;

            var intersections = new List<VVector>();

            for (int i = 0; i < targetContour.Length; i++)
            {
                var p1 = targetContour[i];
                var p2 = targetContour[(i + 1) % targetContour.Length];

                // Check if the segment crosses the horizontal line
                if ((p1.y <= y0 && p2.y >= y0) || (p1.y >= y0 && p2.y <= y0))
                {
                    // Avoid division by zero
                    if (Math.Abs(p2.y - p1.y) < 0.001)
                        continue;

                    // Interpolate to find x coordinate at y = y0
                    double t = (y0 - p1.y) / (p2.y - p1.y);
                    double x = p1.x + t * (p2.x - p1.x);
                    double z = p1.z + t * (p2.z - p1.z);

                    intersections.Add(new VVector(x, y0, z));
                }
            }

            if (intersections.Count < 2)
                return null;

            // Find the two intersections closest to x0 (one on each side)
            var leftIntersections = intersections.Where(p => p.x < x0).OrderByDescending(p => p.x).ToList();
            var rightIntersections = intersections.Where(p => p.x > x0).OrderBy(p => p.x).ToList();

            if (!leftIntersections.Any() || !rightIntersections.Any())
                return null;

            return new List<VVector> { leftIntersections.First(), rightIntersections.First() };
        }

        private VVector? FindVerticalSkinIntersection(
            Structure body, VVector userOrigin, int sliceIndex, Image image, bool isSupine)
        {
            var contours = body.GetContoursOnImagePlane(sliceIndex);

            if (!contours.Any())
                return null;

            var targetContour = FindContourContainingPoint(contours, userOrigin.x, userOrigin.y);

            if (targetContour == null)
                return null;

            // Find intersections of vertical line x = userOrigin.x with the contour
            double x0 = userOrigin.x;
            double y0 = userOrigin.y;

            var intersections = new List<VVector>();

            for (int i = 0; i < targetContour.Length; i++)
            {
                var p1 = targetContour[i];
                var p2 = targetContour[(i + 1) % targetContour.Length];

                // Check if the segment crosses the vertical line
                if ((p1.x <= x0 && p2.x >= x0) || (p1.x >= x0 && p2.x <= x0))
                {
                    // Avoid division by zero
                    if (Math.Abs(p2.x - p1.x) < 0.001)
                        continue;

                    // Interpolate to find y coordinate at x = x0
                    double t = (x0 - p1.x) / (p2.x - p1.x);
                    double y = p1.y + t * (p2.y - p1.y);
                    double z = p1.z + t * (p2.z - p1.z);

                    intersections.Add(new VVector(x0, y, z));
                }
            }

            if (intersections.Count == 0)
                return null;

            // Select the "upper" intersection based on orientation
            // In DICOM: y increases toward posterior (back)
            if (isSupine)
            {
                // For supine: upper = most anterior = smallest y
                return intersections.OrderBy(p => p.y).First();
            }
            else
            {
                // For prone: upper = most posterior = largest y
                return intersections.OrderByDescending(p => p.y).First();
            }
        }

        private VVector[] FindContourContainingPoint(VVector[][] contours, double x, double y)
        {
            // First try to find a contour that contains the point
            foreach (var contour in contours)
            {
                if (IsPointInPolygon(contour, x, y))
                    return contour;
            }

            // If no contour contains it, find the closest one
            double minDistance = double.MaxValue;
            VVector[] closestContour = null;

            foreach (var contour in contours)
            {
                foreach (var point in contour)
                {
                    double dist = Math.Sqrt(Math.Pow(point.x - x, 2) + Math.Pow(point.y - y, 2));
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closestContour = contour;
                    }
                }
            }

            return closestContour;
        }

        private bool IsPointInPolygon(VVector[] polygon, double x, double y)
        {
            // Ray casting algorithm
            bool inside = false;
            int j = polygon.Length - 1;

            for (int i = 0; i < polygon.Length; i++)
            {
                if ((polygon[i].y > y) != (polygon[j].y > y) &&
                    x < (polygon[j].x - polygon[i].x) * (y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
                {
                    inside = !inside;
                }
                j = i;
            }

            return inside;
        }

        private bool HasMarker(VVector point, int centerSlice, int sliceSpan, Image image)
        {
            // Search in a radius around the point, across multiple slices
            int radiusPixelsX = (int)Math.Ceiling(RADIUS_MM / image.XRes);
            int radiusPixelsY = (int)Math.Ceiling(RADIUS_MM / image.YRes);

            // Convert point to voxel indices
            int centerX = (int)Math.Round((point.x - image.Origin.x) / image.XRes);
            int centerY = (int)Math.Round((point.y - image.Origin.y) / image.YRes);

            // Search across slices
            for (int sliceOffset = -sliceSpan; sliceOffset <= sliceSpan; sliceOffset++)
            {
                int z = centerSlice + sliceOffset;

                // Check slice bounds
                if (z < 0 || z >= image.ZSize)
                    continue;

                // Get voxel data for this slice
                int[,] voxelBuffer = new int[image.XSize, image.YSize];
                image.GetVoxels(z, voxelBuffer);

                // Search in a circular region
                for (int dx = -radiusPixelsX; dx <= radiusPixelsX; dx++)
                {
                    for (int dy = -radiusPixelsY; dy <= radiusPixelsY; dy++)
                    {
                        int x = centerX + dx;
                        int y = centerY + dy;

                        // Check bounds
                        if (x < 0 || x >= image.XSize || y < 0 || y >= image.YSize)
                            continue;

                        // Check if within radius (spherical, not cubic)
                        double distMm = Math.Sqrt(
                            Math.Pow(dx * image.XRes, 2) +
                            Math.Pow(dy * image.YRes, 2) +
                            Math.Pow(sliceOffset * image.ZRes, 2)
                        );

                        if (distMm > RADIUS_MM)
                            continue;

                        // Convert voxel value to HU
                        double hu = image.VoxelToDisplayValue(voxelBuffer[x, y]);

                        // Check threshold - if at least 1 voxel is above threshold, marker is detected
                        if (hu >= THRESHOLD_HU)
                            return true;
                    }
                }
            }

            return false;
        }
    }

    // 2.2 Dose validator
    public class DoseValidator : ValidatorBase
    {
        public override IEnumerable<ValidationResult> Validate(ScriptContext context)
        {
            var results = new List<ValidationResult>();

            if (context.PlanSetup != null && context.PlanSetup.Dose != null)
            {
                // Check if any field has SRS in technique
                bool isSRSPlan = context.PlanSetup.Beams.Any(b =>
                    !b.IsSetupField && b.Technique.ToString().Contains("SRS"));

                // Dose grid size validation
                double doseGridSize = context.PlanSetup.Dose.XRes / 10.0; // Convert mm to cm
                bool isValidGrid = isSRSPlan ? doseGridSize <= 0.125 : doseGridSize <= 0.2;

                results.Add(CreateResult(
                    "Dose.Grid",
                    isValidGrid
                        ? $"Dose grid size ({doseGridSize:F3} cm) is valid" + (isSRSPlan ? " for SRS plan" : "")
                        : $"Dose grid size ({doseGridSize:F3} cm) is too large" + (isSRSPlan
                            ? " (should be ≤ 0.125 cm for SRS plans)"
                            : " (should be ≤ 0.2 cm)"),
                    isValidGrid ? ValidationSeverity.Info : ValidationSeverity.Error
                ));

                // SRS technique validation for high-dose plans
                if (context.PlanSetup.DosePerFraction.Dose >= 5)
                {
                    foreach (var beam in context.PlanSetup.Beams.Where(b => !b.IsSetupField))
                    {
                        bool hasSRSTechnique = beam.Technique.ToString().Contains("SRS");
                        results.Add(CreateResult(
                            "Dose.Technique",
                            hasSRSTechnique
                                ? $"Field '{beam.Id}' correctly uses SRS technique " +
                                $"for ≥5Gy/fraction ({context.PlanSetup.DosePerFraction})"
                                : $"Field '{beam.Id}' should use SRS technique " +
                                $"for ≥5Gy/fraction ({context.PlanSetup.DosePerFraction})",
                            hasSRSTechnique ? ValidationSeverity.Info : ValidationSeverity.Error,
                            true
                        ));
                    }
                }

                // Energy-dose rate checks
                foreach (var beam in context.PlanSetup.Beams.Where(b => !b.IsSetupField))
                {
                    string machineId = beam.TreatmentUnit.Id;
                    string energy = beam.EnergyModeDisplayName;
                    double doseRate = beam.DoseRate;

                    bool isEdgeMachine = PlanUtilities.IsEdgeMachine(machineId);
                    bool isHalcyonMachine = PlanUtilities.IsHalcyonMachine(machineId);

                    // Expected dose rates based on machine and energy
                    double expectedDoseRate = -1;

                    if (isEdgeMachine && context.PlanSetup.DosePerFraction.Dose >= 5)
                    {
                        // Energy validation for Edge machine with high dose/fraction
                        bool isValidEnergy = energy == "6X-FFF" || energy == "10X-FFF";
                        results.Add(CreateResult(
                            "Dose.Energy",
                            isValidEnergy
                                ? $"Field '{beam.Id}' correctly uses FFF energy ({energy}) for dose/fraction ≥5Gy"
                                : $"Field '{beam.Id}' should use 6FFF or 10FFF energy for dose/fraction ≥5Gy, " +
                                $"found: {energy}",
                            isValidEnergy ? ValidationSeverity.Info : ValidationSeverity.Error,
                            true
                        ));

                        if (energy == "6X-FFF") expectedDoseRate = 1400;
                        else if (energy == "10X-FFF") expectedDoseRate = 2400;
                        else if (energy == "6X" || energy == "10X") expectedDoseRate = 600;
                    }
                    else if (isHalcyonMachine)
                    {
                        if (energy == "6X-FFF") expectedDoseRate = 600;
                    }

                    // Only validate if we have an expected dose rate value
                    if (expectedDoseRate > 0)
                    {
                        bool isValidDoseRate = doseRate == expectedDoseRate;

                        results.Add(CreateResult(
                            "Dose.DoseRate",
                            isValidDoseRate
                                ? $"Field '{beam.Id}' has correct dose rate ({doseRate} MU/min) for {energy}"
                                : $"Field '{beam.Id}' has incorrect dose rate ({doseRate} MU/min) " +
                                $"for {energy} (should be {expectedDoseRate} MU/min)",
                            isValidDoseRate ? ValidationSeverity.Info : ValidationSeverity.Error,
                            true
                        ));
                    }
                }
            }

            return results;
        }
    }

    // 2.3 Fields validator (parent)
    public class FieldsValidator : CompositeValidator
    {
        public FieldsValidator()
        {
            AddValidator(new FieldNamesValidator());
            AddValidator(new GeometryValidator());
            AddValidator(new SetupFieldsValidator());
        }
    }

    // 2.3.1 Field names validator
    public class FieldNamesValidator : ValidatorBase
    {
        public override IEnumerable<ValidationResult> Validate(ScriptContext context)
        {
            var results = new List<ValidationResult>();

            if (context.PlanSetup?.Beams != null)
            {
                var beams = context.PlanSetup.Beams;
                bool hasAnyFieldWithCouch = PlanUtilities.HasAnyFieldWithCouch(beams);
                
                foreach (var beam in beams)
                {
                    if (!beam.IsSetupField)
                    {
                        bool isValid = IsValidTreatmentFieldName(beam, beams, hasAnyFieldWithCouch);
                        results.Add(CreateResult(
                            "Fields.Names",
                            isValid ? $"Field '{beam.Id}' follows naming convention"
                                   : $"Field '{beam.Id}' does not follow naming convention",
                            isValid ? ValidationSeverity.Info : ValidationSeverity.Warning,
                            true
                        ));
                    }
                }
            }

            return results;
        }

        private bool IsValidTreatmentFieldName(Beam beam, IEnumerable<Beam> allBeams, bool hasAnyFieldWithCouch)
        {
            int couchAngle = (int)Math.Round(beam.ControlPoints.First().PatientSupportAngle);
            double startGantryExact = Math.Round(beam.ControlPoints.First().GantryAngle, 1);
            double endGantryExact = Math.Round(beam.ControlPoints.Last().GantryAngle, 1);

            // Special handling for SRS HyperArc
            bool isSRSHyperArc = beam.Technique?.ToString().Contains("SRS HyperArc") ?? false;

            int startGantry, endGantry;

            if (isSRSHyperArc)
            {
                // Special handling for HyperArc
                // If 180.1, use 181; if 179.9, use 179
                startGantry = (startGantryExact == 180.1) ? 181 :
                              (startGantryExact == 179.9) ? 179 :
                              (int)Math.Round(startGantryExact);

                endGantry = (endGantryExact == 180.1) ? 181 :
                            (endGantryExact == 179.9) ? 179 :
                            (int)Math.Round(endGantryExact);
            }
            else
            {
                // Standard rounding for other techniques
                startGantry = (int)Math.Round(startGantryExact);
                endGantry = (int)Math.Round(endGantryExact);
            }

            bool isArc = startGantry != endGantry;
            string id = beam.Id;

            if (isArc)
            {
                var arcPattern = hasAnyFieldWithCouch ? @"^T(\d+)-(\d+)(CW|CCW)(\d+)-[A-Z]$" : @"^(\d+)(CW|CCW)(\d+)-[A-Z]$";
                var arcMatch = Regex.Match(id, arcPattern);
                if (!arcMatch.Success) return false;

                if (hasAnyFieldWithCouch)
                {
                    int nameCouchAngle = int.Parse(arcMatch.Groups[1].Value);
                    int nameStartAngle = int.Parse(arcMatch.Groups[2].Value);
                    string nameDirection = arcMatch.Groups[3].Value;
                    int nameEndAngle = int.Parse(arcMatch.Groups[4].Value);

                    if (nameCouchAngle != couchAngle) return false;
                    if (nameStartAngle != startGantry) return false;
                    if (nameEndAngle != endGantry) return false;
                    if (((beam.GantryDirection == GantryDirection.Clockwise) && (nameDirection != "CW")) ||
                        ((beam.GantryDirection == GantryDirection.CounterClockwise) && (nameDirection != "CCW")))
                        return false;

                    return true;
                }
                else
                {
                    int nameStartAngle = int.Parse(arcMatch.Groups[1].Value);
                    string nameDirection = arcMatch.Groups[2].Value;
                    int nameEndAngle = int.Parse(arcMatch.Groups[3].Value);

                    if (nameStartAngle != startGantry) return false;
                    if (nameEndAngle != endGantry) return false;
                    if (((beam.GantryDirection == GantryDirection.Clockwise) && (nameDirection != "CW")) ||
                        ((beam.GantryDirection == GantryDirection.CounterClockwise) && (nameDirection != "CCW")))
                        return false;

                    return true;
                }
            }
            else
            {
                var staticPattern = hasAnyFieldWithCouch ? @"^T(\d+)-G(\d+)-[A-Z]$" : @"^G(\d+)-[A-Z]$";
                var staticMatch = Regex.Match(id, staticPattern);
                if (!staticMatch.Success) return false;

                if (hasAnyFieldWithCouch)
                {
                    int nameCouchAngle = int.Parse(staticMatch.Groups[1].Value);
                    int nameGantryAngle = int.Parse(staticMatch.Groups[2].Value);

                    if (nameCouchAngle != couchAngle) return false;
                    if (nameGantryAngle != startGantry) return false;
                    return true;
                }
                else
                {
                    int nameGantryAngle = int.Parse(staticMatch.Groups[1].Value);
                    if (nameGantryAngle != startGantry) return false;
                    return true;
                }
            }
        }
    }

    // 2.3.2 Geometry validator
    public class GeometryValidator : ValidatorBase
    {
        // Check MLC overlapping for divided fields 
        private void CheckMLCOverlapForDuplicatedCollimators(IEnumerable<Beam> beams, List<ValidationResult> results)
        {
            // Only check for Halcyon machines and plans without couch rotation
            if (!beams.Any() || !PlanUtilities.IsHalcyonMachine(beams.First().TreatmentUnit.Id))
                return;

            if (PlanUtilities.HasAnyFieldWithCouch(beams))
                return;

            var beamsByCollimator = beams
                .Where(b => !b.IsSetupField)
                .GroupBy(b => Math.Round(b.ControlPoints.First().CollimatorAngle, 1))
                .Where(g => g.Count() > 1);

            foreach (var group in beamsByCollimator)
            {
                var beamList = group.ToList();
                double collimatorAngle = group.Key;

                for (int i = 0; i < beamList.Count - 1; i++)
                {
                    for (int j = i + 1; j < beamList.Count; j++)
                    {
                        var beam1 = beamList[i];
                        var beam2 = beamList[j];

                        // Get jaw positions from first control point
                        var cp1 = beam1.ControlPoints.First();
                        var cp2 = beam2.ControlPoints.First();

                        double x1_beam1 = cp1.JawPositions.X1;
                        double x2_beam1 = cp1.JawPositions.X2;
                        double x1_beam2 = cp2.JawPositions.X1;
                        double x2_beam2 = cp2.JawPositions.X2;

                        // Calculate overlap (positive value means overlap exists)
                        double overlapStart = Math.Max(x1_beam1, x1_beam2);
                        double overlapEnd = Math.Min(x2_beam1, x2_beam2);
                        double overlap = overlapEnd - overlapStart;

                        if (overlap > 0)
                        {
                            results.Add(CreateResult(
                                "Fields.Geometry.MLCOverlap",
                                $"Fields '{beam1.Id}' and '{beam2.Id}' with collimator {collimatorAngle:F1}° have {overlap / 10:F1} cm jaw overlap " +
                                $"(X1/X2: {x1_beam1 / 10:F1}/{x2_beam1 / 10:F1} cm and {x1_beam2 / 10:F1}/{x2_beam2 / 10:F1} cm)",
                                ValidationSeverity.Info,
                                true
                            ));
                        }
                        else
                        {
                            results.Add(CreateResult(
                                "Fields.Geometry.MLCOverlap",
                                $"Fields '{beam1.Id}' and '{beam2.Id}' with collimator {collimatorAngle:F1}° have no jaw overlap",
                                ValidationSeverity.Warning,
                                true
                            ));
                        }
                    }
                }
            }
        }

        public override IEnumerable<ValidationResult> Validate(ScriptContext context)
        {
            var results = new List<ValidationResult>();

            if (context.PlanSetup?.Beams != null)
            {
                // Fields.Geometry.Collimator - check for duplicates
                var collimatorAngles = context.PlanSetup.Beams
                    .Where(b => !b.IsSetupField)
                    .Select(b => b.ControlPoints.First().CollimatorAngle)
                    .ToList();

                var duplicateAngles = collimatorAngles
                    .GroupBy(a => a)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToHashSet();

                var beams = context.PlanSetup.Beams;

                foreach (var beam in beams)
                {
                    // Collimator angle validation
                    if (!beam.IsSetupField)
                    {
                        var angle = beam.ControlPoints.First().CollimatorAngle;
                        bool isInvalidRange = (angle > 268 && angle < 272) ||
                                             (angle > 358 || angle < 2) ||
                                             (angle > 88 && angle < 92);
                        bool isDuplicate = duplicateAngles.Contains(angle);

                        ValidationSeverity severity;
                        if (isInvalidRange)
                            severity = ValidationSeverity.Error;
                        else if (isDuplicate)
                            severity = ValidationSeverity.Warning;
                        else
                            severity = ValidationSeverity.Info;

                        results.Add(CreateResult(
                            "Fields.Geometry.Collimator",
                            severity == ValidationSeverity.Info ? $"Collimator angle {angle:F1}° is valid" :
                            severity == ValidationSeverity.Warning ? $"Collimator angle {angle:F1}° is duplicated" :
                            $"Invalid collimator angle {angle:F1}°",
                            severity,
                            true
                        ));
                    }

                    // Machine-specific validations
                    string machineId = beam.TreatmentUnit.Id;

                    // Isocenter validation for Halcyon
                    if (PlanUtilities.IsHalcyonMachine(machineId))
                    {
                        var isocenter = beam.IsocenterPosition;
                        var userOrigin = context.StructureSet?.Image?.UserOrigin ?? new VVector(0, 0, 0);

                        // In IEC, Y corresponds to Z in DICOM, relative to User Origin
                        double iecY = (isocenter.z - userOrigin.z) / 10.0; // Convert from mm to cm

                        bool isValid = iecY > -30 && iecY < 17;

                        results.Add(CreateResult(
                            "Fields.Geometry.Isocenter",
                            isValid ? $"Field '{beam.Id}' isocenter Y position ({iecY:F1} cm) " +
                            $"is within Halcyon limits (-30 to +17 cm)"
                                   : $"Field '{beam.Id}' isocenter Y position ({iecY:F1} cm) " +
                                   $"is outside Halcyon limits (-30 to +17 cm)",
                            isValid ? ValidationSeverity.Info : ValidationSeverity.Error,
                            true
                        ));
                    }

                    // Tolerance table validation
                    if (PlanUtilities.IsHalcyonMachine(machineId) || PlanUtilities.IsEdgeMachine(machineId))
                    {
                        string toleranceTable = beam.ToleranceTableLabel;
                        string expectedTable = PlanUtilities.IsHalcyonMachine(machineId) ? "HAL" : "EDGE";
                        bool isValid = toleranceTable == expectedTable;

                        results.Add(CreateResult(
                            "Fields.Geometry.ToleranceTable",
                            isValid ? $"Field '{beam.Id}' has correct tolerance table ({toleranceTable})"
                                   : $"Field '{beam.Id}' has incorrect tolerance table. " +
                                   $"Expected: {expectedTable}, Found: {toleranceTable}",
                            isValid ? ValidationSeverity.Info : ValidationSeverity.Warning,
                            true
                        ));
                    }
                }

                // For plans without couch rotation
                if (!PlanUtilities.HasAnyFieldWithCouch(beams))
                {
                    // Use BeamsInTreatmentOrder to get the actual first field in treatment delivery order
                    var firstBeam = context.PlanSetup.BeamsInTreatmentOrder
                        .FirstOrDefault(b => !b.IsSetupField);
                    if (firstBeam != null)
                    {
                        double firstGantryAngle = firstBeam.ControlPoints.First().GantryAngle;

                        // Find angle closest to 180
                        double deviationFrom180 = Math.Abs(firstGantryAngle - 180);
                        bool firstFieldStartOK = deviationFrom180 > 90 ? false : true;
                        results.Add(CreateResult(
                            "Fields.Geometry.1st Field Start Angle",
                            firstFieldStartOK ? $"First field '{firstBeam.Id}' correctly starts " +
                                $"at {firstGantryAngle:F1}° - closest to the 180°"
                                : $"First field '{firstBeam.Id}' starts at {firstGantryAngle:F1}° (should be close to 180°)",
                            firstFieldStartOK ? ValidationSeverity.Info : ValidationSeverity.Warning,
                            true
                        ));
                    }
                }

                // Check for MLC overlapping for divided fields
                CheckMLCOverlapForDuplicatedCollimators(beams, results);

            }

            return results;
        }
    }

    // 2.3.3. Setup fields validator
    public class SetupFieldsValidator : ValidatorBase
    {
        public override IEnumerable<ValidationResult> Validate(ScriptContext context)
        {
            var results = new List<ValidationResult>();

            if (context.PlanSetup?.Beams != null)
            {
                var setupFields = context.PlanSetup.Beams.Where(b => b.IsSetupField).ToList();
                string machineId = context.PlanSetup.Beams.FirstOrDefault()?.TreatmentUnit.Id;

                // Check setup field count based on machine type
                if (PlanUtilities.IsHalcyonMachine(machineId))
                {
                    bool hasCorrectCount = setupFields.Count == 1;
                    results.Add(CreateResult(
                        "Fields.SetupFields",
                        hasCorrectCount ? "Plan has the required 1 setup field for Halcyon"
                                       : $"Invalid setup field count for Halcyon: {setupFields.Count} (should be 1)",
                        hasCorrectCount ? ValidationSeverity.Info : ValidationSeverity.Error
                    ));
                }
                else if (PlanUtilities.IsEdgeMachine(machineId))
                {
                    bool hasCorrectCount = setupFields.Count == 2;
                    bool hasCBCT = setupFields.Any(f => f.Id.ToUpperInvariant() == "CBCT");
                    bool hasSF0 = setupFields.Any(f => f.Id.ToUpperInvariant() == "SF-0");
                    bool hasCorrectFields = hasCBCT && hasSF0;

                    results.Add(CreateResult(
                        "Fields.SetupFields",
                        hasCorrectCount ? "Plan has the required 2 setup fields for Edge"
                                       : $"Invalid setup field count for Edge: {setupFields.Count} (should be 2)",
                        hasCorrectCount ? ValidationSeverity.Info : ValidationSeverity.Error
                    ));

                    if (hasCorrectCount && !hasCorrectFields)
                    {
                        results.Add(CreateResult(
                            "Fields.SetupFields",
                            "Edge setup fields should be named 'CBCT' and 'SF-0'",
                            ValidationSeverity.Error  // Add the severity parameter
                        ));
                    }
                }

                // Validate each setup field's parameters (existing code)
                foreach (var beam in setupFields)
                {
                    // Original setup field parameter validation...
                    string id = beam.Id.ToUpperInvariant();
                    bool isHalcyon = PlanUtilities.IsHalcyonMachine(machineId);

                    if (isHalcyon)
                    {
                        bool isValid = id == "KVCBCT";
                        results.Add(CreateResult(
                            "Fields.SetupFields",
                            isValid ? $"Setup field '{beam.Id}' configuration is valid for Halcyon"
                                   : $"Invalid setup field for Halcyon: should be 'kVCBCT'",
                            isValid ? ValidationSeverity.Info : ValidationSeverity.Error,
                            true
                        ));
                    }
                    else
                    {
                        string energy = beam.EnergyModeDisplayName;
                        bool isValidName = id == "CBCT" || id.StartsWith("SF-");
                        bool isValidEnergy = energy == "6X" || energy == "10X";

                        bool isValid = isValidName && isValidEnergy;
                        results.Add(CreateResult(
                            "Fields.SetupFields",
                            isValid ? $"Setup field '{beam.Id}' configuration is valid"
                                   : $"Invalid setup field configuration: {beam.Id} with energy {energy}",
                            isValid ? ValidationSeverity.Info : ValidationSeverity.Error,
                            true
                        ));
                    }
                }
            }

            return results;
        }
    }

    // 2.4 Optimization parameters validator
    public class OptimizationValidator : ValidatorBase
    {
        public override IEnumerable<ValidationResult> Validate(ScriptContext context)
        {
            var results = new List<ValidationResult>();

            // Check optimization options
            if (context.PlanSetup != null && context.PlanSetup.Beams.Any())
            {
                string machineId = context.PlanSetup.Beams.First().TreatmentUnit.Id;
                bool isEdgeMachine = PlanUtilities.IsEdgeMachine(machineId);
                bool isSRSPlan = context.PlanSetup.Beams.Any(b =>
                    !b.IsSetupField && PlanUtilities.ContainsSRS(b.Technique.ToString()));

                if (isEdgeMachine)
                {
                    // Check JawTracking usage for EDGE machine
                    bool jawTrackingUsed = context.PlanSetup.OptimizationSetup.
                        Parameters.Any(p => p is OptimizationJawTrackingUsedParameter);

                    results.Add(CreateResult(
                        "Plan.Optimization",
                        jawTrackingUsed
                            ? "Jaw Tracking is used for Edge plan"
                            : "Jaw Tracking is NOT used for Edge plan",
                        jawTrackingUsed
                            ? ValidationSeverity.Info
                            : ValidationSeverity.Warning));

                    // Check ASC for Edge SRS plans
                    if (isSRSPlan)
                    {
                        // Get VMAT optimization parameters
                        var optModel = context.PlanSetup.GetCalculationModel(CalculationType.PhotonOptimization);
                        var vmatParams = context.PlanSetup.GetCalculationOptions(optModel);

                        if (vmatParams != null && vmatParams.ContainsKey("VMAT/ApertureShapeController"))
                        {
                            string ascValue = vmatParams["VMAT/ApertureShapeController"];
                            bool isValidASC = ascValue == "High" || ascValue == "Very High";

                            results.Add(CreateResult(
                                "Plan.Optimization",
                                isValidASC
                                    ? $"Aperture Shape Controller is set to '{ascValue}' for Edge SRS plan"
                                    : $"Aperture Shape Controller is set to '{ascValue}' - " +
                                    $"not 'High' or 'Very High' for Edge SRS plans",
                                isValidASC
                                    ? ValidationSeverity.Info
                                    : ValidationSeverity.Warning
                            ));
                        }
                        else
                        {
                            results.Add(CreateResult(
                                "Plan.Optimization",
                                "Cannot determine Aperture Shape Controller setting for Edge SRS plan",
                                ValidationSeverity.Warning
                            ));
                        }
                    }
                }
            }

            return results;
        }
    }

    // 3 Reference points validator
    public class ReferencePointValidator : ValidatorBase
    {
        public override IEnumerable<ValidationResult> Validate(ScriptContext context)
        {
            var results = new List<ValidationResult>();

            // Check all reference points for RP_ prefix and Target type
            if (context.PlanSetup?.ReferencePoints != null)
            {
                foreach (var refPoint in context.PlanSetup.ReferencePoints)
                {
                    if (refPoint.Id.StartsWith("RP_", StringComparison.OrdinalIgnoreCase))
                    {
                        bool isTargetType = refPoint.ReferencePointType == ReferencePointType.Target;
                        results.Add(CreateResult(
                            "Dose.ReferencePoint",
                            isTargetType
                                ? $"Reference point '{refPoint.Id}' correctly has type 'Target'"
                                : $"Reference point '{refPoint.Id}' should have type 'Target' (current: {refPoint.ReferencePointType})",
                            isTargetType ? ValidationSeverity.Info : ValidationSeverity.Warning
                        ));
                    }
                }
            }

            if (context.PlanSetup?.PrimaryReferencePoint != null)
            {
                var refPoint = context.PlanSetup.PrimaryReferencePoint;

                // Check reference point name
                bool isNameValid = refPoint.Id.StartsWith("RP_", StringComparison.OrdinalIgnoreCase);
                results.Add(CreateResult(
                    "Dose.ReferencePoint",
                    isNameValid
                        ? $"Primary reference point name '{refPoint.Id}' follows naming convention (RP_*)"
                        : $"Primary reference point name '{refPoint.Id}' should start with 'RP_'",
                    isNameValid ? ValidationSeverity.Info : ValidationSeverity.Error
                ));

                // Check reference point doses
                // Get doses from plan
                double totalPrescribedDose = context.PlanSetup.TotalDose.Dose;
                double dosePerFraction = context.PlanSetup.DosePerFraction.Dose;

                // Expected reference point doses
                double expectedTotalDose = totalPrescribedDose + 0.1;
                double expectedDailyDose = dosePerFraction + 0.1;

                // Get actual doses from reference point
                double actualTotalDose = context.PlanSetup.PrimaryReferencePoint.TotalDoseLimit.Dose;
                double actualDailyDose = context.PlanSetup.PrimaryReferencePoint.DailyDoseLimit.Dose;
                double actualSessionDose = context.PlanSetup.PrimaryReferencePoint.SessionDoseLimit.Dose;

                // Validate total dose
                bool isTotalDoseValid = Math.Abs(actualTotalDose - expectedTotalDose) <= 0.09;
                results.Add(CreateResult(
                    "Dose.ReferencePoint",
                    isTotalDoseValid
                        ? $"Total reference point dose ({actualTotalDose:F2} Gy) " +
                        $"is correct: Total+0.1={expectedTotalDose:F2} Gy"
                        : $"Total reference point dose ({actualTotalDose:F2} Gy) " +
                        $"is incorrect: Total+0.1={expectedTotalDose:F2} Gy",
                    isTotalDoseValid ? ValidationSeverity.Info : ValidationSeverity.Error
                ));

                // Validate daily dose
                bool isDailyDoseValid = Math.Abs(actualDailyDose - expectedDailyDose) <= 0.09;
                results.Add(CreateResult(
                    "Dose.ReferencePoint",
                    isDailyDoseValid
                        ? $"Daily reference point dose ({actualDailyDose:F2} Gy) " +
                        $"is correct: Fraction+0.1=({expectedDailyDose:F2} Gy)"
                        : $"Daily reference point dose ({actualDailyDose:F2} Gy) " +
                        $"is incorrect: Fraction+0.1=({expectedDailyDose:F2} Gy)",
                    isDailyDoseValid ? ValidationSeverity.Info : ValidationSeverity.Error
                ));

                // Validate session dose
                bool isSessionDoseValid = Math.Abs(actualSessionDose - expectedDailyDose) <= 0.09;
                results.Add(CreateResult(
                    "Dose.ReferencePoint",
                    isSessionDoseValid
                        ? $"Session reference point dose ({actualSessionDose:F2} Gy) " +
                        $"is correct: Fraction+0.1=({expectedDailyDose:F2} Gy)"
                        : $"Session reference point dose ({actualSessionDose:F2} Gy) " +
                        $"is incorrect: Fraction+0.1=({expectedDailyDose:F2} Gy)",
                    isSessionDoseValid ? ValidationSeverity.Info : ValidationSeverity.Error
                ));

                // Check if prescription dose matches plan dose
                if (context.PlanSetup?.RTPrescription != null)
                {
                    double PrescriptionTotalDose = 0;
                    double PrescriptionFractionDose = 0;

                    // Iterate through all prescription targets
                    foreach (var target in context.PlanSetup.RTPrescription.Targets)
                    {
                        // Get fraction dose and calculate total dose for this target
                        double fractionTargetDose = target.DosePerFraction.Dose;
                        double totalTargetDose = fractionTargetDose * target.NumberOfFractions;

                        if (totalTargetDose > PrescriptionTotalDose)
                        {
                            PrescriptionTotalDose = totalTargetDose;
                            PrescriptionFractionDose = fractionTargetDose;
                        }
                    }

                    if (PrescriptionTotalDose > 0)
                    {
                        bool isTotalDoseMatch = Math.Abs(PrescriptionTotalDose - totalPrescribedDose) < 0.01;
                        bool isFractionDoseMatch = Math.Abs(PrescriptionFractionDose - dosePerFraction) < 0.01;

                        results.Add(CreateResult(
                            "Dose.Prescription",
                            isTotalDoseMatch
                                ? $"Plan dose ({totalPrescribedDose:F2} Gy) " +
                                $"matches prescription dose ({PrescriptionTotalDose:F2} Gy)"
                                : $"Plan dose ({totalPrescribedDose:F2} Gy) " +
                                $"does not match prescription dose ({PrescriptionTotalDose:F2} Gy)",
                            isTotalDoseMatch ? ValidationSeverity.Info : ValidationSeverity.Error
                        ));

                        results.Add(CreateResult(
                            "Dose.Prescription",
                            isFractionDoseMatch
                                ? $"Plan fraction dose ({dosePerFraction:F2} Gy) " +
                                $"matches prescription dose per fraction ({PrescriptionFractionDose:F2} Gy)"
                                : $"Plan fraction dose ({dosePerFraction:F2} Gy) " +
                                $"does not match prescription dose per fraction ({PrescriptionFractionDose:F2} Gy)",
                            isFractionDoseMatch ? ValidationSeverity.Info : ValidationSeverity.Error
                        ));
                    }
                    else
                    {
                        results.Add(CreateResult(
                            "Dose.Prescription",
                            "No dose values found in prescription targets",
                            ValidationSeverity.Warning
                        ));
                    }
                }
                else
                {
                    results.Add(CreateResult(
                        "Dose.Prescription",
                        "This plan is not linked to the Prescription",
                        ValidationSeverity.Warning
                    ));
                }

            }
            else
            {
                results.Add(CreateResult(
                    "Dose.ReferencePoint",
                    "No primary reference point found in plan",
                    ValidationSeverity.Error
                ));
            }

            return results;
        }
    }

    // 4 Fixation devices validator
    public class FixationValidator : ValidatorBase
    {
        // Shared fixation structure prefixes for collision assessment (both Halcyon and Edge)
        private static readonly string[] FixationStructurePrefixesForCollision = new[]
        {
            "BODY",
            "z_AltaLD",
            "z_AltaHD",
            "CouchSurface",
            "z_ArmShuttle",
            "z_VacBag"
        };

        public override IEnumerable<ValidationResult> Validate(ScriptContext context)
        {
            var results = new List<ValidationResult>();

            if (context.StructureSet != null)
            {
                // Check for Halcyon-specific structures
                string machineId = context.PlanSetup?.Beams?.FirstOrDefault()?.TreatmentUnit.Id;
                bool isHalcyonMachine = PlanUtilities.IsHalcyonMachine(machineId);

                if (isHalcyonMachine)
                {
                    // Required structures for Halcyon plans
                    var requiredPrefixes = new[] {
                        "z_AltaHD_", "z_AltaLD_",
                        "CouchSurface", "CouchInterior"
                    };

                    foreach (var prefix in requiredPrefixes)
                    {
                        bool structureExists = context.StructureSet.Structures.Any(s =>
                            s.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

                        results.Add(CreateResult(
                            "Fixation.Structures",
                            structureExists
                                ? $"Required Halcyon structure '{prefix}*' exists"
                                : $"Required Halcyon structure '{prefix}*' is missing",
                            structureExists ? ValidationSeverity.Info : ValidationSeverity.Error
                        ));
                    }
                }

                // Check collision for Halcyon machine
                if (isHalcyonMachine && context.PlanSetup?.Beams?.Any() == true)
                {
                    // Get the isocenter position (from the first beam)
                    VVector isocenter = context.PlanSetup.Beams.First().IsocenterPosition;

                    var ringRadius = 475; // 47.5 cm in mm

                    // Track information for each structure
                    var structureDetails = new List<(
                        Structure Structure, double MaxDistance,
                        VVector FurthestPoint, double Clearance)>();

                    // Check each candidate structure
                    foreach (var prefix in FixationStructurePrefixesForCollision)
                    {
                        var matchingStructures = context.StructureSet.Structures
                            .Where(s => s.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        foreach (var structure in matchingStructures)
                        {
                            double maxRadialDistance = 0;
                            VVector furthestPoint = new VVector();

                            // Loop through all image planes containing the structure
                            for (int i = 0; i < context.StructureSet.Image.ZSize; i++)
                            {
                                var contours = structure.GetContoursOnImagePlane(i);
                                if (contours.Any())
                                {
                                    foreach (var contour in contours)
                                    {
                                        foreach (var point in contour)
                                        {
                                            // Calculate the radial distance in the axial plane (X and Y in DICOM)
                                            double radialDistance = Math.Sqrt(
                                                Math.Pow(point.x - isocenter.x, 2) +
                                                Math.Pow(point.y - isocenter.y, 2)
                                            );

                                            // Keep track of furthest point (largest radial distance)
                                            if (radialDistance > maxRadialDistance)
                                            {
                                                maxRadialDistance = radialDistance;
                                                furthestPoint = point;
                                            }
                                        }
                                    }
                                }
                            }

                            // Calculate clearance for this structure
                            if (maxRadialDistance > 0)
                            {
                                double clearance = (ringRadius - maxRadialDistance) / 10.0; // Convert mm to cm
                                structureDetails.Add((structure, maxRadialDistance, furthestPoint, clearance));
                            }
                        }
                    }

                    // Check if we found any structures
                    if (structureDetails.Any())
                    {
                        // Find the structure with the minimum clearance (closest to colliding)
                        var closestStructure = structureDetails
                            .OrderBy(item => item.Clearance)
                            .First();

                        var structure = closestStructure.Structure;
                        var maxRadialDistance = closestStructure.MaxDistance;
                        var furthestPoint = closestStructure.FurthestPoint;
                        var clearance = closestStructure.Clearance;

                        // Determine direction of furthest point
                        string direction = "";
                        if (maxRadialDistance > 0)
                        {
                            // Calculate direction from isocenter to furthest point
                            double angleRad = Math.Atan2(furthestPoint.y - isocenter.y, furthestPoint.x - isocenter.x);
                            double angleDeg = angleRad * 180.0 / Math.PI;
                            direction = angleDeg >= -45 && angleDeg < 45 ? "left" :
                                        angleDeg >= 45 && angleDeg < 135 ? "anterior" :
                                        angleDeg >= 135 || angleDeg < -135 ? "right" :
                                        "posterior";
                        }

                        // Set severity based on clearance
                        ValidationSeverity severity = ValidationSeverity.Info;
                        if (clearance < 4.5)
                            severity = ValidationSeverity.Error;
                        else if (clearance < 5.0)
                            severity = ValidationSeverity.Warning;

                        // Create message
                        string message = $"Clearance {clearance:F1} cm between " +
                            $"fixation device '{structure.Id}' ({direction} edge) and Halcyon ring";
                        if (clearance < 5.0)
                            message += clearance < 4.5 ? " - potential collision risk" : " - limited clearance";

                        results.Add(CreateResult(
                            "Fixation.Clearance",
                            message,
                            severity
                        ));
                    }
                    else
                    {
                        results.Add(CreateResult(
                            "Fixation.Clearance",
                            "Cannot assess Halcyon collision risk - none of the required fixation devices found",
                            ValidationSeverity.Warning
                        ));
                    }
                }

                // Check collision for Edge machine
                if (PlanUtilities.IsEdgeMachine(machineId) && context.PlanSetup?.Beams?.Any() == true)
                {
                    // Check for couch rotation - if ANY field has couch rotation, skip assessment
                    var allBeams = context.PlanSetup.Beams.ToList();
                    if (PlanUtilities.HasAnyFieldWithCouch(allBeams))
                    {
                        results.Add(CreateResult(
                            "Fixation.Clearance",
                            "Collision assessment skipped for plans with couch rotation - manual verification required",
                            ValidationSeverity.Info
                        ));
                    }
                    else
                    {
                        // Get isocenter position from first beam
                        VVector isocenter = context.PlanSetup.Beams.First().IsocenterPosition;
                        var ringRadius = 380; // 38 cm in mm

                        // Get treatment beams only (no setup fields)
                        var treatmentBeams = context.PlanSetup.Beams.Where(b => !b.IsSetupField).ToList();

                        // Determine if we need sector-based filtering or full 360° check
                        bool isFullArc = PlanUtilities.IsFullArcCoverage(treatmentBeams);
                        List<(double start, double end)> coveredSectors = null;

                        if (!isFullArc)
                        {
                            // Partial coverage - build sector list for filtering
                            coveredSectors = PlanUtilities.GetCoveredAngularSectors(treatmentBeams);
                        }

                        // Track information for each structure
                        var structureDetails = new List<(
                            Structure Structure, double MaxDistance,
                            VVector FurthestPoint, double Clearance)>();

                        // Check each candidate structure
                        foreach (var prefix in FixationStructurePrefixesForCollision)
                        {
                            var matchingStructures = context.StructureSet.Structures
                                .Where(s => s.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                .ToList();

                            foreach (var structure in matchingStructures)
                            {
                                double maxRadialDistance = 0;
                                VVector furthestPoint = new VVector();

                                // Loop through all image planes containing the structure
                                for (int i = 0; i < context.StructureSet.Image.ZSize; i++)
                                {
                                    var contours = structure.GetContoursOnImagePlane(i);
                                    if (contours.Any())
                                    {
                                        foreach (var contour in contours)
                                        {
                                            foreach (var point in contour)
                                            {
                                                // Calculate the radial distance in the axial plane (X and Y in DICOM)
                                                double radialDistance = Math.Sqrt(
                                                    Math.Pow(point.x - isocenter.x, 2) +
                                                    Math.Pow(point.y - isocenter.y, 2)
                                                );

                                                // If partial arc, check if this point's angle is in covered sectors
                                                if (!isFullArc)
                                                {
                                                    // Calculate angle of this point from isocenter
                                                    double angleRad = Math.Atan2(point.y - isocenter.y, point.x - isocenter.x);
                                                    double angleDeg = angleRad * 180.0 / Math.PI;

                                                    // Normalize to 0-360
                                                    if (angleDeg < 0)
                                                        angleDeg += 360;

                                                    // Skip this point if it's not in a covered sector
                                                    if (!PlanUtilities.IsAngleInSectors(angleDeg, coveredSectors))
                                                        continue;
                                                }

                                                // Keep track of furthest point (largest radial distance)
                                                if (radialDistance > maxRadialDistance)
                                                {
                                                    maxRadialDistance = radialDistance;
                                                    furthestPoint = point;
                                                }
                                            }
                                        }
                                    }
                                }

                                // Calculate clearance for this structure
                                if (maxRadialDistance > 0)
                                {
                                    double clearance = (ringRadius - maxRadialDistance) / 10.0; // Convert mm to cm
                                    structureDetails.Add((structure, maxRadialDistance, furthestPoint, clearance));
                                }
                            }
                        }

                        // Check if we found any structures
                        if (structureDetails.Any())
                        {
                            // Find the structure with the minimum clearance (closest to colliding)
                            var closestStructure = structureDetails
                                .OrderBy(item => item.Clearance)
                                .First();

                            var structure = closestStructure.Structure;
                            var maxRadialDistance = closestStructure.MaxDistance;
                            var furthestPoint = closestStructure.FurthestPoint;
                            var clearance = closestStructure.Clearance;

                            // Determine direction of furthest point
                            string direction = "";
                            if (maxRadialDistance > 0)
                            {
                                // Calculate direction from isocenter to furthest point
                                double angleRad = Math.Atan2(furthestPoint.y - isocenter.y, furthestPoint.x - isocenter.x);
                                double angleDeg = angleRad * 180.0 / Math.PI;
                                direction = angleDeg >= -45 && angleDeg < 45 ? "left" :
                                            angleDeg >= 45 && angleDeg < 135 ? "anterior" :
                                            angleDeg >= 135 || angleDeg < -135 ? "right" :
                                            "posterior";
                            }

                            // Set severity based on clearance (Edge thresholds: warning <2cm, error <1cm)
                            ValidationSeverity severity = ValidationSeverity.Info;
                            if (clearance < 1.0)
                                severity = ValidationSeverity.Error;
                            else if (clearance < 2.0)
                                severity = ValidationSeverity.Warning;

                            // Create message
                            string message = $"Clearance {clearance:F1} cm between " +
                                $"fixation device '{structure.Id}' ({direction} edge) and Edge ring";
                            if (clearance < 2.0)
                                message += clearance < 1.0 ? " - potential collision risk" : " - limited clearance";

                            results.Add(CreateResult(
                                "Fixation.Clearance",
                                message,
                                severity
                            ));
                        }
                        // Note: No warning if structures not found (per requirement - different from Halcyon)
                    }
                }

                // Check density overrides for all fixation structures
                var fixationPrefixes = new[]
                {
                "z_AltaHD_", "z_AltaLD_", "z_FrameHN_", "z_MaskLock_",
                "z_FrameHead_", "z_LocBar_", "z_ArmShuttle_", "z_EncFrame_",
                "z_VacBag_", "z_Contrast_", "z_ArmHoldR_", "z_ArmHoldR_",
                "z_FlexHigh_", "z_FlexLow_", "z_LocBarMR_", "z_VacIndex_"
            };

                foreach (var structure in context.StructureSet.Structures)
                {
                    // Check if this structure matches any of our prefixes
                    var matchingPrefix = fixationPrefixes.FirstOrDefault(prefix =>
                        structure.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

                    if (matchingPrefix != null)
                    {
                        // Extract the expected density from structure name (+200HU, -390HU, etc.)
                        if (structure.Id.Contains("_") && structure.Id.EndsWith("HU", StringComparison.OrdinalIgnoreCase))
                        {
                            string densityStr = structure.Id.Substring(structure.Id.LastIndexOf('_') + 1);

                            // Remove "HU" and parse the value
                            if (double.TryParse(densityStr.Substring(0, densityStr.Length - 2),
                                               out double expectedDensity))
                            {
                                // Get actual density override using out parameter
                                double actualDensity;
                                bool hasAssignedHU = structure.GetAssignedHU(out actualDensity);

                                if (hasAssignedHU)
                                {
                                    bool isDensityCorrect = Math.Abs(actualDensity - expectedDensity) < 1; // Small tolerance

                                    results.Add(CreateResult(
                                        "Fixation.Density",
                                        isDensityCorrect
                                            ? $"Structure '{structure.Id}' has correct " +
                                            $"density override ({actualDensity} HU)"
                                            : $"Structure '{structure.Id}' has incorrect " +
                                            $"density override: {actualDensity} HU (expected: {expectedDensity} HU)",
                                        isDensityCorrect ? ValidationSeverity.Info : ValidationSeverity.Error
                                    ));
                                }
                                else
                                {
                                    results.Add(CreateResult(
                                        "Fixation.Density",
                                        $"Structure '{structure.Id}' has no " +
                                        $"density override assigned (expected: {expectedDensity} HU)",
                                        ValidationSeverity.Error
                                    ));
                                }
                            }
                        }
                    }
                }
            }

            return results;
        }
    }

    // 5 Planning structures validator
    public class PlanningStructuresValidator : ValidatorBase
    {
        public override IEnumerable<ValidationResult> Validate(ScriptContext context)
        {
            var results = new List<ValidationResult>();

            // Check z_Air_ structure
            var airStructures = context.StructureSet.Structures
                .Where(s => s.Id.StartsWith("z_Air_", StringComparison.OrdinalIgnoreCase));

            foreach (var structure in airStructures)
            {
                // Extract expected density from structure name (e.g., z_Air_-800HU)
                if (structure.Id.Contains("_") && structure.Id.EndsWith("HU", StringComparison.OrdinalIgnoreCase))
                {
                    string densityStr = structure.Id.Substring(structure.Id.LastIndexOf('_') + 1);

                    if (double.TryParse(densityStr.Substring(0, densityStr.Length - 2), out double expectedDensity))
                    {
                        // Check assigned HU
                        double actualDensity;
                        bool hasAssignedHU = structure.GetAssignedHU(out actualDensity);

                        if (hasAssignedHU)
                        {
                            bool isDensityCorrect = Math.Abs(actualDensity - expectedDensity) < 1;

                            results.Add(CreateResult(
                                "PlanningStructures.z_Air Density",
                                isDensityCorrect
                                    ? $"Air structure '{structure.Id}' has correct density override ({actualDensity} HU)"
                                    : $"Air structure '{structure.Id}' has incorrect density override: {actualDensity} HU " +
                                    $"(expected: {expectedDensity} HU)",
                                isDensityCorrect ? ValidationSeverity.Info : ValidationSeverity.Error
                            ));
                        }
                        else
                        {
                            results.Add(CreateResult(
                                "PlanningStructures.z_Air Density",
                                $"Air structure '{structure.Id}' has no density override assigned (expected: {expectedDensity} HU)",
                                ValidationSeverity.Error
                            ));
                        }

                        // Check original density distribution with sampling
                        if (context.StructureSet.Image != null)
                        {
                            int totalVoxels = 0;
                            int voxelsAboveThreshold = 0;

                            int xSize = context.StructureSet.Image.XSize;
                            int ySize = context.StructureSet.Image.YSize;
                            int zSize = context.StructureSet.Image.ZSize;

                            // Sampling parameters - adjust for speed vs accuracy
                            int sampleStep = 2; // Check every 2nd voxel in each dimension
                            int zStep = 2; // Check every 2nd slice

                            // Expected density for distribution
                            var densityThreshold = expectedDensity + 25; // Allow 25 HU tolerance

                            // Preallocate buffer for voxel data
                            int[,] voxelBuffer = new int[xSize, ySize];

                            // Iterate through sampled image planes
                            for (int z = 0; z < zSize; z += zStep)
                            {
                                // Get voxels for this plane
                                context.StructureSet.Image.GetVoxels(z, voxelBuffer);

                                // Check if structure has contours on this plane
                                var contours = structure.GetContoursOnImagePlane(z);
                                if (!contours.Any()) continue;

                                // Get image geometry
                                VVector origin = context.StructureSet.Image.Origin;
                                double xRes = context.StructureSet.Image.XRes;
                                double yRes = context.StructureSet.Image.YRes;
                                double zPos = origin.z + z * context.StructureSet.Image.ZRes;

                                // Check sampled voxels in the plane
                                for (int x = 0; x < xSize; x += sampleStep)
                                {
                                    for (int y = 0; y < ySize; y += sampleStep)
                                    {
                                        // Get voxel position in DICOM coordinates
                                        double xPos = origin.x + x * xRes;
                                        double yPos = origin.y + y * yRes;

                                        // Check if voxel is inside structure
                                        if (structure.IsPointInsideSegment(new VVector(xPos, yPos, zPos)))
                                        {
                                            totalVoxels++;

                                            // Convert voxel value to HU
                                            int voxelValue = voxelBuffer[x, y];
                                            double huValue = context.StructureSet.Image.VoxelToDisplayValue(voxelValue);

                                            if (huValue > densityThreshold)
                                            {
                                                voxelsAboveThreshold++;
                                            }
                                        }
                                    }
                                }
                            }

                            if (totalVoxels > 0)
                            {
                                // Adjust for sampling
                                double samplingFactor = sampleStep * sampleStep * zStep;
                                double percentageAbove = (double)voxelsAboveThreshold / totalVoxels * 100;
                                bool isPercentageValid = percentageAbove <= 5.0;

                                results.Add(CreateResult(
                                    "PlanningStructures.z_Air Density",
                                    isPercentageValid
                                        ? $"Air structure '{structure.Id}': {percentageAbove:F1}% " +
                                          $"of voxels exceed {densityThreshold} HU (within 5% limit)"
                                        : $"Air structure '{structure.Id}': {percentageAbove:F1}% " +
                                          $"of voxels exceed {densityThreshold} HU (exceeds 5% limit)",
                                    isPercentageValid ? ValidationSeverity.Info : ValidationSeverity.Warning
                                ));
                            }
                        }
                    }
                }
            }

            return results;
        }
    }
}