---
description: Помочь написать тесты для кода
---

Помоги написать тесты для ROcheck (C# ESAPI plugin).

**ВАЖНО:** ESAPI plugins сложно тестировать автоматически из-за зависимости от Eclipse окружения. Фокус на manual testing и test scenarios.

## Стратегия тестирования

### 1. Manual Testing в Eclipse (основной подход)

**Подготовка тестовых планов:**
- Создай тестовые планы с известными проблемами
- Документируй ожидаемые результаты
- Сохраняй тестовые конфигурации

**Тестовые сценарии:**

#### Structure Coverage Tests
- [ ] План с targets без clinical goals → Error
- [ ] План со всеми clinical goals → Info/Pass
- [ ] План с non-prescription targets → должны быть excluded
- [ ] План с SUPPORT structures → должны быть excluded

#### Target Containment Tests
- [ ] GTV полностью внутри PTV → Pass
- [ ] GTV выходит за PTV → Error
- [ ] CTV выходит за PTV → Error
- [ ] Нет GTV/CTV → Pass (nothing to check)

#### PTV-OAR Overlap Tests
- [ ] PTV с lower goal overlapping OAR с Dmax → Warning
- [ ] PTV lower goal > OAR Dmax → Error
- [ ] No overlap → Pass
- [ ] PTV без clinical goals → Pass (nothing to check)

#### Resolution Tests
- [ ] PTV <5cc без high resolution → Error
- [ ] PTV 5-10cc без high resolution → Warning
- [ ] PTV >10cc → Pass
- [ ] PTV <5cc с high resolution → Pass

#### Structure Types Tests
- [ ] PTV с неправильным типом → Warning
- [ ] CTV с неправильным типом → Warning
- [ ] GTV с неправильным типом → Warning

### 2. Unit Testing (если создаешь test project)

**Настройка:**
```bash
# Создать test project
dotnet new nunit -n ROcheck.Tests
cd ROcheck.Tests
dotnet add package NUnit
dotnet add package Moq
dotnet add reference ../ROcheck/ROcheck.csproj
```

**Пример test structure:**
```csharp
using NUnit.Framework;
using VMS.TPS.Common.Model.API;

namespace ROcheck.Tests
{
    [TestFixture]
    public class ClinicalGoalsValidatorTests
    {
        [Test]
        public void Validate_PlanWithoutClinicalGoals_ReturnsError()
        {
            // Arrange
            // Note: Mocking ESAPI objects is complex
            // Better to use integration tests in Eclipse

            // Act
            var results = validator.Validate(context);

            // Assert
            Assert.That(results.Any(r => r.Severity == ValidationSeverity.Error));
        }

        [Test]
        public void Validate_NullPlan_ReturnsEmptyResults()
        {
            // Arrange
            var context = new ScriptContext { PlanSetup = null };

            // Act
            var results = validator.Validate(context);

            // Assert
            Assert.IsEmpty(results);
        }
    }
}
```

**Проблемы с ESAPI unit testing:**
- ESAPI объекты сложно мокировать
- Требуется Eclipse environment
- Лучше использовать integration testing в Eclipse

### 3. Integration Testing в Eclipse

**Test Plan Document:**

Создай документ с тестовыми сценариями:

```markdown
# ROcheck Test Plan v1.2.0

## Test Cases

### TC-001: Clinical Goals Coverage
**Objective:** Verify clinical goals detection
**Steps:**
1. Open plan "Test_NoClinicalGoals"
2. Run ROcheck
**Expected:** Error for PTV1 without clinical goals
**Actual:** [заполнить после теста]
**Status:** [Pass/Fail]

### TC-002: Target Containment
**Objective:** Verify GTV containment check
**Steps:**
1. Open plan "Test_GTVOutsidePTV"
2. Run ROcheck
**Expected:** Error "GTV1 extends beyond PTV1"
**Actual:** [заполнить после теста]
**Status:** [Pass/Fail]

[... more test cases ...]
```

### 4. Edge Cases Testing

**Обязательно проверь:**
- [ ] Plan = null
- [ ] StructureSet = null
- [ ] No structures in plan
- [ ] Clinical goals = null или empty
- [ ] Very large plans (100+ structures)
- [ ] Plans с Russian characters in names
- [ ] Plans с special characters
- [ ] Empty structure volumes
- [ ] Prescription = null

### 5. Performance Testing

**Metrics:**
- Validation должна завершаться <5 секунд для типичного плана
- <10 секунд для large plan (100+ structures)

**Test:**
```csharp
var stopwatch = Stopwatch.StartNew();
var results = validator.Validate(context);
stopwatch.Stop();

Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000),
    "Validation took too long");
```

### 6. UI Testing

**Проверь:**
- [ ] Results отображаются корректно
- [ ] Группировка по categories работает
- [ ] Color coding (Red/Orange/Green) правильный
- [ ] Scrolling работает для длинного списка
- [ ] Window resizing работает
- [ ] UI не freezes во время validation

### 7. Regression Testing

**После каждого изменения проверь:**
- [ ] Все previous test cases still pass
- [ ] No new bugs introduced
- [ ] Performance не ухудшилась
- [ ] UI still works correctly

## Testing Checklist

### Before Release:
- [ ] All manual test scenarios passed
- [ ] Edge cases handled gracefully
- [ ] Performance acceptable
- [ ] UI works correctly
- [ ] No unhandled exceptions
- [ ] Clear error messages
- [ ] Documentation updated

### Test Data Preparation:
- [ ] Create plan without clinical goals
- [ ] Create plan with GTV outside PTV
- [ ] Create plan with small PTVs
- [ ] Create plan with PTV-OAR overlaps
- [ ] Create plan with incorrect structure types
- [ ] Save test plans for future regression testing

## Automated Testing (Future)

**If you want to add automated tests:**

1. **Create test data generator:**
   - Script to create test DICOM RT files
   - Predefined scenarios

2. **Use ESAPI in standalone mode:**
   - Some ESAPI testing possible outside Eclipse
   - Limited but better than nothing

3. **CI/CD integration:**
   - Build validation
   - Static analysis (FxCop, StyleCop)
   - No functional tests without Eclipse

## Best Practices

- **Test in production-like environment** (Eclipse with real plans)
- **Document test scenarios** for regression testing
- **Keep test plans** for future versions
- **Test edge cases** thoroughly
- **Verify error messages** are clear for clinicians
- **Check performance** on large plans
- **Test null safety** extensively

---

См. `.claude/ARCHITECTURE.md` для деталей об архитектуре testing strategy.
