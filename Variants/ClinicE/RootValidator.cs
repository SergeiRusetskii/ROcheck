using ROcheck.Validators;

namespace ROcheck
{
    /// <summary>
    /// Root validator that coordinates all validation checks.
    /// Entry point for the validation system.
    /// </summary>
    public class RootValidator : CompositeValidator
    {
        public RootValidator(IValidationConfig config)
        {
            // Clinical goals and structure validation
            AddValidator(new ClinicalGoalsCoverageValidator(config));

            // Structure geometry validation
            AddValidator(new TargetContainmentValidator());
            AddValidator(new TargetOAROverlapValidator());
            AddValidator(new PTVBodyProximityValidator(config));

            // Structure properties validation
            AddValidator(new TargetResolutionValidator(config));
            AddValidator(new StructureTypesValidator());

            // Dose validation
            AddValidator(new SIBDoseUnitsValidator(config));
        }
    }
}
