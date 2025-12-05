# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ROcheck is a focused quality assurance tool for Varian Eclipse treatment planning system that performs automated validation checks on structure setup and clinical goal configuration for radiation therapy treatment plans. This C# Eclipse Scripting API (ESAPI) plugin provides systematic validation to ensure quality and safety in radiation therapy.

**Key Dependencies:**
- VMS.TPS.Common.Model.API (Eclipse Scripting API v18.0+)
- VMS.TPS.Common.Model.Types
- .NET Framework 4.8
- WPF for user interface
- Windows x64 platform

## Build Commands

**Build the project:**
```bash
msbuild ROcheck.sln /p:Configuration=Release /p:Platform=x64
```

**Debug build:**
```bash
msbuild ROcheck.sln /p:Configuration=Debug /p:Platform=x64
```

**Clean:**
```bash
msbuild ROcheck.sln /t:Clean
```

The output is an ESAPI plugin file: `ROcheck.esapi.dll` in the `Release/` directory (for Release builds) or `bin/x64/Debug/` directory (for Debug builds).

## Installation

1. Build the project using the command above
2. Copy the generated `ROcheck.esapi.dll` file to your Eclipse plugins directory
3. Restart Eclipse - the plugin will appear in the Scripts menu as "ROcheck v1.2.0"

## Architecture

### Core Components

1. **Script.cs** - Entry point for the ESAPI plugin. Creates the main UI window (650x1000) and initializes the validation system via `ValidationViewModel`. Validates that a course and plan are loaded before executing.

2. **Validators.cs** - Contains the validation engine with a composite pattern architecture:
   - `ValidatorBase`: Abstract base class for all validators
   - `CompositeValidator`: Base for validators that contain child validators
   - `RootValidator`: Main validator that orchestrates all validation checks
   - `ClinicalGoalsValidator`: Core validator for structure setup and clinical goal validation

3. **ValidationViewModel.cs** - MVVM pattern view model that:
   - Executes validation using `RootValidator`
   - Exposes `ObservableCollection<ValidationResult>` for UI binding
   - Implements result post-processing to collapse multiple passing field results into summary messages
   - Defines `ValidationResult` class with Category, Message, Severity, and IsFieldResult properties

4. **MainControl.xaml/.cs** - WPF UserControl that displays validation results:
   - Grouped by validation category
   - Color-coded severity indicators (Error=Red, Warning=Orange, Info=Green)
   - Scrollable results display

5. **SeverityToColorConverter.cs** - WPF value converter that maps `ValidationSeverity` enum to WPF color brushes for UI display.

### Validation System Design

The validation system uses a composite pattern where:
- Each validator inherits from `ValidatorBase` and implements `Validate(ScriptContext context)`
- Validators are organized hierarchically under `RootValidator`
- Results are categorized (e.g., "ClinicalGoals.Structures", "ClinicalGoals.TargetContainment", "ClinicalGoals.Resolution")
- Each result has a severity level: `Error`, `Warning`, or `Info`
- Results focus on structure setup and clinical goal consistency

### Validator Classes

**Core Validator:**
- `ClinicalGoalsValidator`: The primary validator that performs all ROcheck validations:
  - Clinical goal presence for structures
  - Target containment (GTV/CTV within PTVs)
  - PTV-OAR overlap detection with dose comparison
  - Small volume resolution requirements for PTVs
  - Structure type validation (PTV/CTV/GTV)

### Key Features

1. **Clinical goal coverage**: Ensures applicable structures have at least one associated clinical goal
   - Prescription-aware: GTV/CTV/PTV structures in dose prescription are validated
   - Non-prescription targets are automatically excluded from validation
   - Excludes support structures (DICOM type 'SUPPORT')
   - Excludes structures with specific keywords: 'wire', 'Encompass', 'Enc', 'Dose'

2. **Target containment**: Flags GTV/CTV volumes that extend beyond their paired PTVs

3. **Target-OAR overlap detection**: Identifies targets with lower dose goals that overlap OARs with conflicting Dmax constraints
   - Uses optimized algorithm: filters by dose comparison first (cheap), then checks spatial overlap (expensive)
   - Detects all structures with lower objectives (minimum dose goals using ≥ or > operators)
   - Detects all OARs with Dmax goals (maximum dose constraints)
   - Reports overlaps where target lower goal > OAR Dmax
   - Provides recommendation to create _eval structures and document in prescription

4. **Small target resolution**: Enforces high-resolution structures for small-volume PTVs and related targets
   - Error for PTVs <5cc without high resolution (was <10cc in v1.1.x)
   - Warning for PTVs 5-10cc without high resolution (was 10-20cc in v1.1.x)
   - Shows smallest PTV volume in all cases

5. **Structure typing**: Validates that PTV/CTV/GTV structures are labeled with the correct types

6. **Severity-based results**: Color-coded results with Error, Warning, and Info levels grouped by validation category

### Structure Exclusion Logic

The validation system intelligently determines which structures to check for clinical goals:

**Always Excluded:**
- Structures with DICOM type 'SUPPORT'
- Structures starting with 'z_'
- Structures containing: 'wire', 'Encompass', 'Enc', 'Dose'
- Structures in ExcludedStructures list: 'Bones', 'CouchInterior', 'CouchSurface', 'Clips', 'Scar_Wire'

**Prescription-Aware Exclusion (v1.1.1+):**
- GTV/CTV/PTV structures are checked against plan.RTPrescription.Targets
- If a GTV/CTV/PTV is **in** the prescription → it is **validated** for clinical goals
- If a GTV/CTV/PTV is **not in** the prescription → it is **excluded** from validation
- This ensures only active treatment targets are checked, while excluding evaluation/backup structures

**Implementation:**
- `GetPrescriptionTargetIds()` extracts target IDs from RTPrescription.Targets using reflection
- `IsStructureExcluded()` receives prescriptionTargetIds and applies exclusion logic
- Uses case-insensitive HashSet for efficient target ID lookup

### ESAPI Integration

- Uses Varian Eclipse Scripting API for accessing treatment plan data
- Accesses plan.RTPrescription.Targets for prescription-aware validation
- Requires x64 platform targeting
- Plugin DLL must be placed in Eclipse's plugin directory
- Executed within Eclipse treatment planning context with loaded course and plan
- Namespace `VMS.TPS` is required for Script.cs entry point

## Version Management

Current version: v1.2.0 (as shown in Script.cs window title)

### Version History
- **v1.2.0**: Target-OAR overlap detection now fully working
  - Fixed clinical goal detection using Unicode comparison operators (≥, ≤, >, <)
  - Implemented optimized overlap detection algorithm (dose filter first, then spatial overlap)
  - Parse dose values from ObjectiveAsString using regex when properties unavailable
  - Updated resolution thresholds: <5cc error, 5-10cc warning (was <10cc error, 10-20cc warning)
  - Cleaner output with single recommendation message for overlaps
  - Comprehensive XML documentation throughout codebase

- **v1.1.1**: Enhanced structure exclusion logic
  - Exclude structures with DICOM type 'SUPPORT'
  - Smart GTV/CTV/PTV exclusion: only exclude targets NOT in prescription
  - Targets included in dose prescription are validated for clinical goals
  - Added exclusion for structures containing 'Encompass', 'Enc', 'Dose'
  - Improved resolution info messages with PTV count details

- **v1.1.0**: Clinical goals detection improvements
  - Comprehensive multi-method clinical goals access (5 different approaches)
  - Fixed clinical goals detection across different ESAPI versions
  - Refined validation categories (Structure Coverage, Target Containment, etc.)
  - Added structure exclusions for wire-related structures

- **v1.0**: Initial release with core validation features

## Development Guidelines

### Adding New Validators

1. Create a new validator class inheriting from `ValidatorBase` in Validators.cs
2. Implement the `Validate(ScriptContext context)` method
3. Return a list of `ValidationResult` objects with appropriate Category, Message, and Severity
4. Add the validator to the appropriate composite validator (typically `RootValidator`)
5. Follow the existing pattern for error handling and null checks

### Code Style

- Use clear, descriptive validator names ending with "Validator"
- Group related validators under composite validators
- Use `PlanUtilities` for common checks to avoid code duplication
- Set appropriate severity levels: Error for critical issues, Warning for review items, Info for confirmations
- Include structure/field identifiers in validation messages for clarity

### Testing

- Test within Eclipse environment with loaded treatment plans
- Verify validation logic with various plan configurations
- Test edge cases and null conditions
- Ensure UI displays results correctly with proper grouping and color coding

## Scope

ROcheck is specifically focused on structure setup and clinical goal validation. It does NOT include:
- Course ID validation
- CT/User origin validation
- Dose grid/technique validation
- Field naming or geometry validation
- Setup field validation
- Optimization parameter validation
- Reference point validation
- Fixation device validation

These comprehensive plan checks are handled by the separate PlanCrossCheck tool.

## Reference Documentation

- Varian Eclipse Scripting API documentation (v18.0+)
- WPF and MVVM patterns for UI components
- .NET Framework 4.8 documentation
