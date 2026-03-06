# ROcheck ClinicH

This variant targets Varian Eclipse 16.1 and keeps a compatibility-focused implementation for the shared ROcheck validation set.

> **WARNING**
>
> This software has not undergone FDA clearance. Clinical decisions must be made by qualified professionals. ROcheck supplements, and does not replace, institutional QA procedures.

## System Configuration

- Eclipse 16.1 compatibility
- .NET Framework 4.8
- Windows x64
- Shared validation infrastructure from `Core/`
- Variant-specific validators and entry points under `Variants/ClinicH/`
- Compatibility-focused implementation with hardcoded thresholds where ClinicE uses configuration

## Validation Components

### 1. Clinical Goals Coverage

**What it checks:**
- applicable structures have at least one clinical goal
- prescription-derived target filtering is used where available
- support and excluded structures are skipped

**Outputs:**
- `Warning` when a checked structure has no associated clinical goal
- `Info` when all applicable structures are covered
- `Info` when no prescriptions are available and target checks are skipped

**Clinical relevance:** prevents incomplete goal setup from being silently missed during plan review.

### 2. Target Containment

**What it checks:**
- matching `CTV` and `GTV` structures are fully contained inside the corresponding `PTV`
- targets are matched by suffix, for example `PTV_70`, `CTV_70`, `GTV_70`

**Outputs:**
- `Error` when a linked target extends outside its PTV
- `Info` when all checked target pairs are contained correctly

**Clinical relevance:** detects target hierarchy inconsistencies that can undermine planning intent.

### 3. Target-OAR Overlap

**What it checks:**
- lower-bound target goals are compared with OAR `Dmax` goals
- only dose-conflicting target/OAR pairs are tested for spatial overlap

**Outputs:**
- `Warning` when a target with a higher lower goal overlaps an OAR with a lower `Dmax`
- `Info` recommending `_eval` structures when overlaps are found
- `Info` when no relevant overlaps are detected

**Clinical relevance:** highlights geometric conflicts between target coverage goals and organ-at-risk constraints.

### 4. PTV-Body Proximity

**What it checks:**
- minimum distance from each PTV surface to the external `BODY` structure
- all CT slices with contours are evaluated
- ClinicH uses a 4 mm threshold

**Outputs:**
- `Warning` when a PTV is within threshold of the body surface
- `Info` showing the closest acceptable PTV when all are outside threshold
- `Warning` if the external body structure cannot be validated

**Clinical relevance:** prompts review of superficial targets that may require an `EVAL` structure for optimization.

### 5. Target Resolution

**What it checks:**
- high-resolution contouring for small PTVs and their linked CTV/GTV structures
- ClinicH uses the same current thresholds as the implementation:
  - `<5 cc` as an error threshold
  - `<10 cc` as a warning threshold

**Outputs:**
- `Error` for very small targets that are not consistently high resolution
- `Warning` for small targets below the softer threshold
- `Info` summary when all checked PTVs pass

**Clinical relevance:** reinforces contour resolution expectations for small volumes where geometric precision matters most.

### 6. Structure Types

**What it checks:**
- `PTV*`, `CTV*`, and `GTV*` names align with DICOM structure types
- marker and support structures are excluded

**Outputs:**
- `Error` when a target structure name and DICOM type disagree
- `Info` when all checked target types are correct

**Clinical relevance:** helps keep structure semantics consistent across planning and downstream review.

### 7. SIB Dose Units

**What it checks:**
- detects SIB plans from target goal dose differences
- ClinicH currently uses a `>6%` threshold in code
- once SIB is detected, all clinical goals must use Gy rather than percentages

**Outputs:**
- `Error` for percentage-based dose goals in detected SIB plans
- silent pass when the plan is not SIB or all goals already use Gy

**Clinical relevance:** avoids ambiguous or inconsistent dose-unit usage in simultaneous integrated boost planning.

## Validation Architecture

```text
RootValidator
├── ClinicalGoalsCoverageValidator
├── TargetContainmentValidator
├── TargetOAROverlapValidator
├── PTVBodyProximityValidator
├── TargetResolutionValidator
├── StructureTypesValidator
└── SIBDoseUnitsValidator
```

ClinicH uses the shared validation model without the ClinicE configuration layer.

## Output Format

- `Error`: issue requiring correction before approval
- `Warning`: issue requiring review or clinical judgment
- `Info`: confirmation or summary of a successful check

Results are grouped by validation category in the plugin UI.

## Requirements

- Windows x64
- Varian Eclipse 16.1 with ESAPI access
- .NET Framework 4.8

## License

See the repository [LICENSE](../../LICENSE) for licensing terms.
