using System;
using System.Windows.Data;
using System.Windows.Media;
using System.Globalization;

namespace PlanCrossCheck
{
    public class SeverityToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ValidationSeverity severity)
            {
                switch (severity)
                {
                    case ValidationSeverity.Info:
                        return new SolidColorBrush(Colors.Green);
                    case ValidationSeverity.Warning:
                        return new SolidColorBrush(Colors.Orange);
                    case ValidationSeverity.Error:
                        return new SolidColorBrush(Colors.Red);
                    default:
                        return new SolidColorBrush(Colors.Gray);
                }
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}