# ROcheck

A focused quality assurance tool for Varian Eclipse treatment planning system that performs automated validation checks on structure setup and clinical goal configuration for radiation therapy treatment plans.

## Overview

ROcheck is an Eclipse Scripting API (ESAPI) plugin that provides systematic validation of target/OAR structure setup and clinical goal consistency to ensure quality and safety in radiation therapy.

## Features

- **Clinical goal coverage**: Ensures applicable structures have at least one associated clinical goal.
- **Target containment**: Flags GTV/CTV volumes that extend beyond their paired PTVs.
- **PTV–OAR overlap awareness**: Warns when lower-dose PTV goals intersect OAR Dmax objectives.
- **Small target resolution**: Enforces high-resolution structures for small-volume PTVs and related targets.
- **Structure typing**: Validates that PTV/CTV/GTV structures are labeled with the correct types.
- **Severity-based results**: Color-coded results with Error, Warning, and Info levels grouped by validation category.

## Requirements

- Varian Eclipse Treatment Planning System v18.0 or later
- .NET Framework 4.8
- Eclipse Scripting API (ESAPI) access
- Windows x64 platform

## Installation

1. Build the project using MSBuild:
   ```bash
   msbuild ROcheck.sln /p:Configuration=Release /p:Platform=x64
   ```

2. Copy the generated `ROcheck.esapi.dll` file to your Eclipse plugins directory

3. Restart Eclipse and the plugin will be available in the Scripts menu

## Usage

1. Load a treatment plan in Eclipse
2. Navigate to Scripts menu and select "ROcheck v1.1.1"
3. The validation tool will automatically analyze the current plan
4. Review results organized by category with severity indicators:
   - 🔴 **Error**: Critical issues that must be addressed
   - 🟡 **Warning**: Items requiring attention or review
   - 🔵 **Info**: Informational messages and confirmations

## Development

### Building

```bash
# Release build
msbuild ROcheck.sln /p:Configuration=Release /p:Platform=x64

# Debug build  
msbuild ROcheck.sln /p:Configuration=Debug /p:Platform=x64

# Clean
msbuild ROcheck.sln /t:Clean
```

### Architecture

The project uses a composite validator pattern:
- `ValidatorBase`: Abstract base for all validators
- `RootValidator`: Main orchestrator for all validation checks
- Individual validator classes for specific plan aspects
- MVVM pattern with WPF UI for results display

### Adding New Validators

1. Create a new validator class inheriting from `ValidatorBase`
2. Implement the `Validate(ScriptContext context)` method
3. Add the validator to the appropriate composite validator
4. Define validation category and severity levels

## Version History

- **v1.1.1**: Enhanced structure exclusion logic with prescription-aware target validation and support structure filtering
- **v1.1.0**: Comprehensive clinical goals detection with multi-method access and refined validation categories
- **v1.0**: Initial release focusing on target/OAR structure QA and clinical goal consistency checks

## License

This project is intended for use with Varian Eclipse treatment planning systems in clinical radiation therapy environments.

## Support

For issues or questions related to this validation tool, please consult your local medical physics team or Eclipse system administrators.
