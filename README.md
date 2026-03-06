# ROcheck

ROcheck is a focused quality assurance plugin for Varian Eclipse treatment planning workflows. It validates structure setup, clinical goal configuration, and related planning consistency checks for radiotherapy plans.

## Variants

- `Variants/ClinicE/` targets Eclipse 18.0 and uses configuration-driven validation settings.
- `Variants/ClinicH/` targets Eclipse 16.1 and preserves a compatibility-focused implementation.

## Validation Coverage

- Clinical goals coverage
- Target containment
- PTV and OAR overlap conflicts
- Target resolution checks
- Structure type validation
- SIB dose unit validation
- PTV to body proximity checks

## Repository Layout

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

## License

See [LICENSE](LICENSE) for licensing terms.
