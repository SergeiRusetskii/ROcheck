# CLAUDE.md — AI Agent Instructions

**Framework:** Claude Code Starter v2.5.1
**Type:** Meta-framework extending Claude Code capabilities

---

## Project-Specific Rules

**Build Policy:**
- User builds the project manually
- NEVER execute msbuild or other build commands via Bash tool
- Only suggest build commands if needed, but do not execute them

**TEST_ Prefix Procedure:**

When user requests to add or remove TEST_ prefix, update ALL THREE locations per variant:

**ClinicE:**
1. `Variants/ClinicE/ROcheck.esapi.csproj` (line 11)
2. `Variants/ClinicE/Properties/AssemblyInfo.cs` (line 12)
3. `Variants/ClinicE/Script.cs` (line ~35)

**ClinicH:**
1. `Variants/ClinicH/ROcheck.esapi.csproj` (line 11)
2. `Variants/ClinicH/Properties/AssemblyInfo.cs` (line 12)
3. `Variants/ClinicH/Script.cs` (line ~35)

**CRITICAL:** All three files must be updated together per variant. Missing the .csproj file means the build will use the wrong assembly name.

**Version Increment Policy:**

**IMPORTANT:** Eclipse caches scripts by version number and will NOT reload a modified script if the version stays the same.

When user provides feedback from real Eclipse script testing (NOT build errors), increment the PATCH version in ALL THREE locations:

1. **Properties/AssemblyInfo.cs** (lines 36-37):
   ```
   [assembly: AssemblyVersion("1.6.X.0")]
   [assembly: AssemblyFileVersion("1.6.X.0")]
   ```
2. **Script.cs** (line ~35): `window.Title = "ROcheck v1.6.X";`
3. Update `.claude/SNAPSHOT.md` with new version

**Version increment rules:**
- Patch version (1.6.4 → 1.6.5): After each real Eclipse testing feedback
- Minor version (1.6.X → 1.7.0): New features or significant changes
- Major version (1.X.X → 2.0.0): Major refactoring or breaking changes

---

## Triggers

**"start", "начать":**
→ Execute Cold Start Protocol

**"заверши", "завершить", "finish", "done":**
→ Execute Completion Protocol

---

## Cold Start Protocol

### Step 0: First Launch Detection

**Check for migration context first:**
```bash
cat .claude/migration-context.json 2>/dev/null
```

If file exists, this is first launch after installation.

**Read context and route:**
- If `"mode": "legacy"` → Execute Legacy Migration workflow
- If `"mode": "upgrade"` → Execute Framework Upgrade workflow
- If `"mode": "new"` → Execute New Project Setup workflow

After completing workflow, delete marker:
```bash
rm .claude/migration-context.json
```

If no migration context, continue to Step 0.1 (Crash Recovery).

### Step 0.1: Crash Recovery
```bash
cat .claude/.last_session
```
- If `"status": "active"` → Previous session crashed:
  1. `git status` — check uncommitted changes
  2. Read `.claude/SNAPSHOT.md` for context
  3. Ask: "Continue or commit first?"
- If `"status": "clean"` → OK, continue to Step 1

### Step 1: Mark Session Active
```bash
echo '{"status": "active", "timestamp": "'$(date -Iseconds)'"}' > .claude/.last_session
```

### Step 2: Load Context
Read `.claude/SNAPSHOT.md` — current version, what's in progress

### Step 3: Context (on demand)
- `.claude/BACKLOG.md` — current sprint tasks (always read)
- `.claude/ROADMAP.md` — strategic direction (read to understand context)
- `.claude/ARCHITECTURE.md` — code structure (read if working with code)

### Step 4: Confirm
```
Context loaded. Directory: [pwd]
Framework: Claude Code Starter v2.5.1
Ready to work!
```

---

## Completion Protocol

### 1. Build (if code changed)
- User builds manually (see Build Policy above)
- Note what needs building

### 2. Update Metafiles
- `.claude/BACKLOG.md` — mark completed tasks `[x]`
- `.claude/SNAPSHOT.md` — update version and status
- `CHANGELOG.md` — add entry (if release)
- `.claude/ARCHITECTURE.md` — update if code structure changed
- `.claude/variants/clinicE.md` or `clinicH.md` — if variant-specific changes

### 3. Git Commit
```bash
git add -A && git status
git commit -m "$(cat <<'EOF'
type: Brief description

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

### 4. Ask About Push & PR

**Push:**
- Ask user: "Push to remote?"
- If yes: `git push`

**Check PR status:**
```bash
git log origin/main..HEAD --oneline
```
- If **empty** → All merged, no PR needed
- If **has commits** → Ask: "Create PR?"

### 5. Mark Session Clean
```bash
echo '{"status": "clean", "timestamp": "'$(date -Iseconds)'"}' > .claude/.last_session
```

---

## Multi-Variant Workflow

**Architecture:** Monorepo with two clinic variants

### Variant Structure

**Primary Variant (ClinicE):**
- Location: `Variants/ClinicE/`
- Eclipse: 18.0
- Configuration-driven (uses IValidationConfig)
- All development happens here first

**Secondary Variant (ClinicH):**
- Location: `Variants/ClinicH/`
- Eclipse: 16.1
- Hardcoded values (config refactor pending)
- Updated only when explicitly requested

**Shared Infrastructure (Core/):**
- `Core/Base/` - ValidatorBase, IValidationConfig
- `Core/Models/` - ValidationResult
- `Core/UI/` - MainControl, SeverityToColorConverter
- `Core/Helpers/` - ValidationHelpers
- Shared by both variants automatically

### Development Workflow

**Normal Development (ClinicE Primary):**

1. All new work happens in ClinicE:
   - Develop in `Variants/ClinicE/`
   - Update `.claude/SNAPSHOT.md` with ClinicE version
   - Update `.claude/variants/clinicE.md` with changes
   - Commit and push

2. ClinicH is NOT automatically updated

**Manual Porting (ClinicE → ClinicH):**

Only happens when explicitly requested by user.

1. Identify what to port:
   - Review changes in ClinicE between versions
   - User specifies which feature/fix to port

2. Port changes:
   ```bash
   # Copy changed files from ClinicE to ClinicH
   cp Variants/ClinicE/Validators/SomeValidator.cs Variants/ClinicH/Validators/
   # Adjust for ClinicH config differences (if needed)
   ```

3. Update metafiles:
   - `.claude/variants/clinicH.md` - Add to Porting Log
   - Note source ClinicE version
   - Update ClinicH version number if needed

4. Commit with clear message:
   ```bash
   git commit -m "port: Feature X to ClinicH (from ClinicE vX.X.X)"
   ```

**Core/ Infrastructure Changes:**

When Core/ infrastructure changes:
1. Both variants affected (since both reference Core/)
2. Note what needs building (user builds manually)
3. If ClinicH breaks, fix is required
4. Update both variant tracking files

### Variant Tracking Files

**`.claude/variants/clinicE.md`:**
- ClinicE deployment status
- Configuration details
- Testing notes
- Recent changes

**`.claude/variants/clinicH.md`:**
- ClinicH porting log
- Pending ports
- Testing status
- Configuration notes

---

## ESAPI Documentation

**Location:** `Documentation/` (organized by variant)

**XML IntelliSense Files:**

ClinicE (Eclipse 18.0):
- `Documentation/ClinicE/VMS.TPS.Common.Model.API.xml` - Eclipse 18 API reference
- `Documentation/ClinicE/VMS.TPS.Common.Model.Types.xml` - Eclipse 18 type definitions

ClinicH (Eclipse 16.1):
- `Documentation/ClinicH/VMS.TPS.Common.Model.API.xml` - Eclipse 16 API reference
- `Documentation/ClinicH/VMS.TPS.Common.Model.Types.xml` - Eclipse 16 type definitions

**PDF Reference Guides (Shared):**
- `Documentation/Eclipse Scripting API Reference Guide 18.0.pdf` - Official ESAPI manual
- `Documentation/VarianApiBook.pdf` - Comprehensive programming guide

**When to use:**
- Planning features - verify method availability and signatures
- Implementing code - check parameters, return types, null safety
- Explaining code - reference official documentation
- Troubleshooting - understand expected behavior
- **IMPORTANT:** Use correct variant documentation (ClinicE vs ClinicH)

See `.claude/ARCHITECTURE.md` for detailed usage guide.

---

## Repository Structure

```
ROcheck/  (Multi-variant monorepo)
├── Core/                           # Shared infrastructure
│   ├── Base/                       # ValidatorBase, IValidationConfig
│   ├── Models/                     # ValidationResult
│   ├── UI/                         # MainControl, SeverityToColorConverter
│   └── Helpers/                    # ValidationHelpers
│
├── Variants/
│   ├── ClinicE/                    # Primary variant (Eclipse 18.0)
│   │   ├── Config/                 # ClinicEConfig.cs
│   │   ├── Validators/             # 7 validators
│   │   ├── Script.cs               # ESAPI entry point
│   │   └── ROcheck.esapi.csproj
│   │
│   └── ClinicH/                    # Secondary variant (Eclipse 16.1)
│       ├── Validators/             # Manual port validators
│       ├── Script.cs
│       └── ROcheck.esapi.csproj
│
├── Documentation/
│   ├── ClinicE/                    # Eclipse 18 API docs
│   └── ClinicH/                    # Eclipse 16 API docs
│
└── .claude/
    ├── SNAPSHOT.md                 # Current state
    ├── BACKLOG.md                  # Sprint tasks
    ├── ARCHITECTURE.md             # Code structure
    └── variants/                   # Variant tracking
        ├── clinicE.md
        └── clinicH.md
```

## Slash Commands

**Core:** `/fi`, `/commit`, `/pr`
**Dev:** `/fix`, `/feature`, `/review`, `/test`, `/security`
**Quality:** `/explain`, `/refactor`, `/optimize`

## Key Principles

1. **Framework as AI Extension** — not just docs, but functionality
2. **Privacy by Default** — dialogs private in .gitignore
3. **Local Processing** — no external APIs
4. **Token Economy** — minimal context loading

## Warnings

- DO NOT skip Crash Recovery check
- DO NOT execute msbuild or build commands
- DO NOT commit without updating metafiles
- ALWAYS mark session clean at completion
- ALWAYS use correct variant documentation

---
*Framework: Claude Code Starter v2.5.1 | Updated: 2026-01-18*
