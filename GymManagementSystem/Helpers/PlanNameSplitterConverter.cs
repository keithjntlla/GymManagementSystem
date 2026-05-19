using System;
using System.Globalization;
using System.Windows.Data;

namespace GymManagementSystem
{
    public class PlanNameSplitterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string input && input.Contains("|"))
            {
                string[] segments = input.Split('|');
                int targetIndex = System.Convert.ToInt32(parameter);
                if (targetIndex < segments.Length)
                {
                    return segments[targetIndex];
                }
            }
            return parameter?.ToString() == "0" ? (value ?? "-") : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StringToCollectionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string input && !string.IsNullOrWhiteSpace(input) && input != "-")
            {
                // FIXED: Splits separate stacked items by comma (e.g., "Daily|x2,Weekly|")
                return input.Split(',');
            }
            return new string[] { "-" };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}