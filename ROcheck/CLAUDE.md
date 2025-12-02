# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a C# Eclipse Scripting API (ESAPI) plugin for Varian Eclipse treatment planning system. The project builds a plan validation tool called "ROcheck" that focuses on clinical-goal and structure QA for radiation therapy treatment plans.

**Key Dependencies:**
- VMS.TPS.Common.Model.API (Eclipse Scripting API)
- VMS.TPS.Common.Model.Types 
- .NET Framework 4.8
- WPF for user interface

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

The output is an ESAPI plugin file: `ROcheck.esapi.dll` in the `Release/` or `bin/` directory.

## Architecture

### Core Components

1. **Script.cs** - Entry point for the ESAPI plugin. Creates the main UI window and initializes the validation system.

2. **Validators.cs** - Contains the validation engine with a composite pattern architecture:
   - `ValidatorBase`: Abstract base class for all validators
   - `CompositeValidator`: Base for validators that contain child validators
   - `RootValidator`: Main validator that orchestrates all validation checks
   - Individual validator classes for specific plan checks

3. **ValidationViewModel.cs** - MVVM pattern view model that:
   - Executes validation using `RootValidator`
   - Exposes `ObservableCollection<ValidationResult>` for UI binding
   - Defines `ValidationResult` class with severity levels

4. **MainControl.xaml/.cs** - WPF UserControl that displays validation results in a grouped, categorized list with severity indicators

5. **SeverityToColorConverter.cs** - WPF value converter that maps validation severity to colors for UI display

### Validation System Design

The validation system uses a composite pattern where:
- Each validator inherits from `ValidatorBase` and implements `Validate(ScriptContext context)`
- Validators are organized hierarchically under `RootValidator`
- Results are categorized (e.g., "Plan Parameters", "Dose Calculations") 
- Each result has a severity: Error, Warning, or Info

### ESAPI Integration

- Uses Varian Eclipse Scripting API for accessing treatment plan data
- Requires x64 platform targeting
- Plugin DLL must be placed in Eclipse's plugin directory
- Executed within Eclipse treatment planning context

## Version Management

Current version: v1.6.0 (as shown in Script.cs window title)
Versioned releases are stored in directories like "Script V1.0", "Script V1.2", etc.