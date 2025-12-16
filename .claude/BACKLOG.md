# BACKLOG — ROcheck

*Task tracking for current sprint*

> **Planning Workflow:**
> - 💡 Rough ideas → [IDEAS.md](./IDEAS.md)
> - 🗺️ Strategic plans → [ROADMAP.md](./ROADMAP.md)
> - 🎯 Current tasks → **BACKLOG.md** (you are here)

---

## Active Sprint

### High Priority
- [ ] Add your high priority tasks here

### Medium Priority
- [ ] Add your medium priority tasks here

### Low Priority
- [ ] Add your low priority tasks here

---

## Bug Fixes

### Critical Bugs
- [ ] Known bug 1
- [ ] Known bug 2

### Minor Bugs
- [ ] ...

---

## Completed

### Recently Completed
- [x] Release v1.6.0 - Production release (2025-12-16)
  - Removed TEST_ prefix from all three locations
  - Updated version to 1.6.0 (minor version for significant milestone)
  - Removed all debug code from ClinicalGoalsCoverageValidator
  - Production ready with full prescription support via TreatmentPhases
  - All code uses documented ESAPI (no reflection)
  - Fully functional prescription filtering for reviewed prescriptions
- [x] Update to v1.5.4 - Access ALL prescriptions via TreatmentPhases (2025-12-16)
  - Fixed prescription access to use Course.TreatmentPhases.Prescriptions
  - Works with both linked and non-linked prescriptions
  - Collects all reviewed prescriptions from all treatment phases
- [x] Update to v1.5.3 - Use documented ESAPI instead of reflection (2025-12-16)
  - Replaced all reflection code with documented ESAPI methods/properties
  - Clinical goals: Use PlanSetup.GetClinicalGoals() method
  - Prescriptions: Use PlanSetup.RTPrescription property (not Course!)
  - Prescription targets: Use RTPrescription.Targets and RTPrescriptionTarget.TargetId
  - Removed GetPrescriptionsFromCourse and GetTargetsFromPrescription helper methods
  - Removed GetClinicalGoalsFromProperty/Method/Course helper methods
  - All API usage now referenced from VMS.TPS.Common.Model.API.xml documentation
- [x] Update to v1.5.2 - Added prescription debugging (2025-12-16)
  - Added comprehensive DEBUG output showing all prescription properties
  - Changed section name to "Clinical Goals existence"
  - Fixed TEST_ prefix in ROcheck.csproj
  - Added Version Increment Policy to CLAUDE.md
  - Eclipse caching policy: increment patch version after each testing feedback
- [x] Enhanced prescription filtering with "Reviewed" status check (2025-12-16)
  - Updated GetReviewedPrescriptionTargetIds to filter by prescription status
  - Added hasReviewedPrescriptions flag to track if reviewed prescriptions exist
  - ClinicalGoalsCoverageValidator shows info message when no reviewed prescriptions found
  - Updated version to 1.5.1 with TEST_ prefix
- [x] Enhanced SIB validator with percentage dose unit checking (2025-12-15)
  - SIBDoseUnitsValidator now detects percentage units in clinical goals
  - Uses HasPercentageDose helper method for comprehensive checking
  - Validates all clinical goals (targets + OARs) when SIB detected
  - Updated documentation with detailed feature descriptions
- [x] Updated to v1.6.0 - Removed TEST_ prefix for production release (2025-12-15)
  - Changed window title to "ROcheck v1.6.0"
  - Changed assembly name to "ROcheck.esapi"
  - Updated version in AssemblyInfo.cs to 1.6.0.0
  - Updated documentation (SNAPSHOT.md)
- [x] Updated to v1.5.0 with TEST_ prefix (2025-12-12)
  - Changed window title to "TEST_ROcheck v1.5.0"
  - Changed assembly name to "TEST_ROcheck.esapi"
  - Updated version in AssemblyInfo.cs to 1.5.0.0
  - Updated documentation
- [x] Refactored validators into separate files organized in Validators/ folder (2025-12-12)
  - Split ClinicalGoalsValidator into 6 focused validators
  - Created Validators/ folder for better code organization
  - Updated namespaces to ROcheck.Validators
  - Moved spatial helpers to ValidationHelpers.cs
  - Updated all documentation (SNAPSHOT, ARCHITECTURE, CLAUDE.md)
- [x] Added PTV to Body surface proximity validator (2025-12-12)
- [x] Updated CLAUDE.md with framework usage policy (2025-12-12)
- [x] Initialized Claude Code Starter Framework (2025-12-10)
- [x] Upgraded to Framework v2.1 (2025-12-10)

---

## Notes

- Strategic features/improvements are now in [ROADMAP.md](./ROADMAP.md)
- Rough ideas go in [IDEAS.md](./IDEAS.md) first
- This file contains ONLY current sprint tasks

**Commands:**
- `/feature` - Plan new features (adds to ROADMAP)
- `/fix` - Address bugs (adds to BACKLOG)
- `/fi` - Sprint/Phase completion review

---
*Framework: Claude Code Starter v2.1*
