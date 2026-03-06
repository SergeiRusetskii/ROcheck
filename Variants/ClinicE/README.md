# ROcheck ClinicE

This variant targets Varian Eclipse 18.0 and uses configuration-driven validation settings for clinic-specific thresholds and exclusions.

## Scope

- Eclipse 18.0 compatibility
- Shared validation infrastructure from `Core/`
- Variant-specific settings in `Config/ClinicEConfig.cs`

## Validation Coverage

- Clinical goals coverage
- Target containment
- PTV and OAR overlap conflicts
- Target resolution checks
- Structure type validation
- SIB dose unit validation
- PTV to body proximity checks

## License

See the repository [LICENSE](../../LICENSE) for licensing terms.
