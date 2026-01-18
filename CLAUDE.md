# CLAUDE.md — AI Agent Instructions

**Framework:** Claude Code Starter v2.1
**Type:** Meta-framework extending Claude Code capabilities

## Project-Specific Rules

**Build Policy:**
- User builds the project manually
- NEVER execute msbuild or other build commands via Bash tool
- Only suggest build commands if needed, but do not execute them

**TEST_ Prefix Procedure:**

When user requests to add or remove TEST_ prefix, update ALL THREE locations:

1. **ROcheck.csproj** (line 11):
   - Testing: `<AssemblyName>TEST_ROcheck.esapi</AssemblyName>`
   - Production: `<AssemblyName>ROcheck.esapi</AssemblyName>`

2. **Properties/AssemblyInfo.cs** (line 12):
   - Testing: `[assembly: AssemblyProduct("TEST_ROcheck.esapi")]`
   - Production: `[assembly: AssemblyProduct("ROcheck.esapi")]`

3. **Script.cs** (line ~35):
   - Testing: `window.Title = "TEST_ROcheck v1.X.X";`
   - Production: `window.Title = "ROcheck v1.X.X";`

**CRITICAL:** All three files must be updated together. Missing the .csproj file means the build will use the wrong assembly name.

**Version Increment Policy:**

**IMPORTANT:** Eclipse caches scripts by version number and will NOT reload a modified script if the version stays the same.

When user provides feedback from real Eclipse script testing (NOT build errors), increment the PATCH version in ALL THREE locations:

1. **ROcheck.csproj** (line 11): `<AssemblyName>TEST_ROcheck.esapi</AssemblyName>` - no version here
2. **Properties/AssemblyInfo.cs** (lines 36-37):
   ```
   [assembly: AssemblyVersion("1.5.X.0")]
   [assembly: AssemblyFileVersion("1.5.X.0")]
   ```
3. **Script.cs** (line ~35): `window.Title = "TEST_ROcheck v1.5.X";`

**Version increment rules:**
- Patch version (1.5.1 → 1.5.2): After each real Eclipse testing feedback
- Minor version (1.5.X → 1.6.0): New features or significant changes
- Major version (1.X.X → 2.0.0): Major refactoring or breaking changes

**Example:** User tests v1.5.1 in Eclipse and reports an issue → Update to v1.5.2 before making fixes.

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
- If `"mode": "legacy"` → Execute Legacy Migration workflow (see below)
- If `"mode": "upgrade"` → Execute Framework Upgrade workflow (see below)
- If `"mode": "new"` → Execute New Project Setup workflow (see below)

After completing workflow, delete marker:
```bash
rm .claude/migration-context.json
```

If no migration context, continue to Step 0.1 (Crash Recovery).

---

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
Framework: Claude Code Starter v2.1
Ready to work!
```

---

## Completion Protocol

### 1. Build (if code changed)
```bash
npm run build
```

### 2. Update Metafiles
- `.claude/BACKLOG.md` — mark completed tasks `[x]`
- `.claude/SNAPSHOT.md` — update version and status
- `CHANGELOG.md` — add entry (if release)
- `.claude/ARCHITECTURE.md` — update if code structure changed

### 3. Git Commit
```bash
git add -A && git status
git commit -m "$(cat <<'EOF'
type: Brief description

🤖 Generated with [Claude Code](https://claude.com/claude-code)

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

## Slash Commands

**Core:** `/fi`, `/commit`, `/pr`
**Dev:** `/fix`, `/feature`, `/review`, `/test`, `/security`
**Quality:** `/explain`, `/refactor`, `/optimize`
**Database:** `/db-migrate`
**Installation:** `/migrate-legacy`, `/upgrade-framework`

## Key Principles

1. **Framework as AI Extension** — not just docs, but functionality
2. **Privacy by Default** — dialogs private in .gitignore
3. **Local Processing** — no external APIs
4. **Token Economy** — minimal context loading

## Framework Tools Usage Policy

**MANDATORY: Use Task tool with Explore agent for:**
- Exploring codebase structure and architecture
- Understanding how systems work (e.g., "how are validators registered?")
- Finding related files and patterns
- Answering questions requiring multiple file searches
- Any non-trivial search operations

**DO NOT:**
- Run Glob/Grep/Read directly for exploratory tasks
- Search manually when exploring unfamiliar code
- Skip the framework for "quick searches" — they're rarely quick

**Example:**
```
❌ BAD: User asks "how are validators registered?"
→ Agent runs Glob *.cs, Grep "validator", Read multiple files

✅ GOOD: User asks "how are validators registered?"
→ Agent uses Task tool with Explore agent
```

## Warnings

- DO NOT skip Crash Recovery check
- DO NOT commit without updating metafiles
- ALWAYS mark session clean at completion

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
   # (e.g., different thresholds, structure names)
   ```

3. Update metafiles:
   - `.claude/variants/clinicH.md` - Add to Porting Log
   - Note source ClinicE version
   - Update ClinicH version number if needed

4. Test ClinicH independently

5. Commit with clear message:
   ```bash
   git commit -m "port: HasSegment checks to ClinicH (from ClinicE v1.6.3)"
   ```

**When Bug Found in ClinicH:**

If bug discovered during ClinicH testing that likely affects ClinicE:

1. Fix in ClinicE first (since it's primary)
2. Test in ClinicE
3. Port fix back to ClinicH
4. Update both variant tracking files

**Core/ Infrastructure Changes:**

When Core/ infrastructure changes:

1. Both variants affected (since both reference Core/)
2. Build both variants to verify:
   ```bash
   # User builds manually - DO NOT execute msbuild
   # Just note what needs building
   ```
3. If ClinicH breaks, fix is required (can't ignore Core changes)
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
- Configuration notes (when refactored)

### Configuration System

**ClinicE (Configuration-Driven):**
- `Core/Base/IValidationConfig.cs` - Interface
- `Variants/ClinicE/Config/ClinicEConfig.cs` - Implementation
- Validators accept config via constructor injection
- Easy to change thresholds and exclusions

**ClinicH (Hardcoded - Pending Config):**
- Still uses hardcoded values
- Config refactor will be ported when explicitly requested
- Different thresholds expected (more conservative)

### File Paths

**ClinicE Files:**
- Project: `Variants/ClinicE/ROcheck.esapi.csproj`
- Entry: `Variants/ClinicE/Script.cs`
- Config: `Variants/ClinicE/Config/ClinicEConfig.cs`
- Validators: `Variants/ClinicE/Validators/*.cs`

**ClinicH Files:**
- Project: `Variants/ClinicH/ROcheck.esapi.csproj`
- Entry: `Variants/ClinicH/Script.cs`
- Validators: `Variants/ClinicH/Validators/*.cs`

**Core Files (Shared):**
- Base: `Core/Base/ValidatorBase.cs`, `Core/Base/IValidationConfig.cs`
- Models: `Core/Models/ValidationResult.cs`
- UI: `Core/UI/MainControl.xaml`, `Core/UI/SeverityToColorConverter.cs`
- Helpers: `Core/Helpers/ValidationHelpers.cs`

### TEST_ Prefix Procedure (Multi-Variant)

**ClinicE:**
1. `Variants/ClinicE/ROcheck.esapi.csproj` (line 11)
2. `Variants/ClinicE/Properties/AssemblyInfo.cs` (line 12)
3. `Variants/ClinicE/Script.cs` (line ~35)

**ClinicH:**
1. `Variants/ClinicH/ROcheck.esapi.csproj` (line 11)
2. `Variants/ClinicH/Properties/AssemblyInfo.cs` (line 12)
3. `Variants/ClinicH/Script.cs` (line ~35)

Update all three files in the respective variant.

---

## ESAPI Documentation

**Location:** `Documentation/` (organized by variant)

This project includes complete ESAPI API reference documentation for both Eclipse versions:

**XML IntelliSense Files (use with Read tool or Grep):**

ClinicE (Eclipse 18.0):
- `Documentation/ClinicE/VMS.TPS.Common.Model.API.xml` - Eclipse 18 API reference
- `Documentation/ClinicE/VMS.TPS.Common.Model.Types.xml` - Eclipse 18 type definitions

ClinicH (Eclipse 16.1):
- `Documentation/ClinicH/VMS.TPS.Common.Model.API.xml` - Eclipse 16 API reference
- `Documentation/ClinicH/VMS.TPS.Common.Model.Types.xml` - Eclipse 16 type definitions

**PDF Reference Guides (Shared):**
- `Documentation/Eclipse Scripting API Reference Guide 18.0.pdf` - Official ESAPI manual
- `Documentation/Image Registration and Segmentation Scripting API Reference Guide.pdf`
- `Documentation/VarianApiBook.pdf` - Comprehensive programming guide

**When to use:**
- Planning features - verify method availability and signatures
- Implementing code - check parameters, return types, null safety
- Explaining code - reference official documentation
- Troubleshooting - understand expected behavior
- **IMPORTANT:** Use correct variant documentation (ClinicE vs ClinicH)

**Example usage:**
```bash
# ClinicE (Eclipse 18) - Find Structure class documentation
grep -A 20 "T:VMS.TPS.Common.Model.API.Structure" Documentation/ClinicE/VMS.TPS.Common.Model.API.xml

# ClinicH (Eclipse 16) - Find specific method
grep -A 10 "M:VMS.TPS.Common.Model.API.Structure.OverlapsWith" Documentation/ClinicH/VMS.TPS.Common.Model.API.xml
```

See `.claude/ARCHITECTURE.md` for detailed usage guide.

---

## Legacy Migration Protocol

**Triggered when:** `.claude/migration-context.json` exists with `"mode": "legacy"`

**Purpose:** Analyze existing project and generate Framework files.

**Workflow:**

1. **Read migration context:**
   ```bash
   cat .claude/migration-context.json
   ```

2. **Execute `/migrate-legacy` command:**
   - Follow instructions in `.claude/commands/migrate-legacy.md`
   - Discovery → Deep Analysis → Questions → Report → Generate Files

3. **After completion:**
   - Verify all Framework files created
   - Delete migration marker:
     ```bash
     rm .claude/migration-context.json
     ```
   - Show success summary

4. **Next session:**
   - Use normal Cold Start Protocol

---

## Framework Upgrade Protocol

**Triggered when:** `.claude/migration-context.json` exists with `"mode": "upgrade"`

**Purpose:** Migrate from old Framework version to v2.1.

**Workflow:**

1. **Read migration context:**
   ```bash
   cat .claude/migration-context.json
   ```
   Extract `old_version` field.

2. **Execute `/upgrade-framework` command:**
   - Follow instructions in `.claude/commands/upgrade-framework.md`
   - Detect Version → Migration Plan → Backup → Execute → Verify

3. **After completion:**
   - Verify migration successful
   - Delete migration marker:
     ```bash
     rm .claude/migration-context.json
     ```
   - Show success summary

4. **Next session:**
   - Use normal Cold Start Protocol with new structure

---

## New Project Setup Protocol

**Triggered when:** `.claude/migration-context.json` exists with `"mode": "new"`

**Purpose:** Verify Framework installation and welcome user.

**Workflow:**

1. **Show welcome message:**
   ```
   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   ✅ Installation complete!
   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

   📁 Framework Files Created:

     ✅ .claude/SNAPSHOT.md
     ✅ .claude/BACKLOG.md
     ✅ .claude/ROADMAP.md
     ✅ .claude/ARCHITECTURE.md
     ✅ .claude/IDEAS.md

   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

   🚀 Next Step:

     Type "start" to launch the framework.

   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   ```

2. **Delete migration marker:**
   ```bash
   rm .claude/migration-context.json
   ```

3. **Next session:**
   - Use normal Cold Start Protocol

---
*Framework: Claude Code Starter v2.1*
