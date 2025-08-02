// UI/Converters/BoolToColorConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace UI
{
    public class BoolToColorConverter : IValueConverter
    {
        public static readonly BoolToColorConverter Instance = new BoolToColorConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isConnected)
            {
                return isConnected ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}