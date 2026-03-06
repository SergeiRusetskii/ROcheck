# ROcheck

ROcheck is a focused quality assurance plugin for Varian Eclipse treatment planning workflows. It validates structure setup, clinical goal configuration, and related planning consistency checks for radiotherapy plans.

> **WARNING**
>
> This software has not undergone FDA clearance. Clinical decisions must be made by qualified professionals. ROcheck supplements, and does not replace, institutional QA procedures.

## Overview

ROcheck is an Eclipse Scripting API plugin for automated treatment plan review. The repository uses a shared-core layout with clinic-specific variants for different Eclipse environments.

## Clinic Variants

- `Variants/ClinicE/` targets Eclipse 18.0 and uses configuration-driven validation settings.
- `Variants/ClinicH/` targets Eclipse 16.1 and preserves a compatibility-focused implementation.

Each variant README documents validator behavior and variant-specific constraints in more detail.

## Key Features

- Clinical goals coverage
- Target containment
- PTV and OAR overlap conflicts
- Target resolution checks
- Structure type validation
- SIB dose unit validation
- PTV to body proximity checks

## Requirements

- Windows x64
- Varian Eclipse with ESAPI access
- .NET Framework 4.8

## Structure

```text
ROcheck/
├── Core/
│   ├── Base/
│   ├── Helpers/
│   ├── Models/
│   └── UI/
└── Variants/
    ├── ClinicE/
    └── ClinicH/
```

`Core/` contains shared validation, model, helper, and UI infrastructure. Each variant supplies the root script entry point and any clinic-specific behavior.

## Contributing

Contributions are welcome. Public-facing documentation and code changes should:

- describe validation logic clearly
- preserve the existing clinic-variant structure
- avoid introducing local deployment or build instructions into public docs
- keep clinical claims aligned with observable code behavior

## License

See [LICENSE](LICENSE) for licensing terms.
