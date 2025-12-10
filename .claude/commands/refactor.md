---
description: Помочь с рефакторингом кода
---

Помоги отрефакторить код ROcheck (C# ESAPI plugin), сохраняя функциональность и улучшая качество.

## Цели рефакторинга

### 1. Улучшение читаемости

**Понятные имена:**
```csharp
// ❌ Плохо
var s = GetStr();
var p = ctx.PS;

// ✅ Хорошо
var structure = GetStructure();
var planSetup = context.PlanSetup;
```

**Разбивка больших методов:**
```csharp
// ❌ Плохо: 200+ lines method
public List<ValidationResult> Validate(ScriptContext context)
{
    // 200 lines of validation logic...
}

// ✅ Хорошо: Split into focused methods
public List<ValidationResult> Validate(ScriptContext context)
{
    var results = new List<ValidationResult>();

    results.AddRange(ValidateStructureCoverage(context));
    results.AddRange(ValidateTargetContainment(context));
    results.AddRange(ValidatePTVOAROverlap(context));
    results.AddRange(ValidateResolution(context));

    return results;
}

private List<ValidationResult> ValidateStructureCoverage(ScriptContext context)
{
    // Focused validation logic
}
```

**Удаление дублирования:**
```csharp
// ❌ Плохо: Duplicated logic
if (structure.Id.Contains("PTV") || structure.Id.Contains("ptv"))
{
    // ...
}

if (structure.Id.Contains("CTV") || structure.Id.Contains("ctv"))
{
    // ...
}

// ✅ Хорошо: Extract helper method
private bool IsTargetStructure(Structure structure, string targetType)
{
    return structure.Id.Contains(targetType, StringComparison.OrdinalIgnoreCase);
}

if (IsTargetStructure(structure, "PTV")) { }
if (IsTargetStructure(structure, "CTV")) { }
```

### 2. Применение паттернов

**Composite Pattern (уже используется):**
```csharp
// ✅ Хорошо: Hierarchical validators
public class RootValidator : CompositeValidator
{
    public RootValidator()
    {
        AddValidator(new ClinicalGoalsValidator());
        // Add more validators...
    }
}
```

**Strategy Pattern для различных проверок:**
```csharp
// Пример: Different strategies for different structure types
public interface IStructureValidator
{
    bool CanValidate(Structure structure);
    ValidationResult Validate(Structure structure);
}

public class PTVValidator : IStructureValidator
{
    public bool CanValidate(Structure structure) =>
        structure.Id.Contains("PTV", StringComparison.OrdinalIgnoreCase);

    public ValidationResult Validate(Structure structure)
    {
        // PTV-specific validation
    }
}
```

**Null Object Pattern:**
```csharp
// ❌ Плохо: Null checks everywhere
if (clinicalGoals != null && clinicalGoals.Any())
{
    // Process goals
}

// ✅ Хорошо: Null object
public static class ClinicalGoalsExtensions
{
    public static IEnumerable<ClinicalGoal> OrEmpty(this IEnumerable<ClinicalGoal> goals)
    {
        return goals ?? Enumerable.Empty<ClinicalGoal>();
    }
}

foreach (var goal in clinicalGoals.OrEmpty())
{
    // Process goals - no null check needed
}
```

### 3. SOLID Principles

**Single Responsibility:**
```csharp
// ❌ Плохо: Class does too much
public class ClinicalGoalsValidator
{
    public List<ValidationResult> Validate() { }
    public string FormatMessage() { }
    public void SaveToFile() { }  // Wrong responsibility!
}

// ✅ Хорошо: Each class has one responsibility
public class ClinicalGoalsValidator
{
    public List<ValidationResult> Validate() { }
}

public class ValidationResultFormatter
{
    public string Format(List<ValidationResult> results) { }
}
```

**Dependency Inversion:**
```csharp
// ❌ Плохо: Direct dependency on concrete class
public class MyValidator
{
    private SpecificHelper helper = new SpecificHelper();
}

// ✅ Хорошо: Depend on abstraction
public class MyValidator
{
    private readonly IHelper _helper;

    public MyValidator(IHelper helper)
    {
        _helper = helper;
    }
}
```

### 4. Улучшение типобезопасности

**Strongly typed categories:**
```csharp
// ❌ Плохо: Magic strings
Category = "ClinicalGoals.Structures"

// ✅ Хорошо: Constants or enum
public static class ValidationCategories
{
    public const string StructureCoverage = "ClinicalGoals.Structures";
    public const string TargetContainment = "ClinicalGoals.TargetContainment";
    // ...
}

Category = ValidationCategories.StructureCoverage
```

**Strongly typed messages:**
```csharp
// ❌ Плохо: Inconsistent messages
Message = $"Structure {id} missing goal"
Message = $"{id} has no clinical goal"  // Inconsistent!

// ✅ Хорошо: Message builder class
public static class ValidationMessages
{
    public static string MissingClinicalGoal(string structureId) =>
        $"Structure '{structureId}' missing clinical goals";

    public static string GTVOutsidePTV(string gtvId, string ptvId) =>
        $"{gtvId} extends beyond {ptvId}";
}

Message = ValidationMessages.MissingClinicalGoal(structure.Id)
```

### 5. Улучшение error handling

**Specific exceptions:**
```csharp
// ❌ Плохо: Generic exception
throw new Exception("Invalid structure");

// ✅ Хорошо: Specific exception
public class ESAPIException : Exception
{
    public ESAPIException(string message) : base(message) { }
}

throw new ESAPIException($"Structure '{id}' not found");
```

**Try-Parse pattern:**
```csharp
// ❌ Плохо: Exception for control flow
try
{
    var dose = double.Parse(doseString);
}
catch
{
    dose = 0;  // Bad practice
}

// ✅ Хорошо: TryParse
if (double.TryParse(doseString, out var dose))
{
    // Use dose
}
else
{
    // Handle invalid input
}
```

### 6. Код-качество

**XML Documentation:**
```csharp
/// <summary>
/// Validates clinical goals coverage for applicable structures.
/// </summary>
/// <param name="context">The script context containing plan data.</param>
/// <returns>List of validation results.</returns>
/// <remarks>
/// Only validates structures in the prescription. Excludes SUPPORT structures
/// and structures containing 'wire', 'Encompass', 'Enc', 'Dose'.
/// </remarks>
public override List<ValidationResult> Validate(ScriptContext context)
{
    // Implementation
}
```

**Const instead of magic numbers:**
```csharp
// ❌ Плохо: Magic numbers
if (volume < 5.0)  // What does 5.0 mean?

// ✅ Хорошо: Named constants
private const double SmallVolumeThreshold = 5.0;  // cubic centimeters
private const double MediumVolumeThreshold = 10.0;

if (volume < SmallVolumeThreshold)
```

### 7. LINQ Refactoring

**Readable LINQ:**
```csharp
// ❌ Плохо: Complex one-liner
var result = structures.Where(s => !string.IsNullOrWhiteSpace(s.Id) && s.Volume > 0 && !excludedIds.Contains(s.Id)).Select(s => new { s.Id, s.Volume }).ToList();

// ✅ Хорошо: Readable multi-line
var validStructures = structures
    .Where(s => !string.IsNullOrWhiteSpace(s.Id))
    .Where(s => s.Volume > 0)
    .Where(s => !excludedIds.Contains(s.Id))
    .Select(s => new
    {
        s.Id,
        s.Volume
    })
    .ToList();
```

## Процесс рефакторинга

### 1. Анализ
- Найди code smells (дублирование, длинные методы, magic numbers)
- Определи что можно улучшить
- Приоритизируй изменения

### 2. План
- Определи шаги рефакторинга
- Начни с малого
- Проверяй после каждого шага

### 3. Рефакторинг
- Делай одно изменение за раз
- Компилируй после каждого изменения
- Тестируй что функциональность сохранена

### 4. Проверка
- Build успешен
- Тесты проходят (manual testing в Eclipse)
- Функциональность не изменилась

## Refactoring Checklist

### Code Quality
- [ ] Нет magic numbers/strings (используй константы)
- [ ] Нет дублирования кода
- [ ] Методы короткие и focused (<50 lines)
- [ ] Понятные имена переменных и методов
- [ ] XML documentation для публичных методов

### Patterns
- [ ] Composite pattern для validators
- [ ] Null object pattern где уместно
- [ ] Strategy pattern для различных типов проверок

### SOLID
- [ ] Single responsibility - каждый класс делает одно
- [ ] Open/closed - расширяемо без модификации
- [ ] Dependency inversion - зависимости на абстракциях

### Error Handling
- [ ] Try-catch вокруг ESAPI calls
- [ ] Null checks для ESAPI objects
- [ ] Graceful degradation при errors
- [ ] Понятные error messages

### Performance
- [ ] Нет unnecessary ESAPI calls
- [ ] Кэширование где уместно
- [ ] Efficient collections usage

## Common Refactorings

### Extract Method
```csharp
// Before: Long method
public void ProcessStructure()
{
    // 20 lines of validation logic
    // 20 lines of formatting
    // 20 lines of result creation
}

// After: Extracted methods
public void ProcessStructure()
{
    var isValid = ValidateStructure();
    var message = FormatMessage(isValid);
    var result = CreateResult(message);
}
```

### Extract Class
```csharp
// Before: God class
public class ClinicalGoalsValidator
{
    // Too many responsibilities
}

// After: Separate classes
public class StructureCoverageValidator { }
public class TargetContainmentValidator { }
public class PTVOAROverlapValidator { }
```

### Introduce Parameter Object
```csharp
// Before: Long parameter list
public ValidationResult Validate(
    Structure structure,
    double volume,
    string id,
    bool hasGoals,
    List<string> excluded)

// After: Parameter object
public class ValidationContext
{
    public Structure Structure { get; set; }
    public double Volume { get; set; }
    public string Id { get; set; }
    public bool HasGoals { get; set; }
    public List<string> ExcludedIds { get; set; }
}

public ValidationResult Validate(ValidationContext context)
```

---

**После рефакторинга:**
- Обнови ARCHITECTURE.md если структура изменилась
- Проверь что все тесты проходят
- Документируй крупные изменения
- Создай commit с описанием рефакторинга
