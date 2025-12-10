---
description: Оптимизировать производительность кода
---

Помоги оптимизировать производительность ROcheck (C# ESAPI plugin).

**Цель:** Validation должна выполняться быстро (<5 сек для типичного плана)

## Области оптимизации

### 1. ESAPI Access Patterns

**Проблема:** ESAPI calls медленные, особенно Volume calculations

**Оптимизации:**
```csharp
// ❌ Плохо: Multiple calls к Volume
foreach (var structure in structures)
{
    if (structure.Volume < 5.0) { }  // Call 1
    if (structure.Volume > 10.0) { } // Call 2 - same property!
}

// ✅ Хорошо: Cache volume
foreach (var structure in structures)
{
    var volume = structure.Volume;  // Call once
    if (volume < 5.0) { }
    if (volume > 10.0) { }
}
```

### 2. Collection Operations

**LINQ Performance:**
```csharp
// ❌ Плохо: Multiple enumerations
var ptvs = structures.Where(s => s.Id.Contains("PTV"));
var count = ptvs.Count();  // Enumeration 1
var first = ptvs.First();  // Enumeration 2

// ✅ Хорошо: Single enumeration
var ptvs = structures.Where(s => s.Id.Contains("PTV")).ToList();
var count = ptvs.Count;
var first = ptvs[0];
```

**Filter Early:**
```csharp
// ❌ Плохо: Process all, then filter
var results = structures
    .Select(s => ExpensiveOperation(s))
    .Where(r => r.IsValid);

// ✅ Хорошо: Filter first
var results = structures
    .Where(s => s.IsRelevant)  // Cheap filter first
    .Select(s => ExpensiveOperation(s));
```

### 3. Spatial Overlap Checks

**Проблема:** `structure.OverlapWith()` очень медленный

**Оптимизация:**
```csharp
// ❌ Плохо: Check overlap for all combinations
foreach (var ptv in ptvs)
{
    foreach (var oar in oars)
    {
        if (ptv.OverlapsWith(oar)) { }  // Expensive!
    }
}

// ✅ Хорошо: Filter by dose first, then check overlap
var ptvsWithLowerGoals = ptvs
    .Where(p => HasLowerDoseGoal(p))  // Cheap filter
    .ToList();

var oarsWithDmax = oars
    .Where(o => HasDmaxGoal(o))  // Cheap filter
    .ToList();

// Now only check overlaps for filtered lists
foreach (var ptv in ptvsWithLowerGoals)
{
    var ptvLowerGoal = GetLowerGoal(ptv);

    foreach (var oar in oarsWithDmax)
    {
        var oarDmax = GetDmaxGoal(oar);

        // Check dose first (cheap)
        if (ptvLowerGoal > oarDmax)
        {
            // Only then check spatial overlap (expensive)
            if (ptv.OverlapsWith(oar))
            {
                // Report overlap
            }
        }
    }
}
```

### 4. String Operations

**Performance Tips:**
```csharp
// ❌ Плохо: Case-sensitive comparison
if (structureId.Contains("PTV"))

// ✅ Хорошо: Use StringComparison
if (structureId.Contains("PTV", StringComparison.OrdinalIgnoreCase))

// ❌ Плохо: String concatenation in loop
var message = "";
foreach (var item in items)
{
    message += item + ", ";  // Creates new string each time!
}

// ✅ Хорошо: StringBuilder
var sb = new StringBuilder();
foreach (var item in items)
{
    sb.Append(item).Append(", ");
}
var message = sb.ToString();
```

### 5. Memory Allocations

**Reduce allocations:**
```csharp
// ❌ Плохо: Create new list each time
public List<ValidationResult> Validate(ScriptContext context)
{
    var results = new List<ValidationResult>();
    // Add items...
    return results;
}

// ✅ Хорошо: Pre-allocate capacity if known
public List<ValidationResult> Validate(ScriptContext context)
{
    var results = new List<ValidationResult>(capacity: 20);  // Avoid resizing
    // Add items...
    return results;
}

// ✅ Еще лучше: Reuse collections where possible
private readonly List<ValidationResult> _resultsBuffer = new List<ValidationResult>();

public List<ValidationResult> Validate(ScriptContext context)
{
    _resultsBuffer.Clear();
    // Add items to _resultsBuffer...
    return new List<ValidationResult>(_resultsBuffer);  // Return copy
}
```

### 6. Clinical Goals Access

**Cache clinical goals:**
```csharp
// ❌ Плохо: Access multiple times
foreach (var structure in structures)
{
    var goals = GetClinicalGoals(plan, structure);  // Expensive call
    if (goals.Any()) { }
    var count = goals.Count();  // Call again!
}

// ✅ Хорошо: Cache once
var clinicalGoalsCache = new Dictionary<string, List<ClinicalGoal>>();

foreach (var structure in structures)
{
    if (!clinicalGoalsCache.ContainsKey(structure.Id))
    {
        clinicalGoalsCache[structure.Id] = GetClinicalGoals(plan, structure);
    }

    var goals = clinicalGoalsCache[structure.Id];
    if (goals.Any()) { }
    var count = goals.Count;
}
```

### 7. Conditional Logic

**Short-circuit evaluation:**
```csharp
// ❌ Плохо: Always evaluates both
if (ExpensiveCheck1() && ExpensiveCheck2()) { }

// ✅ Хорошо: Short-circuits if first fails
if (CheapCheck() && ExpensiveCheck()) { }

// ✅ Еще лучше: Return early
if (!CheapCheck())
    return results;

if (!ExpensiveCheck())
    return results;
```

### 8. UI Updates

**Batch UI updates:**
```csharp
// ❌ Плохо: Update UI for each item
foreach (var result in results)
{
    ObservableCollection.Add(result);  // UI update each time!
}

// ✅ Хорошо: Batch updates
var batch = new List<ValidationResult>();
foreach (var result in results)
{
    batch.Add(result);
}

// Single UI update
Application.Current.Dispatcher.Invoke(() =>
{
    foreach (var item in batch)
    {
        ObservableCollection.Add(item);
    }
});
```

## Performance Profiling

**Measure before optimizing:**

```csharp
using System.Diagnostics;

public List<ValidationResult> Validate(ScriptContext context)
{
    var sw = Stopwatch.StartNew();

    var results = new List<ValidationResult>();

    // Validation logic...

    sw.Stop();
    Debug.WriteLine($"Validation took: {sw.ElapsedMilliseconds}ms");

    return results;
}
```

**Profile specific operations:**
```csharp
var sw1 = Stopwatch.StartNew();
var structures = GetStructures();  // How long does this take?
sw1.Stop();
Debug.WriteLine($"GetStructures: {sw1.ElapsedMilliseconds}ms");

var sw2 = Stopwatch.StartNew();
var goals = GetClinicalGoals();  // How long does this take?
sw2.Stop();
Debug.WriteLine($"GetClinicalGoals: {sw2.ElapsedMilliseconds}ms");
```

## Optimization Checklist

- [ ] Cache ESAPI property access (Volume, Dose, etc)
- [ ] Filter collections early (before expensive operations)
- [ ] Use cheap checks before expensive checks (dose before overlap)
- [ ] Pre-allocate collection capacity where possible
- [ ] Use StringBuilder for string concatenation in loops
- [ ] Cache clinical goals lookup
- [ ] Batch UI updates
- [ ] Profile to find actual bottlenecks

## Performance Targets

- **Typical plan (20-30 structures):** <2 seconds
- **Medium plan (50-70 structures):** <5 seconds
- **Large plan (100+ structures):** <10 seconds

## Testing Performance

**Create performance test cases:**
1. Small plan (10 structures) - baseline
2. Medium plan (50 structures) - typical
3. Large plan (150 structures) - stress test

**Measure:**
- Total validation time
- Time per validator
- Memory usage

---

**После оптимизации:**
- Документируй улучшения
- Обнови ARCHITECTURE.md если изменилась структура
- Проверь что функциональность не изменилась
