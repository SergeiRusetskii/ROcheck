# ARCHITECTURE — ROcheck

*Code structure and architecture documentation*

## Overview

ROcheck is a focused quality assurance tool for Varian Eclipse treatment planning system that performs automated validation checks on structure setup and clinical goal configuration for radiation therapy treatment plans.

**Tech Stack:**
- VMS.TPS.Common.Model.API (Eclipse Scripting API v18.0+)
- VMS.TPS.Common.Model.Types
- .NET Framework 4.8
- WPF for user interface
- Windows x64 platform

## Directory Structure

```
ROcheck/
├── Script.cs                      # ESAPI plugin entry point
├── Validators.cs                  # Validation engine (composite pattern)
├── ValidationViewModel.cs         # MVVM view model
├── MainControl.xaml               # WPF UI markup
├── MainControl.xaml.cs            # WPF UI code-behind
├── SeverityToColorConverter.cs   # UI color converter
├── Properties/
│   └── AssemblyInfo.cs           # Version: v1.2.0
├── ROcheck.csproj                # Project file (x64, .NET 4.8)
├── ROcheck.sln                   # Solution file
└── .claude/                      # Framework files
```

## Key Components

### Script.cs
**Location:** `/Script.cs`
**Purpose:** Entry point for the ESAPI plugin
- Creates main UI window (650x1000 pixels)
- Initializes validation system via ValidationViewModel
- Validates that course and plan are loaded before executing
- Requires namespace `VMS.TPS` for ESAPI entry point
- Window title: "ROcheck v1.2.0"

### Validators.cs
**Location:** `/Validators.cs`
**Purpose:** Validation engine with composite pattern architecture
- `ValidatorBase`: Abstract base class for all validators
- `CompositeValidator`: Base for validators that contain child validators
- `RootValidator`: Main validator orchestrating all checks
- `ClinicalGoalsValidator`: Core validator for structure setup and clinical goal validation

### ValidationViewModel.cs
**Location:** `/ValidationViewModel.cs`
**Purpose:** MVVM pattern view model
- Executes validation using `RootValidator`
- Exposes `ObservableCollection<ValidationResult>` for UI binding
- Result post-processing: collapses multiple passing field results into summary messages
- Defines `ValidationResult` class with Category, Message, Severity, IsFieldResult properties

### MainControl.xaml/.cs
**Location:** `/MainControl.xaml` and `/MainControl.xaml.cs`
**Purpose:** WPF UserControl displaying validation results
- Results grouped by validation category
- Color-coded severity indicators (Error=Red, Warning=Orange, Info=Green)
- Scrollable results display

### SeverityToColorConverter.cs
**Location:** `/SeverityToColorConverter.cs`
**Purpose:** WPF value converter
- Maps `ValidationSeverity` enum to WPF color brushes for UI display

## Architecture Patterns

**Pattern:** Composite Pattern + MVVM
**Description:**
- **Composite Pattern**: Validators organized hierarchically, each implementing `Validate(ScriptContext context)`
- **MVVM**: Separation of UI (MainControl.xaml) and logic (ValidationViewModel)
- **Observer Pattern**: ObservableCollection for automatic UI updates

## Validation System Design

### Composite Pattern Structure

```
RootValidator
└── ClinicalGoalsValidator
    ├── Structure Coverage validation
    ├── Target Containment validation
    ├── PTV-OAR Overlap detection
    ├── Target Resolution validation
    └── Structure Type validation
```

### Validation Flow

```
Script.cs
  ↓
ValidationViewModel
  ↓
RootValidator.Validate(context)
  ↓
ClinicalGoalsValidator
  ↓
List<ValidationResult>
  ↓
ObservableCollection (UI binding)
  ↓
MainControl.xaml (display)
```

## Validation Categories

### 1. Structure Coverage (ClinicalGoals.Structures)
**Purpose:** Ensure applicable structures have clinical goals
**Logic:**
- Prescription-aware: GTV/CTV/PTV in dose prescription are validated
- Non-prescription targets automatically excluded
- Excludes SUPPORT structures, structures with 'wire', 'Encompass', 'Enc', 'Dose'
- Excludes: 'Bones', 'CouchInterior', 'CouchSurface', 'Clips', 'Scar_Wire'

### 2. Target Containment (ClinicalGoals.TargetContainment)
**Purpose:** Flag GTV/CTV volumes extending beyond paired PTVs
**Logic:** Spatial containment check

### 3. PTV-OAR Overlap (ClinicalGoals.PTVOAROverlap)
**Purpose:** Identify targets with conflicting dose constraints
**Algorithm:**
1. Filter by dose comparison (cheap): target lower goal > OAR Dmax
2. Check spatial overlap (expensive): only for dose conflicts
3. Report overlaps with recommendation for _eval structures

### 4. Target Resolution (ClinicalGoals.Resolution)
**Purpose:** Enforce high-resolution structures for small PTVs
**Thresholds:**
- Error: PTV <5cc without high resolution
- Warning: PTV 5-10cc without high resolution
- Shows smallest PTV volume

### 5. Structure Types (ClinicalGoals.StructureTypes)
**Purpose:** Validate PTV/CTV/GTV proper labeling

## External Dependencies

**Required References:**
- VMS.TPS.Common.Model.API (from Eclipse Scripting API v18.0+)
- VMS.TPS.Common.Model.Types
- PresentationCore (WPF)
- PresentationFramework (WPF)
- WindowsBase (WPF)
- System.Xaml

**Installation:**
- Varian Eclipse must be installed
- ESAPI SDK must be available

## Configuration

**Environment:**
- Windows x64 platform
- .NET Framework 4.8
- Eclipse Scripting API v18.0+

**Build:**
```bash
msbuild ROcheck.sln /p:Configuration=Release /p:Platform=x64
```

**Output:**
- Release: `Release/ROcheck.esapi.dll`
- Debug: `bin/x64/Debug/ROcheck.esapi.dll`

## Installation & Deployment

1. Build the project (see Build Commands above)
2. Copy `ROcheck.esapi.dll` to Eclipse plugins directory
3. Restart Eclipse
4. Plugin appears in Scripts menu as "ROcheck v1.2.0"

## Testing Strategy

**Manual Testing in Eclipse:**
- Test within Eclipse environment with loaded treatment plans
- Verify validation logic with various plan configurations
- Test edge cases and null conditions
- Ensure UI displays results correctly with proper grouping and color coding

**Test Scenarios:**
- Plans with/without clinical goals
- Various structure types (PTV, CTV, GTV, OAR)
- Small volume PTVs (<5cc, 5-10cc, >10cc)
- Target-OAR overlaps with conflicting dose constraints
- Prescription targets vs non-prescription targets

## Code Style Guidelines

- Clear, descriptive validator names ending with "Validator"
- Group related validators under composite validators
- Appropriate severity levels: Error (critical), Warning (review), Info (confirmation)
- Include structure/field identifiers in validation messages
- Comprehensive XML documentation

## Scope

**ROcheck validates:**
✅ Structure setup and clinical goal configuration
✅ Target containment and overlap
✅ PTV resolution requirements
✅ Structure type labeling

**Out of scope (handled by PlanCrossCheck):**
❌ Course ID validation
❌ CT/User origin validation
❌ Dose grid/technique validation
❌ Field naming or geometry validation
❌ Setup field validation
❌ Optimization parameter validation
❌ Reference point validation
❌ Fixation device validation

---
*Framework: Claude Code Starter v2.1*
