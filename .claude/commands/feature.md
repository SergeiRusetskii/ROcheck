---
description: Помочь спланировать и реализовать новую фичу
---

Помоги мне спланировать и реализовать новую функцию в ROcheck (C# ESAPI plugin).

**Шаги:**

## 1. Анализ требований

- Что именно нужно реализовать?
- Какие есть edge cases?
- Как это вписывается в существующую архитектуру валидаторов?
- Какие данные из ESAPI API нужны?

## 2. Планирование

**Определи компоненты:**
- Нужен ли новый валидатор класс?
- Изменения в существующих валидаторах?
- Нужны ли изменения в ValidationViewModel?
- Требуются ли изменения UI (MainControl.xaml)?
- Какие ESAPI объекты использовать (Structure, PlanSetup, Dose, etc)?

**Архитектурные вопросы:**
- Какой ValidationCategory использовать?
- Какие Severity levels (Error/Warning/Info)?
- Нужна ли интеграция с другими валидаторами?

## 3. Безопасность и Надежность

**ESAPI threading:**
- Все обращения к ESAPI должны быть в UI thread
- Использовать Application.Current.Dispatcher где нужно

**Null safety:**
- Проверяй все ESAPI объекты на null
- Обрабатывай случаи отсутствующих данных
- Try-catch для ESAPI исключений

**Medical safety:**
- Валидации должны повышать безопасность пациента
- Ложные срабатывания лучше пропущенных проблем
- Понятные сообщения для клиницистов

## 4. Разбивка на задачи

Разбей реализацию на шаги:
1. Создать новый validator class (или изменить существующий)
2. Реализовать логику валидации
3. Добавить в RootValidator
4. Обновить ValidationViewModel если нужно
5. Обновить UI если нужно
6. Добавить XML documentation
7. Обновить версию в AssemblyInfo.cs
8. Тестирование в Eclipse

## 5. Реализация

**Coding standards:**
- Наследуй от `ValidatorBase` или `CompositeValidator`
- Используй composite pattern для сложных валидаторов
- Добавляй XML documentation (///)
- Понятные имена: `SomethingValidator`
- Категории: "ClinicalGoals.XXX" или новая категория

**Пример структуры validator:**
```csharp
/// <summary>
/// Validates [описание]
/// </summary>
public class MyValidator : ValidatorBase
{
    public override List<ValidationResult> Validate(ScriptContext context)
    {
        var results = new List<ValidationResult>();

        try
        {
            // Null checks
            if (context?.PlanSetup == null)
                return results;

            // Validation logic
            // ...

            // Add results
            results.Add(new ValidationResult
            {
                Category = "CategoryName.SubCategory",
                Message = "Clear message for clinician",
                Severity = ValidationSeverity.Error
            });
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
}
```

## 6. Тестирование

**Тест в Eclipse:**
- Загрузи различные планы
- Проверь edge cases
- Проверь null safety
- Проверь performance (для больших планов)
- Проверь UI отображение результатов

**Тестовые сценарии:**
- План с проблемой (должен показать Error/Warning)
- План без проблемы (должен показать Info или ничего)
- План с отсутствующими данными (должен обработать gracefully)
- Большой план (должен работать быстро)

## 7. Документация

**Обнови:**
- `.claude/ARCHITECTURE.md` - добавь описание нового валидатора
- `.claude/ROADMAP.md` - отметь фичу как реализованную
- `Properties/AssemblyInfo.cs` - обновь версию (v1.X.X)
- `Script.cs` - обновь window title с новой версией

**Опционально создай:**
- CHANGELOG.md запись о новой версии

## Принципы

- **Следуй архитектуре** (.claude/ARCHITECTURE.md)
- **Composite pattern** для валидаторов
- **Medical safety first** - понятные сообщения, консервативные проверки
- **Null-safe** - проверяй все ESAPI объекты
- **Performance** - оптимизируй для больших планов
- **ESAPI thread-safe** - используй UI thread для ESAPI вызовов

---

После реализации используй `/commit` для создания коммита и `/fi` для обновления документации.
