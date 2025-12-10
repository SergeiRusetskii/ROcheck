---
description: Провести аудит безопасности кода
---

Проведи детальный аудит безопасности для ROcheck (C# ESAPI medical device plugin).

**ВАЖНО:** ROcheck - это медицинское устройство для radiation therapy. Безопасность критична для безопасности пациентов!

## Security Audit Checklist

### 1. Patient Safety (Highest Priority)

**Validation Logic:**
- [ ] Все валидации консервативны (false positive лучше false negative)
- [ ] Критичные проблемы помечены как Error, не Warning
- [ ] Сообщения понятны клиницистам
- [ ] Нет пропущенных edge cases которые могут навредить пациенту

**Examples:**
```csharp
// ✅ Good: Conservative check
if (ptvVolume < 5.0)  // Clear threshold
{
    results.Add(new ValidationResult
    {
        Severity = ValidationSeverity.Error,  // Error, not Warning
        Message = "PTV volume < 5cc requires high resolution"
    });
}

// ❌ Bad: Too lenient
if (ptvVolume < 2.0)  // Too small threshold, might miss issues
{
    Severity = ValidationSeverity.Warning  // Should be Error
}
```

### 2. Null Safety & Exception Handling

**ESAPI Objects:**
- [ ] Все ESAPI объекты проверяются на null
- [ ] Try-catch блоки обрабатывают ESAPI exceptions
- [ ] Unhandled exceptions не crashes plugin
- [ ] Graceful degradation при missing data

**Examples:**
```csharp
// ✅ Good: Comprehensive null checks
public override List<ValidationResult> Validate(ScriptContext context)
{
    var results = new List<ValidationResult>();

    try
    {
        if (context?.PlanSetup == null)
        {
            return results;  // Graceful return
        }

        if (context.PlanSetup.StructureSet == null)
        {
            results.Add(new ValidationResult
            {
                Category = "System.Warning",
                Message = "No structure set available",
                Severity = ValidationSeverity.Warning
            });
            return results;
        }

        // Validation logic...
    }
    catch (Exception ex)
    {
        results.Add(new ValidationResult
        {
            Category = "System.Error",
            Message = $"Validation error: {ex.Message}",
            Severity = ValidationSeverity.Warning
        });
    }

    return results;
}

// ❌ Bad: No null checks, no exception handling
public override List<ValidationResult> Validate(ScriptContext context)
{
    var structures = context.PlanSetup.StructureSet.Structures;  // Crash if null!
    // ...
}
```

### 3. Threading & Concurrency

**ESAPI Thread Safety:**
- [ ] Все ESAPI вызовы в UI thread
- [ ] Используется Application.Current.Dispatcher где нужно
- [ ] Нет race conditions
- [ ] Нет deadlocks

**Examples:**
```csharp
// ✅ Good: UI thread for ESAPI
Application.Current.Dispatcher.Invoke(() =>
{
    var dose = plan.Dose;  // ESAPI call in UI thread
});

// ❌ Bad: ESAPI call in background thread
Task.Run(() =>
{
    var dose = plan.Dose;  // Will crash!
});
```

### 4. Input Validation

**User Input:**
- [ ] Нет direct user input (read-only validation tool)
- [ ] ESAPI data validated перед использованием
- [ ] String операции безопасны (no injection risks)

**ESAPI Data:**
```csharp
// ✅ Good: Validate ESAPI data
if (string.IsNullOrWhiteSpace(structure.Id))
{
    continue;  // Skip invalid structure
}

if (double.IsNaN(structure.Volume) || double.IsInfinity(structure.Volume))
{
    continue;  // Skip invalid volume
}

// ❌ Bad: Trust ESAPI data blindly
var message = $"Structure {structure.Id} volume: {structure.Volume}";  // Could be invalid
```

### 5. Information Disclosure

**Error Messages:**
- [ ] Ошибки не раскрывают sensitive patient data
- [ ] Ошибки не раскрывают system internals
- [ ] Stack traces не показываются пользователю
- [ ] Логирование безопасно (no PHI in logs)

**Examples:**
```csharp
// ✅ Good: Safe error message
catch (Exception ex)
{
    results.Add(new ValidationResult
    {
        Message = "Validation error occurred",  // Generic message
        Severity = ValidationSeverity.Warning
    });
    // Log full details internally if needed (not to user)
}

// ❌ Bad: Exposes internals
catch (Exception ex)
{
    results.Add(new ValidationResult
    {
        Message = $"Error: {ex.ToString()}",  // Full stack trace to user!
        Severity = ValidationSeverity.Error
    });
}
```

### 6. Code Injection Risks

**String Operations:**
- [ ] Нет dynamic code execution (eval, reflection на user data)
- [ ] Нет SQL injection risks (no database)
- [ ] Нет command injection risks
- [ ] String formatting безопасен

**Examples:**
```csharp
// ✅ Good: Safe string interpolation
var message = $"Structure {structureId} has issue";  // Safe

// ❌ Bad: Dynamic code (not applicable to ROcheck, but be aware)
// var code = GetCodeFromUser();
// var compiled = CSharpScript.EvaluateAsync(code);  // Never do this!
```

### 7. Memory Management

**Resource Cleanup:**
- [ ] ESAPI objects disposed properly
- [ ] No memory leaks в long-running validation
- [ ] Large collections cleared после use
- [ ] Event handlers unsubscribed

**Examples:**
```csharp
// ✅ Good: Clear large collections
var structures = new List<Structure>();
// Use structures...
structures.Clear();
structures = null;

// ❌ Bad: Keep references to large ESAPI objects
private List<Structure> _allStructures;  // Keeps references, memory leak
```

### 8. Dependencies & Libraries

**NuGet Packages:**
- [ ] Только trusted packages (ESAPI, WPF framework)
- [ ] Нет known vulnerabilities
- [ ] Минимум зависимостей
- [ ] Dependencies up to date

**Check:**
```bash
# Check for vulnerabilities (if using .NET 8+)
dotnet list package --vulnerable

# Check outdated packages
dotnet list package --outdated
```

### 9. Build & Deployment Security

**Build Process:**
- [ ] Code signing certificate (для production)
- [ ] Release builds используются для deployment
- [ ] Debug symbols не в production DLL
- [ ] No sensitive data в build output

**Configuration:**
```xml
<!-- ✅ Good: Release configuration -->
<Configuration>Release</Configuration>
<DebugType>none</DebugType>
<Optimize>true</Optimize>

<!-- ❌ Bad: Debug in production -->
<Configuration>Debug</Configuration>
<DebugType>full</DebugType>
```

### 10. HIPAA / PHI Compliance

**Patient Data:**
- [ ] Plugin не stores patient data
- [ ] Plugin не transmits patient data
- [ ] Plugin не logs patient identifiable info
- [ ] Validation results не contain PHI

**Safe Practices:**
```csharp
// ✅ Good: No PHI in messages
Message = $"Structure {structureId} missing clinical goals"  // Structure ID only

// ❌ Bad: PHI in messages
Message = $"Patient {patientName} has issue"  // Patient name is PHI!
```

### 11. Medical Device Standards

**IEC 62304 Considerations:**
- [ ] Software classification appropriate
- [ ] Risk analysis performed
- [ ] Validation documented
- [ ] Traceability maintained

**FDA Considerations (if applicable):**
- [ ] Device classification determined
- [ ] Quality system requirements followed
- [ ] Software validation documented

## Security Review Report Template

```markdown
# ROcheck Security Audit Report

## Date: [Date]
## Version: [v1.X.X]
## Auditor: Claude Code

## Executive Summary

[Overall security posture: Good/Needs Improvement/Critical Issues]

## Critical Findings

### 🔴 Critical (Fix Immediately)
- [ ] Finding 1: [Description]
  - **Impact:** [Patient safety/System crash/Data loss]
  - **Location:** [File:Line]
  - **Recommendation:** [How to fix]

### 🟠 High (Fix Before Release)
- [ ] Finding 2: [Description]
  - **Impact:** [Potential issue]
  - **Location:** [File:Line]
  - **Recommendation:** [How to fix]

### 🟡 Medium (Fix When Possible)
- [ ] Finding 3: [Description]

### ⚪ Low (Nice to Have)
- [ ] Finding 4: [Description]

## Positive Findings

✅ [List what's done well]

## Recommendations

1. [Priority recommendation]
2. [Priority recommendation]
3. [Priority recommendation]

## Conclusion

[Overall assessment and sign-off]
```

## Best Practices for Medical Device Security

1. **Conservative by Design**
   - Prefer false positives over false negatives
   - Clear, actionable error messages
   - Fail-safe defaults

2. **Robust Error Handling**
   - Comprehensive null checks
   - Try-catch all ESAPI interactions
   - Graceful degradation

3. **No Patient Data Storage**
   - Read-only operations
   - No logging of PHI
   - No data transmission

4. **Thread Safety**
   - All ESAPI calls in UI thread
   - No race conditions
   - Proper synchronization

5. **Minimal Attack Surface**
   - No user input
   - No network access
   - Minimal dependencies

---

**После аудита обнови:**
- `.claude/BACKLOG.md` - добавь найденные issues
- Security findings document
- Risk analysis if needed