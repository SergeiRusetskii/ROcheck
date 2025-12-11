# SNAPSHOT — ROcheck

*Framework: Claude Code Starter v2.1*
*Last updated: 2025-12-11*

> **Planning Documents:**
> - 🎯 Current tasks: [BACKLOG.md](./BACKLOG.md)
> - 🗺️ Strategic roadmap: [ROADMAP.md](./ROADMAP.md)
> - 💡 Ideas: [IDEAS.md](./IDEAS.md)
> - 📊 Architecture: [ARCHITECTURE.md](./ARCHITECTURE.md)

## Current State

**Version:** v1.4.0
**Status:** Production - Refactored and ready for deployment
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
├── Script.cs                      # ESAPI entry point (creates main window)
├── ValidationResult.cs            # Validation result classes and enums
├── ValidatorBase.cs               # Base validator classes (Composite pattern)
├── RootValidator.cs               # Root validator (entry point)
├── ClinicalGoalsValidator.cs      # Clinical goals validation logic
├── ValidationHelpers.cs           # Helper methods for validation
├── ValidationViewModel.cs         # MVVM view model
├── MainControl.xaml/.cs          # WPF UI
├── SeverityToColorConverter.cs   # UI color converter
└── Properties/AssemblyInfo.cs    # Version info
```

## Recent Progress

- [x] v1.4.0: Production release with code refactoring
- [x] Refactored Validators.cs into 6 separate files for better maintainability
- [x] Fixed ExtractTargetDose to use IsLowerGoal logic (consistent with overlap detection)
- [x] Fixed GetTargetSuffix to handle structures without underscores
- [x] Updated excluded structures: added Sternum, Implant*, Lymph*, LN_*
- [x] Added IsValid property to ValidationResult
- [x] Removed Test_ prefix, ready for production
- [x] v1.3.0: SIB dose unit validation
- [x] v1.2.0: Target-OAR overlap detection

## Active Work

- Ready for production deployment
- All refactoring complete and tested

## Next Steps

- Deploy to Eclipse plugins directory
- Monitor production usage
- Gather user feedback

## Key Concepts

**Validation Categories:**
- Structure Coverage: Clinical goal presence
- Target Containment: GTV/CTV within PTVs
- PTV-OAR Overlap: Conflicting dose constraints
- Target Resolution: High-res for small PTVs
- Structure Types: Proper PTV/CTV/GTV labeling
- SIB Dose Units: Gy units required in SIB plans (NEW in v1.3.0)

**Prescription-Aware Validation:**
- Only targets IN prescription are validated for clinical goals
- Non-prescription targets automatically excluded

---
*Quick-start context for AI sessions*
