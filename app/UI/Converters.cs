using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;

namespace VdlParser;

[ValueConversion(typeof(object), typeof(bool))]
public class ObjectToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => 
        value != null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => 
        (bool)value;
}

[ValueConversion(typeof(string), typeof(bool))]
public class StringToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        !string.IsNullOrEmpty((string)value);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        "";
}

[ValueConversion(typeof(bool), typeof(Visibility))]
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isInversed = (bool?)parameter == true;
        return (bool)value ?
            (isInversed ? Visibility.Collapsed : Visibility.Visible) :
            (isInversed ? Visibility.Visible : Visibility.Collapsed);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => 
        (Visibility)value == Visibility.Visible;
}

[ValueConversion(typeof(bool), typeof(bool))]
public class NegateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (bool)value == false;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        (bool)value == false;
}

[ValueConversion(typeof(string), typeof(string))]
public class PathUIConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrEmpty((string)value) ? "[not selected yet]" : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value;
}

public class FriendlyEnumConverter(Type type) : EnumConverter(type)
{
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string))
        {
            return value == null ? string.Empty :
                Regex.Replace(
                        value.ToString() ?? "",
                        "([A-Z])", " $1",
                        RegexOptions.Compiled
                    ).Trim();
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
