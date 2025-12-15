using System.Collections.Generic;
using VMS.TPS.Common.Model.API;

namespace ROcheck.Validators
{
    /// <summary>
    /// Base validator class for all validators in the system.
    /// </summary>
    public abstract class ValidatorBase
    {
        public abstract IEnumerable<ValidationResult> Validate(ScriptContext context);

        protected ValidationResult CreateResult(string category, string message, ValidationSeverity severity, bool isFieldResult = false)
        {
            return new ValidationResult
            {
                Category = category,
                Message = message,
                Severity = severity,
                IsFieldResult = isFieldResult
            };
        }
    }

    /// <summary>
    /// Composite validator that can contain child validators.
    /// Implements the Composite pattern for hierarchical validation.
    /// </summary>
    public abstract class CompositeValidator : ValidatorBase
    {
        protected List<ValidatorBase> Validators { get; } = new List<ValidatorBase>();

        public void AddValidator(ValidatorBase validator)
        {
            Validators.Add(validator);
        }

        public override IEnumerable<ValidationResult> Validate(ScriptContext context)
        {
            var results = new List<ValidationResult>();
            foreach (var validator in Validators)
            {
                results.AddRange(validator.Validate(context));
            }
            return results;
        }
    }
}
