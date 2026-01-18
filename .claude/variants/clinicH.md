# Clinic H Variant Status

**Version:** v1.6.4
**Eclipse:** 16.1
**Clinic:** Clinic H (Secondary variant - manual port only)
**Status:** Stable - Updated on request only
**Last Updated:** 2026-01-18

## Configuration

**Note:** ClinicH validators still use hardcoded values (not yet refactored to use config).
Future porting will implement ClinicHConfig when features are manually ported.

**Expected Configuration (when refactored):**
- Body Structure: "External" (different from ClinicE!)
- PTV Proximity Threshold: 5.0mm (more conservative)
- High-Res Thresholds: 4cc (error), 8cc (warning)
- SIB Detection Threshold: 5.0% (different clinic definition)

## Porting Log

### v1.6.4 (2026-01-18) - Multi-variant restructure
**Source:** ClinicE v1.6.4
**Status:** ✅ Structural changes complete, config refactor pending

**Changes:**
- Moved shared infrastructure to Core/ (ValidatorBase, Models, UI, Helpers)
- Updated ROcheck16.esapi.csproj to reference Core/ files
- Removed duplicate copies of shared files
- Organized Eclipse 16.1 API documentation in Documentation/ClinicH/
- Created .claude-archive/ to preserve old framework files

**Validators Status:**
- ⏸️ ClinicalGoalsCoverageValidator - Uses hardcoded exclusions (port pending)
- ✅ TargetContainmentValidator - No config needed
- ✅ TargetOAROverlapValidator - No config needed
- ⏸️ PTVBodyProximityValidator - Uses hardcoded values (port pending)
- ⏸️ TargetResolutionValidator - Uses hardcoded thresholds (port pending)
- ✅ StructureTypesValidator - No config needed
- ⏸️ SIBDoseUnitsValidator - Uses hardcoded threshold (port pending)

**Next Port (when requested):**
- Create ClinicHConfig with clinic-specific thresholds
- Refactor validators to use config injection
- Update RootValidator, ValidationViewModel, Script.cs
- Test with Eclipse 16.1 environment

### v1.6.3 (2026-01-17) - Ported from ClinicE
**Source:** ClinicE v1.6.3
**Changes:**
- ✅ MARKER structure exclusion fix
- ✅ Updated ValidationHelpers and 5 validators
- ✅ Tested with 8 clinical plans
- ✅ No issues reported

### v1.6.2 (2026-01-17) - Ported from ClinicE
**Source:** ClinicE v1.6.2 & v1.6.3 combined
**Changes:**
- ✅ HasSegment safety checks
- ✅ Updated IsStructureContained and StructuresOverlap methods
- ✅ Prevents crashes on empty/invalid structures

### v1.6.0 (2025-12-16) - Initial sync
**Source:** ClinicE v1.6.0
**Status:** Base version synchronized

## Pending Ports

**None currently** - ClinicH is structurally up to date.
Config refactor will be ported when explicitly requested.

## Testing Notes

**Last Tested:** 2026-01-17
**Test Plans:** 8 clinical plans
**Eclipse Version:** 16.1
**Issues:** None reported

**Compatibility:**
- Uses Eclipse 16.1 ESAPI (Version=1.0.500.25)
- All validators working correctly with hardcoded values
- Ready for config refactor when requested

## Deployment

**Build:** Variants/ClinicH/ROcheck.esapi.csproj
**Output:** ROcheck.esapi.dll (Eclipse 16 compatible)
**Target:** Eclipse 16.1 plugins directory

---
*Tracking file for Clinic H variant - updates on request only*
