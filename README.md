# ROcheck

> **Automated Quality Assurance for Varian Eclipse Treatment Planning**

ROcheck is a comprehensive validation tool for Varian Eclipse treatment planning systems that performs automated quality checks on radiation therapy treatment plans. It validates structure setup, clinical goal configuration, and prescription consistency to ensure safety and quality in radiation therapy delivery.

[![Version](https://img.shields.io/badge/version-1.6.0-blue.svg)](https://github.com/SergeiRusetskii/ROcheck/releases)
[![License](https://img.shields.io/badge/license-Community-blue.svg)](LICENSE)
[![Eclipse API](https://img.shields.io/badge/Eclipse%20API-18.0+-green.svg)](https://varianapis.github.io/)
[![.NET](https://img.shields.io/badge/.NET%20Framework-4.8-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-lightgrey.svg)](https://www.microsoft.com/windows)

## рџ“‹ Table of Contents

- [Overview](#overview)
- [What ROcheck Validates](#what-rocheck-validates)
- [Key Features](#key-features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Usage](#usage)
- [Validation Categories](#validation-categories)
- [Technical Details](#technical-details)
- [Building from Source](#building-from-source)
- [Contributing](#contributing)
- [Version History](#version-history)
- [License](#license)

## Overview

ROcheck is an Eclipse Scripting API (ESAPI) plugin that automatically validates radiation therapy treatment plans against clinical best practices and institutional standards. It performs seven distinct categories of validation checks, providing immediate feedback on potential issues before plan approval.

**Primary Goals:**
- **Quality Assurance**: Catch planning errors before treatment delivery
- **Safety**: Identify potential dose delivery conflicts
- **Consistency**: Ensure clinical goals align with prescriptions
- **Efficiency**: Automated checks reduce manual review time

## What ROcheck Validates

ROcheck performs comprehensive validation across multiple aspects of treatment planning:

### рџЋЇ Clinical Goal Coverage
- Verifies all treatment structures have associated clinical goals
- Uses prescription-aware filtering (only validates targets in "Reviewed" prescriptions)
- Excludes support structures (couch, clips, wires, etc.)
- Provides clear feedback on missing goals

### рџ“¦ Target Containment
- Validates GTV/CTV volumes are fully contained within their corresponding PTVs
- Uses voxel-based spatial analysis for accurate containment detection
- Matches targets by naming convention (e.g., PTV_70 в†’ CTV_70 в†’ GTV_70)
- Flags planning errors where targets extend beyond PTV boundaries

### вљ пёЏ PTV-OAR Overlap Detection
- Identifies spatial overlaps between target volumes and organs at risk
- Detects dose constraint conflicts (PTV minimum dose vs OAR maximum dose)
- Prevents planning inconsistencies where targets and OARs have conflicting goals
- Uses lower-bound target goals (в‰Ґ) vs Dmax OAR constraints

### рџ“Џ Target Resolution
- Enforces high-resolution contouring for small target volumes
- **Error**: PTVs < 5cc must use high-resolution structures
- **Warning**: PTVs 5-10cc should use high-resolution structures
- Includes related GTV/CTV volumes in resolution validation
- Displays smallest PTV volume for reference

### рџЏ·пёЏ Structure Type Validation
- Verifies DICOM structure types match naming conventions
- Ensures PTVs are labeled as "PTV" type
- Ensures CTVs are labeled as "CTV" type
- Ensures GTVs are labeled as "GTV" type
- Helps maintain consistent structure organization

### рџ’‰ SIB Dose Unit Validation
- Automatically detects SIB (Simultaneously Integrated Boost) plans
- SIB detection: Target dose difference > 6% of higher dose
- **Requirement**: All clinical goals in SIB plans must use absolute dose (Gy), not percentages
- Validates both target and OAR goals for consistent dose specification
- Silent pass: Only shows errors when percentage units are detected

### рџ“Ќ PTV-Body Proximity
- Measures minimum distance from PTV surfaces to Body (skin) surface
- **Warning**: PTVs в‰¤ 4mm from skin should consider EVAL structures for optimization
- Uses 3D contour-based distance calculation across all CT slices
- Shows closest PTV distance when all PTVs are acceptable
- Helps optimize superficial target treatment

## Key Features

вњ… **Prescription-Aware Validation**
- Accesses ALL prescriptions in course via TreatmentPhases
- Filters by "Reviewed" status to validate only approved prescriptions
- Works with both plan-linked and non-linked prescriptions

вњ… **Zero Reflection - 100% Documented ESAPI**
- All code uses officially documented Eclipse Scripting API methods
- Clinical goals: `PlanSetup.GetClinicalGoals()`
- Prescriptions: `Course.TreatmentPhases в†’ TreatmentPhase.Prescriptions`
- No "guessed" property names or reflection-based hacks

вњ… **Intelligent Structure Filtering**
- Automatically excludes support structures (Bones, Couch, Clips, Wires)
- Prescription-aware: Only validates targets in reviewed prescriptions
- Wildcard exclusions: Implant*, Lymph*, LN_*

вњ… **Severity-Based Results**
- рџ”ґ **Error**: Red indicator - Critical issues requiring immediate attention
- в¬њ **Warning**: White rectangle - Items requiring review or consideration
- в¬њ **Info**: White rectangle - Confirmations and informational messages

вњ… **Grouped Validation Categories**
- Results organized by validation type for easy review
- Informational summaries when all checks pass
- Clear, actionable messages for each finding

## Requirements

### System Requirements
- **Eclipse TPS**: Version 18.0 or later
- **.NET Framework**: 4.8
- **Platform**: Windows x64
- **API Access**: Eclipse Scripting API (ESAPI) license

### ESAPI Components Required
- `VMS.TPS.Common.Model.API.dll`
- `VMS.TPS.Common.Model.Types.dll`

## Installation

### Method 1: Pre-built Release

1. Download the latest `ROcheck.esapi.dll` from [Releases](https://github.com/SergeiRusetskii/ROcheck/releases)

2. Copy the DLL to your Eclipse plugins directory:
   ```
   C:\Program Files (x86)\Varian\RTM\[Version]\esapi\plugins\
   ```

3. Restart Eclipse

4. The script will appear in the Scripts menu as **"ROcheck v1.6.0"**

### Method 2: Build from Source

See [Building from Source](#building-from-source) section below.

## Usage

### Running the Validation

1. **Open a treatment plan** in Eclipse
2. **Navigate to**: Scripts в†’ ROcheck v1.6.0
3. **Review results** in the validation window

The tool will automatically:
- Extract clinical goals from the plan
- Access all prescriptions in the course
- Validate structure setup and configuration
- Display organized, color-coded results

### Interpreting Results

**Severity Levels:**

- рџ”ґ **Error** (Red indicator): Must be addressed before plan approval
  - Example: "PTV_60 is 3.2 mm from Body surface. Consider creating EVAL structure"

- в¬њ **Warning** (White rectangle): Requires review and clinical judgment
  - Example: "Structure 'Liver' has no associated clinical goal"

- в¬њ **Info** (White rectangle): Confirmation that checks passed
  - Example: "All 12 applicable structures have associated clinical goals"

**No Results = All Checks Passed**
- If a validation category shows only info messages, all checks passed
- Categories with issues will display specific error/warning messages

## Validation Categories

### 1. Clinical Goals Existence

**What it checks:**
- Every applicable structure has at least one clinical goal
- Prescription targets match reviewed prescriptions

**Exclusions:**
- Support structures: Bones, CouchInterior, CouchSurface, Clips, Scar_Wire, Sternum
- Targets not in reviewed prescriptions

**Example Results:**
- вњ… Info: "All 15 applicable structures have associated clinical goals"
- вљ пёЏ Warning: "Structure 'Rectum' has no associated clinical goal"
- в„№пёЏ Info: "No 'Reviewed' prescriptions found; all target structures were skipped"

### 2. Target Containment

**What it checks:**
- GTV volumes fully contained within corresponding CTVs
- CTV volumes fully contained within corresponding PTVs
- Uses voxel-level spatial analysis

**Naming Convention:**
- Matches by suffix: PTV_70 в†’ CTV_70 в†’ GTV_70
- Also handles: PTV1 в†’ CTV1 в†’ GTV1

**Example Results:**
- вњ… Info: "All target volumes properly contained within PTVs"
- рџ”ґ Error: "CTV_60 extends outside PTV_60"

### 3. PTV-OAR Overlap

**What it checks:**
- Spatial overlap between PTVs and OARs
- Dose constraint conflicts (PTV min dose vs OAR max dose)

**Conflict Detection:**
- PTV lower goal (в‰Ґ or >) vs OAR Dmax constraint
- Only reports overlaps with conflicting dose constraints

**Example Results:**
- вњ… Info: "No target with OAR Dmax conflicts detected"
- вљ пёЏ Warning: "PTV_70 (в‰Ґ70Gy) overlaps SpinalCord (Dmax<45Gy) - spatial overlap detected"

### 4. Target Resolution

**What it checks:**
- Small PTVs use high-resolution contouring
- Related GTV/CTV volumes also validated

**Thresholds:**
- < 5cc: ERROR - must use high-resolution
- 5-10cc: WARNING - should use high-resolution
- > 10cc: No validation

**Example Results:**
- вњ… Info: "All small targets use high-resolution structures"
- рџ”ґ Error: "PTV_boost (2.3cc) must use high-resolution contouring"

### 5. Structure Types

**What it checks:**
- DICOM structure type matches naming convention
- PTV structures в†’ type "PTV"
- CTV structures в†’ type "CTV"
- GTV structures в†’ type "GTV"

**Example Results:**
- вњ… Info: "All 6 target structures have correct DICOM types"
- вљ пёЏ Warning: "PTV_70 has type 'NONE' but should be 'PTV'"

### 6. SIB Dose Units

**What it checks:**
- Detects SIB plans (target dose difference > 6%)
- In SIB plans: ALL goals must use Gy (no percentages)

**SIB Detection Logic:**
```
For each pair of targets with clinical goals:
  dose_difference = |dose1 - dose2| / max(dose1, dose2) * 100%
  if dose_difference > 6% в†’ Plan is SIB
```

**Example Results:**
- вњ… Silent pass when not SIB or all goals in Gy
- рџ”ґ Error: "SIB plan detected: clinical goal for 'PTV_54' uses percentage dose units. SIB plans require Gy units for all clinical goals"

### 7. PTV-Body Proximity

**What it checks:**
- Minimum distance from PTV to Body surface
- 4mm threshold for optimization considerations

**Distance Calculation:**
- 3D contour-based measurement
- Evaluates all CT slices
- Finds global minimum distance

**Example Results:**
- вњ… Info: "Closest PTV_70 is 12.3 mm from Body surface"
- вљ пёЏ Warning: "PTV_breast is 3.1 mm from Body surface. Consider creating EVAL structure"

## Technical Details

### Architecture

**Design Pattern**: Composite Pattern + MVVM

```
ROcheck/
в”њв”Ђв”Ђ Validators/              # Validation logic
в”‚   в”њв”Ђв”Ђ ValidatorBase.cs    # Abstract base class
в”‚   в”њв”Ђв”Ђ ClinicalGoalsCoverageValidator.cs
в”‚   в”њв”Ђв”Ђ TargetContainmentValidator.cs
в”‚   в”њв”Ђв”Ђ TargetOAROverlapValidator.cs
в”‚   в”њв”Ђв”Ђ PTVBodyProximityValidator.cs
в”‚   в”њв”Ђв”Ђ TargetResolutionValidator.cs
в”‚   в”њв”Ђв”Ђ StructureTypesValidator.cs
в”‚   в””в”Ђв”Ђ SIBDoseUnitsValidator.cs
в”њв”Ђв”Ђ RootValidator.cs         # Orchestrates all validators
в”њв”Ђв”Ђ ValidationHelpers.cs     # Spatial algorithms, utilities
в”њв”Ђв”Ђ ValidationViewModel.cs   # MVVM view model
в””в”Ђв”Ђ MainControl.xaml        # WPF UI
```

### Key Algorithms

**Spatial Containment** (TargetContainmentValidator)
- Voxel-based analysis using ESAPI dose grid
- Checks if all CTV/GTV voxels fall within PTV volume
- Handles multiple structures per plan

**Proximity Calculation** (PTVBodyProximityValidator)
- 3D Euclidean distance between contour points
- Iterates all CT slices with contours
- Finds global minimum distance

**Dose Extraction** (ValidationHelpers)
- Parses clinical goal ObjectiveAsString
- Handles percentage and absolute dose units
- Normalizes doses to Gy for comparison

### API Usage (Documented ESAPI)

**Clinical Goals:**
```csharp
var goals = plan.GetClinicalGoals();  // PlanningItem.GetClinicalGoals()
```

**Prescriptions:**
```csharp
// Access ALL prescriptions via TreatmentPhases
var phases = plan.Course.TreatmentPhases;
foreach (var phase in phases) {
    var prescriptions = phase.Prescriptions;  // TreatmentPhase.Prescriptions
    foreach (var rx in prescriptions) {
        var status = rx.Status;      // RTPrescription.Status
        var targets = rx.Targets;    // RTPrescription.Targets
    }
}
```

**Structures:**
```csharp
var structures = context.StructureSet.Structures;  // Documented API
var ptv = structures.FirstOrDefault(s => s.Id.StartsWith("PTV"));
```

### Performance

- **Typical validation time**: 20-60 seconds (depends on CT slice count and structure volumes)
- **Memory footprint**: < 50 MB
- **UI responsiveness**: Results display immediately after validation completes
- **No external dependencies**: Pure ESAPI implementation

## Building from Source

### Prerequisites

- Visual Studio 2019 or later
- .NET Framework 4.8 SDK
- Eclipse Scripting API DLLs (from Eclipse installation)

### Build Steps

1. **Clone the repository:**
   ```bash
   git clone https://github.com/SergeiRusetskii/ROcheck.git
   cd ROcheck
   ```

2. **Update ESAPI references:**
   - Open `ROcheck.csproj`
   - Update paths to your Eclipse ESAPI DLLs:
     ```xml
     <HintPath>C:\Program Files (x86)\Varian\RTM\18.0\esapi\API\VMS.TPS.Common.Model.API.dll</HintPath>
     ```

3. **Build the project:**
   ```bash
   # Release build (recommended)
   msbuild ROcheck.sln /p:Configuration=Release /p:Platform=x64

   # Debug build (for development)
   msbuild ROcheck.sln /p:Configuration=Debug /p:Platform=x64
   ```

4. **Locate the output:**
   ```
   Release/ROcheck.esapi.dll
   ```

5. **Deploy to Eclipse:**
   - Copy `ROcheck.esapi.dll` to Eclipse plugins directory
   - Restart Eclipse

### Development

**Adding New Validators:**

1. Create new class inheriting from `ValidatorBase`
2. Implement `Validate(ScriptContext context)` method
3. Return `IEnumerable<ValidationResult>` with findings
4. Register in `RootValidator.cs`

**Example:**
```csharp
public class MyValidator : ValidatorBase
{
    public override IEnumerable<ValidationResult> Validate(ScriptContext context)
    {
        var results = new List<ValidationResult>();

        // Your validation logic here
        if (somethingWrong)
        {
            results.Add(CreateResult(
                "My Category",
                "Description of the issue",
                ValidationSeverity.Warning
            ));
        }

        return results;
    }
}
```

## Contributing

Contributions are welcome! This project follows standard radiation oncology medical physics practices.

**Guidelines:**
- All validation logic must use documented ESAPI methods
- Include clinical rationale for validation checks
- Add unit tests for complex algorithms
- Update documentation for new features
- Follow existing code style and patterns

**Areas for Contribution:**
- Additional validation categories
- Improved spatial algorithms
- Support for additional planning techniques
- Enhanced reporting features
- Performance optimizations

## Version History

### v1.6.0 (2025-12-16) - Current Release
- вњ… Production release with full prescription support
- вњ… Access ALL prescriptions via Course.TreatmentPhases.Prescriptions
- вњ… Works with both linked and non-linked prescriptions
- вњ… 100% documented ESAPI (zero reflection)
- вњ… Removed TEST_ prefix - production ready

### v1.5.x (2025-12-16) - Development Series
- Enhanced prescription filtering with "Reviewed" status
- Replaced reflection code with documented ESAPI
- Fixed prescription access via TreatmentPhases
- Added comprehensive debugging and testing

### v1.4.0 (2025-12-12)
- Major refactoring: organized validators into separate files
- Split monolithic validator into 7 focused validators
- Created Validators/ folder structure
- Updated documentation and architecture

### v1.3.0
- SIB dose unit validation
- Automatic SIB detection (>6% dose difference)
- Percentage dose unit checking

### v1.2.0
- Target-OAR overlap detection
- Spatial overlap with dose conflict validation

### v1.1.x
- Enhanced structure exclusion logic
- Prescription-aware target validation
- Clinical goals detection improvements

### v1.0.0
- Initial release
- Basic structure and clinical goal validation

## License

**ROcheck Community License**

Copyright (c) 2025 Sergei Rusetskii

### What's Allowed
- Internal use (including commercial clinics), with modifications for internal needs
- Free redistribution with attribution and a link to this repository (no fees)
- Free, non-profit SaaS/hosted use with attribution

### Not Allowed
- Embedding or bundling ROcheck (original or modified) into paid products or services
- Providing the software (original or modified) as paid SaaS/hosted offerings
- Including ROcheck in paid consulting deliverables as a bundled component

**For commercial licensing**, please contact:
- **Sergei Rusetskii** - [GitHub Profile](https://github.com/SergeiRusetskii)
- Open an issue or discussion on this repository

### Full License

See [LICENSE](LICENSE) file for complete terms and conditions.

---

**Medical Disclaimer**: This tool is provided for quality assurance purposes in radiation therapy planning. Clinical decisions should always be made by qualified medical physics and radiation oncology professionals. This software does not replace professional clinical judgment or institutional quality assurance procedures.

## Support & Contact

**For Issues:**
- GitHub Issues: [Report a bug or request a feature](https://github.com/SergeiRusetskii/ROcheck/issues)

**For Clinical Questions:**
- Consult your local medical physics team
- Refer to your institutional treatment planning protocols

**For ESAPI Questions:**
- [Varian Developer Community](https://varianapis.github.io/)
- [ESAPI Documentation](https://varianapis.github.io/)

---

**Developed by**: Sergei Rusetskii
**Powered by**: Varian Eclipse Scripting API v18.0+
**Framework**: Claude Code Starter v2.1

*ROcheck - Ensuring quality and safety in radiation therapy planning*

