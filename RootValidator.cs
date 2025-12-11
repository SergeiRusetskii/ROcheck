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
            AddValidator(new ClinicalGoalsValidator());
        }
    }
}
