using System;
using System.Globalization;
using System.Windows.Data;

namespace PlayniteDlssgPlugin
{
    public class WidthToItemWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double)
            {
                double actualWidth = (double)value;
                if (actualWidth > 0)
                {
                    double availableWidth = actualWidth - 24;
                    
                    if (availableWidth < 300)
                    {
                        return Math.Max(260, availableWidth - 8);
                    }
                    
                    int columns;
                    if (availableWidth >= 900)
                        columns = 3;
                    else if (availableWidth >= 580)
                        columns = 2;
                    else
                        columns = 1;
                    
                    double itemWidth = (availableWidth - (columns - 1) * 8) / columns;
                    
                    return Math.Max(260, Math.Min(600, itemWidth));
                }
            }
            return 280.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
