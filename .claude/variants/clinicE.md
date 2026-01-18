# Clinic E Variant Status

**Version:** v1.6.4
**Eclipse:** 18.0
**Clinic:** Clinic E (Primary development variant)
**Status:** Production - Configuration-driven architecture
**Last Updated:** 2026-01-18

## Configuration

**Body Structure:** "BODY"
**PTV Proximity Threshold:** 4.0mm
**High-Res Thresholds:**
- Critical (Error): <5cc
- Warning: <10cc

**SIB Detection Threshold:** 6.0% dose difference

**Excluded Structures:**
- Bones, CouchInterior, CouchSurface, Clips, Scar_Wire, Sternum

**Excluded Patterns:**
- z_*, *wire*, *Encompass*, *Enc Marker*, *Dose*
- Implant*, Lymph*, LN_*

## Architecture Changes

**Multi-variant restructure (2026-01-18):**
- Created IValidationConfig interface for clinic-specific parameters
- Implemented ClinicEConfig with all thresholds and exclusions
- Refactored validators to use config injection:
  - PTVBodyProximityValidator (threshold + body structure ID)
  - ClinicalGoalsCoverageValidator (excluded structures)
  - TargetResolutionValidator (high-res thresholds)
  - SIBDoseUnitsValidator (SIB dose percent threshold)
- Updated RootValidator, ValidationViewModel, Script.cs for config injection
- Moved shared code to Core/ (ValidatorBase, Models, UI, Helpers)

## Recent Changes

### v1.6.4 (2026-01-18)
- Multi-variant architecture implementation
- Configuration-driven validation (IValidationConfig)
- Moved shared infrastructure to Core/
- Version increment for testing in Eclipse

### v1.6.3 (2026-01-17)
- Added MARKER structure exclusion
- Prevents false positives from reference point markers
- Updated 6 files: ValidationHelpers, 5 validators

### v1.6.2 (2026-01-17)
- Added HasSegment safety checks
- Prevents crashes on empty/invalid structures
- Updated spatial operation methods in ValidationHelpers

### v1.6.0 (2025-12-16)
- Production release
- Full prescription support via Course.TreatmentPhases
- Removed TEST_ prefix
- Community License implementation

## Testing Notes

**Last Tested:** 2026-01-17
**Test Plans:** 15 clinical plans
**Issues:** None reported

**Validators Status:**
- ✅ ClinicalGoalsCoverageValidator - Config-driven exclusions
- ✅ TargetContainmentValidator - No config needed
- ✅ TargetOAROverlapValidator - No config needed
- ✅ PTVBodyProximityValidator - Config-driven threshold + body ID
- ✅ TargetResolutionValidator - Config-driven thresholds
- ✅ StructureTypesValidator - No config needed
- ✅ SIBDoseUnitsValidator - Config-driven SIB threshold

## Active Development

**Current Sprint:** Multi-variant architecture implementation
**Focus:** ClinicE is primary development variant
**Workflow:** All new features developed here first

## Deployment

**Build:** Variants/ClinicE/ROcheck.esapi.csproj
**Output:** ROcheck.esapi.dll (Eclipse 18 compatible)
**Target:** Eclipse 18.0 plugins directory

---
*Tracking file for Clinic E variant*
