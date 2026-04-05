using System.Globalization;
using System.Windows;
using System.Windows.Data;
using NewDialer.Desktop.Models;

namespace NewDialer.Desktop.Converters;

public sealed class SectionToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ShellSection section && parameter is string parameterText)
        {
            return string.Equals(section.ToString(), parameterText, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
