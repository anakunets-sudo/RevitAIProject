using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RevitAIProject.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        // Превращает bool в Visibility (для показа ProgressBar или текста "ИИ думает...")
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool bValue)
            {
                // Если параметр "Inverse", то логика меняется на противоположную
                if (parameter?.ToString() == "Inverse")
                    return bValue ? Visibility.Collapsed : Visibility.Visible;

                return bValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        // Обратное преобразование (обычно не требуется для UI)
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility vis && vis == Visibility.Visible;
        }
    }
}
