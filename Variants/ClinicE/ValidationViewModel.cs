using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using VMS.TPS.Common.Model.API;

namespace ROcheck
{
    public class ValidationViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ValidationResult> ValidationResults { get; } = new ObservableCollection<ValidationResult>();

        public ValidationViewModel(ScriptContext context, IValidationConfig config)
        {
            // Create and use the root validator
            var rootValidator = new RootValidator(config);
            var results = rootValidator.Validate(context);

            // Post-process results: if a validator produced a result per field and
            // every field passed (Info severity), collapse these into a single
            // summary result so the UI only shows the total result.
            var processedResults = results
                .GroupBy(r => r.Category)
                .SelectMany<IGrouping<string, ValidationResult>, ValidationResult>(group =>
                {
                    var groupList = group.ToList();
                    bool allPass = groupList.All(r => r.Severity == ValidationSeverity.Info);
                    bool allFieldResults = groupList.All(r => r.IsFieldResult);
                    bool hasMultipleResults = groupList.Count > 1;

                    if (allPass && allFieldResults && hasMultipleResults)
                    {
                        return new[]
                        {
                            new ValidationResult
                            {
                                Category = group.Key,
                                Severity = ValidationSeverity.Info,
                                Message = $"All treatment fields passed {group.Key} checks"
                            }
                        };
                    }

                    return groupList;
                });

            foreach (var result in processedResults)
            {
                ValidationResults.Add(result);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}
