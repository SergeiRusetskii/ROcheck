# SNAPSHOT — ROcheck

*Framework: Claude Code Starter v2.1*
*Last updated: 2026-01-18*

> **Planning Documents:**
> - 🎯 Current tasks: [BACKLOG.md](./BACKLOG.md)
> - 🗺️ Strategic roadmap: [ROADMAP.md](./ROADMAP.md)
> - 💡 Ideas: [IDEAS.md](./IDEAS.md)
> - 📊 Architecture: [ARCHITECTURE.md](./ARCHITECTURE.md)
> - 📋 Variant tracking: [variants/](./variants/)

## Current State

**Architecture:** Multi-variant monorepo (ClinicE primary, ClinicH manual port)
**Primary Variant:** ClinicE v1.6.4 (Eclipse 18.0)
**Secondary Variant:** ClinicH v1.6.4 (Eclipse 16.1)
**Status:** Production - Multi-variant architecture implemented
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
ROcheck/  (Multi-variant monorepo)
├── Core/                                           # Shared infrastructure
│   ├── Base/
│   │   ├── ValidatorBase.cs                       # Base validator classes (Composite pattern)
│   │   └── IValidationConfig.cs                   # Configuration interface
│   ├── Models/
│   │   └── ValidationResult.cs                    # Validation result classes and enums
│   ├── UI/
│   │   ├── MainControl.xaml/.cs                   # WPF UI
│   │   └── SeverityToColorConverter.cs            # UI color converter
│   └── Helpers/
│       └── ValidationHelpers.cs                   # Helper methods and spatial algorithms
│
├── Variants/
│   ├── ClinicE/                                   # Primary variant (Eclipse 18.0)
│   │   ├── Config/
│   │   │   └── ClinicEConfig.cs                  # Clinic E configuration
│   │   ├── Validators/                           # Clinic E validators
│   │   │   ├── ClinicalGoalsCoverageValidator.cs
│   │   │   ├── TargetContainmentValidator.cs
│   │   │   ├── TargetOAROverlapValidator.cs
│   │   │   ├── PTVBodyProximityValidator.cs
│   │   │   ├── TargetResolutionValidator.cs
│   │   │   ├── StructureTypesValidator.cs
│   │   │   └── SIBDoseUnitsValidator.cs
│   │   ├── Script.cs                             # ESAPI entry point
│   │   ├── RootValidator.cs                      # Validator orchestrator
│   │   ├── ValidationViewModel.cs                # MVVM view model
│   │   ├── ROcheck.esapi.csproj                 # Eclipse 18 project
│   │   └── Properties/AssemblyInfo.cs
│   │
│   └── ClinicH/                                   # Secondary variant (Eclipse 16.1)
│       ├── Config/                               # (Config folder - pending refactor)
│       ├── Validators/                           # Clinic H validators (manual port)
│       ├── Script.cs
│       ├── RootValidator.cs
│       ├── ValidationViewModel.cs
│       ├── ROcheck.esapi.csproj                 # Eclipse 16 project
│       └── Properties/AssemblyInfo.cs
│
├── Documentation/
│   ├── ClinicE/                                   # Eclipse 18 API docs
│   │   ├── VMS.TPS.Common.Model.API.xml
│   │   └── VMS.TPS.Common.Model.Types.xml
│   └── ClinicH/                                   # Eclipse 16 API docs
│       ├── VMS.TPS.Common.Model.API.xml
│       └── VMS.TPS.Common.Model.Types.xml
│
└── .claude/
    ├── SNAPSHOT.md                                # Tracks ClinicE (primary)
    ├── ARCHITECTURE.md                            # Multi-variant design
    └── variants/
        ├── clinicE.md                            # ClinicE deployment tracking
        └── clinicH.md                            # ClinicH porting log
```

## Recent Progress

- [x] **Multi-variant architecture implementation (2026-01-18)**
  - [x] Created Core/ for shared infrastructure (ValidatorBase, Models, UI, Helpers)
  - [x] Organized ClinicE (Eclipse 18) and ClinicH (Eclipse 16) as separate variants
  - [x] Implemented IValidationConfig interface for clinic-specific parameters
  - [x] Created ClinicEConfig with thresholds and exclusions
  - [x] Refactored 4 ClinicE validators to use config injection
  - [x] Updated .csproj files to reference shared Core/ files
  - [x] Organized Documentation/ by Eclipse version
  - [x] Created .claude/variants/ tracking system
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

- ✅ Multi-variant architecture implemented
- ✅ ClinicE (primary) uses configuration-driven validation
- ✅ ClinicH (secondary) preserved for manual porting when needed
- ⏳ Build verification (user responsibility)
- ⏳ Testing in Eclipse environments (user responsibility)

## Next Steps

**Development Workflow:**
- All new features developed in ClinicE first
- ClinicH updated only when explicitly requested
- Core/ infrastructure changes benefit both variants automatically
- Track porting in .claude/variants/clinicH.md

**Deployment:**
- Build and test both variants
- Deploy to respective Eclipse environments
- Monitor production usage
- Gather feedback for config refinements

## Key Concepts

**Multi-Variant Architecture:**
- Core/ contains shared infrastructure (ValidatorBase, Models, UI, Helpers)
- Variants/ contains clinic-specific implementations
- IValidationConfig interface defines clinic-specific parameters
- ClinicE-primary workflow: all development happens here first
- ClinicH manual port: updated only when explicitly requested

**Configuration System (ClinicE):**
- Body structure ID: "BODY"
- PTV proximity threshold: 4.0mm
- High-res thresholds: 5cc (error), 10cc (warning)
- SIB detection: 6.0% dose difference
- Excluded structures: Bones, CouchInterior, CouchSurface, Clips, Scar_Wire, Sternum
- Excluded patterns: z_*, *wire*, *Encompass*, *Enc Marker*, *Dose*, Implant*, Lymph*, LN_*

**Validation Categories:**
- Structure Coverage: Clinical goal presence
- Target Containment: GTV/CTV within PTVs
- PTV-OAR Overlap: Conflicting dose constraints
- Target Resolution: High-res for small PTVs
- Structure Types: Proper PTV/CTV/GTV labeling
- SIB Dose Units: Detects SIB plans and validates Gy-only units (no percentages)
- PTV-Body Proximity: Distance from PTV to Body surface

**Prescription-Aware Validation:**
- Only targets in "Reviewed" prescriptions are validated for clinical goals
- Non-prescription targets automatically excluded
- Informational message shown when no reviewed prescriptions exist

---
*Quick-start context for AI sessions*
