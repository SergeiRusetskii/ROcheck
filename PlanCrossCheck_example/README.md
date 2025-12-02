# PlanCrossCheck

A comprehensive quality assurance tool for Varian Eclipse treatment planning system that performs automated validation checks on radiation therapy treatment plans.

## Overview

PlanCrossCheck is an Eclipse Scripting API (ESAPI) plugin that provides systematic validation of treatment plans to ensure quality and safety in radiation therapy. The tool performs multiple categories of checks including plan parameters, dose calculations, beam configurations, and clinical protocols.

## Features

- **Comprehensive Validation**: Automated checks across multiple categories of plan parameters
- **Severity-based Results**: Color-coded results with Error, Warning, and Info levels  
- **Grouped Display**: Results organized by validation category for easy review
- **Real-time Feedback**: Instant validation results within Eclipse treatment planning system
- **Extensible Architecture**: Modular validator system for easy addition of new checks

## Requirements

- Varian Eclipse Treatment Planning System v18.0 or later
- .NET Framework 4.8
- Eclipse Scripting API (ESAPI) access
- Windows x64 platform

## Installation

1. Build the project using MSBuild:
   ```bash
   msbuild PlanCrossCheck.sln /p:Configuration=Release /p:Platform=x64
   ```

2. Copy the generated `TEST_Cross_Check.esapi.dll` file to your Eclipse plugins directory

3. Restart Eclipse and the plugin will be available in the Scripts menu

## Usage

1. Load a treatment plan in Eclipse
2. Navigate to Scripts menu and select "Cross-check v1.6.0"
3. The validation tool will automatically analyze the current plan
4. Review results organized by category with severity indicators:
   - 🔴 **Error**: Critical issues that must be addressed
   - 🟡 **Warning**: Items requiring attention or review
   - 🔵 **Info**: Informational messages and confirmations

## Development

### Building

```bash
# Release build
msbuild PlanCrossCheck.sln /p:Configuration=Release /p:Platform=x64

# Debug build  
msbuild PlanCrossCheck.sln /p:Configuration=Debug /p:Platform=x64

# Clean
msbuild PlanCrossCheck.sln /t:Clean
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

- v1.6.0: Edge collision assessment with sector-based filtering, wraparound fix
- v1.5.7: Previous version

## License

This project is intended for use with Varian Eclipse treatment planning systems in clinical radiation therapy environments.

## Support

For issues or questions related to this validation tool, please consult your local medical physics team or Eclipse system administrators.
