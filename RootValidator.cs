using ROcheck.Validators;

namespace ROcheck
{
    /// <summary>
    /// Root validator that coordinates all validation checks.
    /// Entry point for the validation system.
    /// </summary>
    public class RootValidator : CompositeValidator
    {
        public RootValidator()
        {
            // Clinical goals and structure validation
            AddValidator(new ClinicalGoalsCoverageValidator());

            // Structure geometry validation
            AddValidator(new TargetContainmentValidator());
            AddValidator(new TargetOAROverlapValidator());
            AddValidator(new PTVBodyProximityValidator());

            // Structure properties validation
            AddValidator(new TargetResolutionValidator());
            AddValidator(new StructureTypesValidator());

            // Dose validation
            AddValidator(new SIBDoseUnitsValidator());
        }
    }
}
