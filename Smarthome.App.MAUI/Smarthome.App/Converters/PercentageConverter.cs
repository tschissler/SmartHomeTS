using Microsoft.Maui.Controls;
using System;
using System.Globalization;
using System.Reflection;

namespace Smarthome.App.Converters
{
    public class PercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width)
            {
                if (parameter is string percentageString)
                {
                    if (Double.TryParse(percentageString, culture, out var result))
                        return width * result / 100;
                    else
                        return 20;
                }
                if (parameter is double percentage)
                {
                    return width * percentage / 100;
                }
                if (parameter is Binding binding && binding.Source is not null)
                {
                    var source = binding.Source;
                    var propertyName = binding.Path;
                    var propertyInfo = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                    if (propertyInfo != null)
                    {
                        var percentageValue = (double)propertyInfo.GetValue(source);
                        return width * percentageValue / 100;
                    }
                }
            }
            return 20;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
