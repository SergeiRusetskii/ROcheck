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
3. Restart Eclipse - the plugin will appear in the Scripts menu as "ROcheck v1.0"

## Architecture

### Core Components

1. **Script.cs** - Entry point for the ESAPI plugin. Creates the main UI window (650x1000) and initializes the validation system via `ValidationViewModel`. Validates that a course and plan are loaded before executing.

2. **Validators.cs** - Contains the validation engine with a composite pattern architecture:
   - `ValidatorBase`: Abstract base class for all validators
   - `CompositeValidator`: Base for validators that contain child validators
   - `RootValidator`: Main validator that orchestrates all validation checks
   - Individual validator classes for specific plan checks (see Validator Classes section)
   - `PlanUtilities`: Static utility class with helper methods for common checks

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
- Results are categorized (e.g., "Clinical Goals", "Structure Setup", "Dose Calculations")
- Each result has a severity level: `Error`, `Warning`, or `Info`
- Field-level results can be collapsed into summary results if all pass

### Validator Classes

The following validators are implemented:

**Structure Validators:**
- `CourseValidator`: Validates course-level configuration
- `PlanValidator`: Composite validator for plan-level checks
- `PlanningStructuresValidator`: Validates structure setup, types, containment, and high-resolution requirements

**Geometry & Setup Validators:**
- `CTAndPatientValidator`: Validates CT and patient setup
- `UserOriginMarkerValidator`: Validates user origin placement
- `SetupFieldsValidator`: Validates setup field configuration
- `FixationValidator`: Validates patient fixation setup

**Treatment Field Validators:**
- `FieldsValidator`: Composite validator for field-level checks
- `FieldNamesValidator`: Validates field naming conventions
- `GeometryValidator`: Validates beam geometry and collision risks

**Dosimetry Validators:**
- `DoseValidator`: Validates dose calculations
- `ReferencePointValidator`: Validates reference point configuration

**Planning Validators:**
- `OptimizationValidator`: Validates optimization parameters
- `ClinicalGoalsValidator`: Validates clinical goal coverage and consistency

### Key Features

1. **Clinical goal coverage**: Ensures applicable structures have at least one associated clinical goal
2. **Target containment**: Flags GTV/CTV volumes that extend beyond their paired PTVs
3. **PTV-OAR overlap awareness**: Warns when lower-dose PTV goals intersect OAR Dmax objectives
4. **Small target resolution**: Enforces high-resolution structures for small-volume PTVs and related targets
5. **Structure typing**: Validates that PTV/CTV/GTV structures are labeled with the correct types
6. **Severity-based results**: Color-coded results with Error, Warning, and Info levels grouped by validation category

### ESAPI Integration

- Uses Varian Eclipse Scripting API for accessing treatment plan data
- Requires x64 platform targeting
- Plugin DLL must be placed in Eclipse's plugin directory
- Executed within Eclipse treatment planning context with loaded course and plan
- Namespace `VMS.TPS` is required for Script.cs entry point

## Version Management

Current version: v1.0 (as shown in Script.cs window title and README)

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

## Machine-Specific Configuration

The codebase includes machine-specific checks:
- Edge machine: `TrueBeamSN6368`
- Halcyon machines: Identified by name prefix "Halcyon"
- Update `PlanUtilities` methods if adding support for new machines

## Reference Documentation

- Varian Eclipse Scripting API documentation (v18.0+)
- WPF and MVVM patterns for UI components
- .NET Framework 4.8 documentation
