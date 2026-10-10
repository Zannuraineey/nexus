using System.Globalization;
using System.Windows.Data;
using NexusDeviceService.Desktop.Services;

namespace NexusDeviceService.Desktop.Converters;

public sealed class NavigationTargetToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is NavigationTarget current && parameter is string paramStr && Enum.TryParse<NavigationTarget>(paramStr, out var expected))
        {
            return current == expected;
        }

        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string paramStr && Enum.TryParse<NavigationTarget>(paramStr, out var target))
        {
            return target;
        }

        return Binding.DoNothing;
    }
}
