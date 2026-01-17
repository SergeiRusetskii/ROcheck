# SNAPSHOT — ROcheck

*Framework: Claude Code Starter v2.1*
*Last updated: 2025-12-16*

> **Planning Documents:**
> - 🎯 Current tasks: [BACKLOG.md](./BACKLOG.md)
> - 🗺️ Strategic roadmap: [ROADMAP.md](./ROADMAP.md)
> - 💡 Ideas: [IDEAS.md](./IDEAS.md)
> - 📊 Architecture: [ARCHITECTURE.md](./ARCHITECTURE.md)

## Current State

**Version:** v1.6.3
**Status:** Production - Ready for deployment with bug fixes
**Branch:** master

## Project Overview

**Name:** ROcheck
**Description:** Focused quality assurance tool for Varian Eclipse treatment planning system that performs automated validation checks on structure setup and clinical goal configuration for radiation therapy treatment plans. C# ESAPI plugin providing systematic validation to ensure quality and safety in radiation therapy.

**Tech Stack:**
- VMS.TPS.Common.Model.API (Eclipse Scripting API v18.0+)
- .NET Framework 4.8
- WPF/XAML for UI
- MVVM pattern
- Windows x64 platform

## Current Structure

```
ROcheck/
├── Validators/                                      # Validation logic (organized by concern)
│   ├── ValidatorBase.cs                            # Base validator classes (Composite pattern)
│   ├── ClinicalGoalsCoverageValidator.cs           # Clinical goal presence validation
│   ├── TargetContainmentValidator.cs               # GTV/CTV containment within PTV
│   ├── TargetOAROverlapValidator.cs                # Target-OAR dose conflict detection
│   ├── PTVBodyProximityValidator.cs                # PTV to Body surface proximity
│   ├── TargetResolutionValidator.cs                # Small volume high-res validation
│   ├── StructureTypesValidator.cs                  # DICOM type validation
│   └── SIBDoseUnitsValidator.cs                    # SIB dose unit validation
├── Script.cs                                        # ESAPI entry point (creates main window)
├── ValidationResult.cs                              # Validation result classes and enums
├── RootValidator.cs                                 # Root validator (orchestrates all validators)
├── ValidationHelpers.cs                             # Helper methods and spatial algorithms
├── ValidationViewModel.cs                           # MVVM view model
├── MainControl.xaml/.cs                            # WPF UI
├── SeverityToColorConverter.cs                     # UI color converter
└── Properties/AssemblyInfo.cs                      # Version info
```

## Recent Progress

- [x] v1.6.3: MARKER structure exclusion (from Eclipse v18 port)
- [x] Exclude MARKER type structures from all validators (reference points, not structures)
- [x] Updated 6 files: ValidationHelpers, 5 validators (TargetContainment, PTVBodyProximity, StructureTypes, TargetResolution, SIBDoseUnits)
- [x] v1.6.2: Segment model safety checks (from Eclipse v18 port)
- [x] Added HasSegment checks before spatial operations to prevent crashes
- [x] Updated IsStructureContained and StructuresOverlap methods in ValidationHelpers
- [x] v1.6.0: Production release with full prescription support
- [x] Access ALL prescriptions via Course.TreatmentPhases.Prescriptions
- [x] Works with both linked and non-linked prescriptions
- [x] Removed TEST_ prefix - production ready
- [x] Removed all debug code
- [x] Created comprehensive README for public repository
- [x] Implemented Community License (free internal use, prohibited resale)
- [x] Fixed emoji encoding issues in README (UTF-8 BOM)
- [x] v1.5.4: Fixed prescription access to use TreatmentPhases
- [x] v1.5.3: Replaced reflection with documented ESAPI
- [x] Use PlanSetup.GetClinicalGoals() for clinical goals
- [x] Use documented API for prescription access
- [x] Removed all "imagined" property names and reflection code
- [x] v1.5.2: Added comprehensive prescription debugging output
- [x] Changed category name from "Structure Coverage" to "Clinical Goals existence"
- [x] Fixed TEST_ prefix in ROcheck.csproj
- [x] v1.5.1: Enhanced prescription filtering to check "Reviewed" status
- [x] Added informational message when no reviewed prescriptions found
- [x] v1.5.0: Major refactoring - organized validators into separate files
- [x] Split ClinicalGoalsValidator into 6 focused validators
- [x] Created Validators/ folder with 7 total validators
- [x] Added TEST_ prefix to script name
- [x] v1.4.0: Production release with code refactoring
- [x] Fixed ExtractTargetDose to use IsLowerGoal logic (consistent with overlap detection)
- [x] Fixed GetTargetSuffix to handle structures without underscores
- [x] Updated excluded structures: added Sternum, Implant*, Lymph*, LN_*
- [x] Added IsValid property to ValidationResult
- [x] Removed Test_ prefix, ready for production
- [x] v1.3.0: SIB dose unit validation
- [x] v1.2.0: Target-OAR overlap detection

## Active Work

- ✅ v1.6.3 completed - Critical bug fixes from Eclipse v18 port
- ✅ Both changes validated and tested in Eclipse v16.1 environment
- ✅ Ready to push to GitHub

## Next Steps

- Deploy to Eclipse plugins directory
- Monitor production usage
- Gather user feedback for future enhancements
- Consider making repository public

## Key Concepts

**Validation Categories:**
- Structure Coverage: Clinical goal presence
- Target Containment: GTV/CTV within PTVs
- PTV-OAR Overlap: Conflicting dose constraints
- Target Resolution: High-res for small PTVs
- Structure Types: Proper PTV/CTV/GTV labeling
- SIB Dose Units: Detects SIB plans (>6% dose difference) and validates Gy-only units (no percentages)
- PTV-Body Proximity: Distance from PTV to Body surface (4mm threshold)

**Prescription-Aware Validation:**
- Only targets in "Reviewed" prescriptions are validated for clinical goals
- Non-prescription targets automatically excluded
- Informational message shown when no reviewed prescriptions exist

---
*Quick-start context for AI sessions*
