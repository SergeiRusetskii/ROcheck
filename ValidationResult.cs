namespace ROcheck
{
    public enum ValidationSeverity
    {
        Error,
        Warning,
        Info
    }

    public class ValidationResult
    {
        public string Category { get; set; }
        public string Message { get; set; }
        public ValidationSeverity Severity { get; set; }
        public bool IsFieldResult { get; set; }

        // Treat result as valid when it is not an error
        public bool IsValid => Severity != ValidationSeverity.Error;
    }
}
