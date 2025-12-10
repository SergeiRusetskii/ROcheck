# SNAPSHOT — ROcheck

*Framework: Claude Code Starter v2.1*
*Last updated: 2025-12-10*

> **Planning Documents:**
> - 🎯 Current tasks: [BACKLOG.md](./BACKLOG.md)
> - 🗺️ Strategic roadmap: [ROADMAP.md](./ROADMAP.md)
> - 💡 Ideas: [IDEAS.md](./IDEAS.md)
> - 📊 Architecture: [ARCHITECTURE.md](./ARCHITECTURE.md)

## Current State

**Version:** v1.2.0
**Status:** Production - Target-OAR overlap detection fully working
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
├── Validators.cs                  # Validation engine (composite pattern)
├── ValidationViewModel.cs         # MVVM view model
├── MainControl.xaml/.cs          # WPF UI
├── SeverityToColorConverter.cs   # UI color converter
└── Properties/AssemblyInfo.cs    # Version info
```

## Recent Progress

- [x] v1.2.0: Target-OAR overlap detection fully working
- [x] Fixed clinical goal detection with Unicode operators (≥, ≤, >, <)
- [x] Optimized overlap algorithm (dose filter first, then spatial)
- [x] Updated resolution thresholds (<5cc error, 5-10cc warning)
- [x] Upgraded to Framework v2.1

## Active Work

- Currently in production use
- No active development tasks

## Next Steps

- Monitor for user feedback
- Consider additional validation rules (see ROADMAP.md)

## Key Concepts

**Validation Categories:**
- Structure Coverage: Clinical goal presence
- Target Containment: GTV/CTV within PTVs
- PTV-OAR Overlap: Conflicting dose constraints
- Target Resolution: High-res for small PTVs
- Structure Types: Proper PTV/CTV/GTV labeling

**Prescription-Aware Validation:**
- Only targets IN prescription are validated for clinical goals
- Non-prescription targets automatically excluded

---
*Quick-start context for AI sessions*
