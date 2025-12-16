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
├── Validators/                                      # Validation logic (organized by concern)
│   ├── ValidatorBase.cs                            # Base validator classes (Composite pattern)
│   ├── ClinicalGoalsCoverageValidator.cs           # Clinical goal presence validation
│   ├── TargetContainmentValidator.cs               # GTV/CTV containment within PTV
│   ├── TargetOAROverlapValidator.cs                # Target-OAR dose conflict detection
│   ├── PTVBodyProximityValidator.cs                # PTV to Body surface proximity
│   ├── TargetResolutionValidator.cs                # Small volume high-res validation
│   ├── StructureTypesValidator.cs                  # DICOM type validation
│   └── SIBDoseUnitsValidator.cs                    # SIB dose unit validation
├── Script.cs                                        # ESAPI plugin entry point
├── ValidationResult.cs                              # Validation result classes and enums
├── RootValidator.cs                                 # Root validator (orchestrates all validators)
├── ValidationHelpers.cs                             # Helper methods and spatial algorithms
├── ValidationViewModel.cs                           # MVVM view model
├── MainControl.xaml                                 # WPF UI markup
├── MainControl.xaml.cs                             # WPF UI code-behind
├── SeverityToColorConverter.cs                     # UI color converter
├── Properties/
│   └── AssemblyInfo.cs                             # Version: v1.6.0
├── Examples/                                        # Example validators and patterns
├── Documentation/                                   # ESAPI XML and PDF documentation
├── ROcheck.csproj                                  # Project file (x64, .NET 4.8)
├── ROcheck.sln                                     # Solution file
└── .claude/                                        # Framework files
```

## Key Components

### Script.cs
**Location:** `/Script.cs`
**Purpose:** Entry point for the ESAPI plugin
- Creates main UI window (650x1000 pixels)
- Initializes validation system via ValidationViewModel
- Validates that course and plan are loaded before executing
- Requires namespace `VMS.TPS` for ESAPI entry point
- Window title: "ROcheck v1.6.0"
- Assembly name: "ROcheck.esapi"

**Note on TEST_ Prefix:**
When switching between testing and production, update ALL THREE locations:
1. `ROcheck.csproj` - `<AssemblyName>` tag (line 11)
2. `Properties/AssemblyInfo.cs` - `AssemblyProduct` attribute (line 12)
3. `Script.cs` - `window.Title` property (line ~35)

See CLAUDE.md for complete procedure.

### ValidationResult.cs
**Location:** `/ValidationResult.cs`
**Purpose:** Core validation data structures
- `ValidationSeverity`: Enum (Error, Warning, Info)
- `ValidationResult`: Class with Category, Message, Severity, IsFieldResult properties

### Validators/ValidatorBase.cs
**Location:** `/Validators/ValidatorBase.cs`
**Namespace:** `ROcheck.Validators`
**Purpose:** Base classes for validation system
- `ValidatorBase`: Abstract base class for all validators
- `CompositeValidator`: Base for validators that contain child validators (Composite pattern)

### RootValidator.cs
**Location:** `/RootValidator.cs`
**Namespace:** `ROcheck`
**Purpose:** Entry point for validation system
- Orchestrates all 7 specialized validators
- Organizes validators by category (clinical goals, geometry, properties, dose)

### Specialized Validators (Validators/ folder)
**Namespace:** `ROcheck.Validators`

#### ClinicalGoalsCoverageValidator.cs
- Validates that all applicable structures have at least one clinical goal
- Uses prescription-aware filtering

#### TargetContainmentValidator.cs
- Validates GTV/CTV volumes are fully contained within their PTV
- Voxel-based spatial containment detection

#### TargetOAROverlapValidator.cs
- Detects target-OAR dose conflicts with spatial overlap
- Warns when PTV lower goals exceed OAR Dmax constraints

#### PTVBodyProximityValidator.cs
- Checks minimum distance from PTV to BODY surface
- Warns if distance ≤4mm, suggests EVAL structures

#### TargetResolutionValidator.cs
- Validates small PTVs (<10cc) use high-resolution contouring
- Error for <5cc, Warning for 5-10cc

#### StructureTypesValidator.cs
- Validates DICOM structure types match naming (PTV→PTV, CTV→CTV, GTV→GTV)

#### SIBDoseUnitsValidator.cs
- Detects SIB plans (>6% dose difference between targets)
- Ensures all goals use Gy units (no percentages)

### ValidationHelpers.cs
**Location:** `/ValidationHelpers.cs`
**Namespace:** `ROcheck`
**Purpose:** Static helper methods for validation operations
- Clinical goals retrieval (cross-version compatible via reflection)
- Structure goal lookup building
- Dose extraction and parsing
- Goal type detection (lower goals, Dmax goals)
- Prescription target extraction
- SIB detection helpers
- Percentage dose detection
- Spatial algorithms (IsStructureContained, StructuresOverlap)
- Structure exclusion logic

### ValidationViewModel.cs
**Location:** `/ValidationViewModel.cs`
**Purpose:** MVVM pattern view model
- Executes validation using `RootValidator`
- Exposes `ObservableCollection<ValidationResult>` for UI binding
- Result post-processing: collapses multiple passing field results into summary messages

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
    ├── Structure Type validation
    └── SIB Dose Units validation
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
- Excludes structures starting with: 'z_', 'Implant', 'Lymph', 'LN_'
- Excludes: 'Bones', 'CouchInterior', 'CouchSurface', 'Clips', 'Scar_Wire', 'Sternum'

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

### 6. SIB Dose Units (ClinicalGoals.SIB)
**Purpose:** Validate dose units in SIB (Simultaneously Integrated Boost) plans
**Algorithm:**
1. SIB Detection: Compare target clinical goal doses, if >6% difference → SIB
2. Dose Unit Validation: In SIB plans, ALL clinical goals (targets + OARs) must use Gy
3. Percentage detection: Regex pattern `@"[<>≤≥]\s*\d+\.?\d*\s*%"` with volume constraint exclusion
4. Reports Error for percentage dose units in SIB plans

**Important Notes:**
- Uses clinical goals ONLY for SIB detection
- RTPrescription not accessible at script launch (documented limitation)
- D95% goals prioritized for target dose comparison
- Volume percentage constraints (V X Gy ≥ Y%) are allowed
- Silent pass: No messages unless Error found

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
**IMPORTANT:** User builds manually - do NOT run build commands via Bash tool.
```bash
# User will execute manually:
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

## ESAPI API Reference Documentation

**Location:** `Documentation/`

### XML IntelliSense Documentation

**VMS.TPS.Common.Model.API.xml**
- Complete API reference for VMS.TPS.Common.Model.API.dll
- Contains IntelliSense documentation for all ESAPI classes, methods, properties
- Essential for development - look up any ESAPI type, method, or property
- Use with Read tool to find method signatures and descriptions

**VMS.TPS.Common.Model.Types.xml**
- Complete API reference for VMS.TPS.Common.Model.Types.dll
- Contains IntelliSense documentation for all ESAPI types and enums
- Essential for understanding data types used in ESAPI

### How to Use XML Documentation

**Finding a method:**
```bash
# Search for a specific class or method
grep -A 10 "T:VMS.TPS.Common.Model.API.Structure" Documentation/VMS.TPS.Common.Model.API.xml
grep -A 10 "M:VMS.TPS.Common.Model.API.Structure.OverlapsWith" Documentation/VMS.TPS.Common.Model.API.xml
```

**Reading method documentation:**
Use the Read tool to read specific sections of the XML files:
```
Read Documentation/VMS.TPS.Common.Model.API.xml
Search for member name="M:VMS.TPS.Common.Model.API.Structure.OverlapsWith"
```

**Common classes to reference:**
- `Structure` - Structure geometry and properties
- `PlanSetup` - Treatment plan data
- `Dose` - Dose distribution
- `StructureSet` - Collection of structures
- `Course` - Course and prescription data
- `Patient` - Patient demographics
- `Image` - CT/MR imaging data

### PDF Documentation

**Eclipse Scripting API Reference Guide 18.0.pdf**
- Official ESAPI reference manual
- Architecture overview, code examples, best practices
- Essential reading for understanding ESAPI concepts

**Image Registration and Segmentation Scripting API Reference Guide.pdf**
- Advanced image processing and registration
- Segmentation algorithms

**VarianApiBook.pdf**
- Comprehensive ESAPI programming guide
- Code samples and patterns
- Advanced topics

### When to Use Documentation

**During /feature planning:**
- Check XML docs to verify method availability
- Understand method parameters and return types
- Find example usage patterns

**During /explain:**
- Reference XML docs to explain ESAPI method behavior
- Look up property descriptions
- Understand type relationships

**During development:**
- Verify method signatures before implementing
- Check for null return possibilities
- Understand threading requirements

**Example workflow:**
1. Need to check if Structure has Volume property
2. `grep "P:VMS.TPS.Common.Model.API.Structure.Volume" Documentation/VMS.TPS.Common.Model.API.xml`
3. Read documentation to understand data type and null safety
4. Implement with proper null checks

---
*Framework: Claude Code Starter v2.1*
