using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Yell
{
    /// <summary>
    /// 工业日志方向颜色转换器：将 TX/RX 字符串转换为对应的 UI 颜色
    /// </summary>
    public class DirectionToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string direction = value as string;

            return direction switch
            {
                "TX" => new SolidColorBrush(Color.FromRgb(0, 122, 204)),  // 工业蓝
                "RX" => new SolidColorBrush(Color.FromRgb(40, 167, 69)),  // 成功绿
                "系统" => Brushes.Gray,                                    // 系统灰
                "错误" => Brushes.Red,                                     // 警告红
                _ => Brushes.Black                                        // 默认黑
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // 这种单向转换不需要实现 ConvertBack
        }
    }
}