# ROcheck

**Focused Quality Assurance Tool for Varian Eclipse Treatment Planning System**

ROcheck is a C# ESAPI plugin that performs automated validation checks on structure setup and clinical goal configuration for radiation therapy treatment plans. It provides systematic validation to ensure quality and safety in radiation therapy planning.

---

## 🏗️ Multi-Variant Architecture

This repository uses a **monorepo architecture** supporting multiple clinic configurations and Eclipse versions:

### Variants

- **ClinicE** (Primary) - Eclipse 18.0 with configuration-driven validation
- **ClinicH** (Secondary) - Eclipse 16.1 with clinic-specific settings

### Repository Structure

```
ROcheck/
├── Core/                    # Shared infrastructure
│   ├── Base/               # ValidatorBase, IValidationConfig
│   ├── Models/             # ValidationResult classes
│   ├── UI/                 # WPF user interface
│   └── Helpers/            # Utility methods
│
├── Variants/
│   ├── ClinicE/            # Eclipse 18.0 variant
│   │   ├── Config/         # Clinic E configuration
│   │   └── Validators/     # Clinic E validators
│   │
│   └── ClinicH/            # Eclipse 16.1 variant
│       └── Validators/     # Clinic H validators
│
└── Documentation/          # ESAPI API references
    ├── ClinicE/           # Eclipse 18.0 docs
    └── ClinicH/           # Eclipse 16.1 docs
```

---

## ✨ Features

### Validation Categories

- **Clinical Goals Coverage** - Validates presence of clinical goals for all targets
- **Target Containment** - Ensures GTV/CTV structures are contained within PTVs
- **PTV-OAR Overlap** - Detects conflicting dose constraints
- **Target Resolution** - Validates high-resolution contouring for small PTVs
- **Structure Types** - Verifies proper PTV/CTV/GTV DICOM type labeling
- **SIB Dose Units** - Validates dose units in Simultaneous Integrated Boost plans
- **PTV-Body Proximity** - Checks distance from PTV to Body surface

### Prescription-Aware Validation

- Only targets in "Reviewed" prescriptions are validated
- Non-prescription targets automatically excluded
- Works with both linked and non-linked prescriptions

---

## 🚀 Quick Start

### Building

Each variant has its own project file:

```bash
# ClinicE (Eclipse 18.0)
msbuild Variants/ClinicE/ROcheck.esapi.csproj

# ClinicH (Eclipse 16.1)
msbuild Variants/ClinicH/ROcheck.esapi.csproj
```

### Deployment

Copy the compiled DLL to your Eclipse plugins directory:

- **ClinicE**: Copy to Eclipse 18.0 plugins directory
- **ClinicH**: Copy to Eclipse 16.1 plugins directory

### Usage

1. Load a treatment plan in Eclipse
2. Run ROcheck script from Scripts menu
3. Review validation results in the dialog
4. Address any errors or warnings before proceeding

---

## 📚 Documentation

### For Developers

- **Architecture Details**: See `.claude/ARCHITECTURE.md`
- **Development Workflow**: See `CLAUDE.md`
- **Current State**: See `.claude/SNAPSHOT.md`
- **Variant Status**:
  - ClinicE: `.claude/variants/clinicE.md`
  - ClinicH: `.claude/variants/clinicH.md`

### For Users

- **ClinicE README**: `Variants/ClinicE/README.md` (if exists)
- **ClinicH README**: `Variants/ClinicH/README.md` (if exists)
- **ESAPI Documentation**: `Documentation/` folder

---

## 🛠️ Technology Stack

- **Eclipse Scripting API (ESAPI)** - v18.0 (ClinicE), v16.1 (ClinicH)
- **.NET Framework 4.8**
- **WPF/XAML** - User interface
- **MVVM Pattern** - Architecture pattern
- **Windows x64** - Target platform

---

## 🔧 Configuration System

**ClinicE** uses a configuration-driven approach:

- Thresholds and exclusions defined in `ClinicEConfig.cs`
- Easy to adjust without code changes
- Implements `IValidationConfig` interface

**ClinicH** currently uses hardcoded values (config refactor pending manual port when requested).

---

## 📋 Development Workflow

### Primary Development (ClinicE)

All new features are developed in ClinicE first:

1. Implement in `Variants/ClinicE/`
2. Test with Eclipse 18.0
3. Update `.claude/variants/clinicE.md`
4. Commit and push

### Manual Porting (ClinicH)

ClinicH is updated only when explicitly requested:

1. Identify features to port from ClinicE
2. Copy and adapt code for ClinicH
3. Update `.claude/variants/clinicH.md` with porting log
4. Test with Eclipse 16.1
5. Commit with clear message

---

## 📦 Current Version

- **ClinicE**: v1.6.4
- **ClinicH**: v1.6.4

See variant tracking files in `.claude/variants/` for detailed change history.

---

## 📄 License

This project uses a **Community License** (free for internal use, commercial licensing available).

See variant-specific README files for detailed license information.

---

## 🤝 Contributing

This is a clinical tool repository. Contributions should follow:

1. Medical physics domain knowledge
2. Eclipse ESAPI best practices
3. Multi-variant architecture guidelines
4. Thorough testing with clinical plans

---

## 🔗 Resources

- [Varian Developer Portal](https://developer.varian.com/)
- [Eclipse Scripting API Documentation](Documentation/)
- [Project Architecture](.claude/ARCHITECTURE.md)

---

**Framework**: Built with [Claude Code Starter v2.1](https://github.com/rusetskiy/claude-code-starter)

**Maintained by**: Medical Physics Team

---

*For detailed technical documentation, see `.claude/` directory.*
