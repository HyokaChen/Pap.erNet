using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Pap.erNet.Utils;

/// <summary>
/// Tab 选中状态转颜色转换器
/// true = 白色 (#FFFFFF), false = 灰色 (#A0A0A0)
/// </summary>
public class BoolToTabBrushConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var isSelected = value is true;
		return new SolidColorBrush(isSelected ? Color.FromArgb(255, 255, 255, 255) : Color.FromArgb(255, 160, 160, 160));
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}

/// <summary>
/// Tab 下划线选中状态转颜色转换器
/// true = 白色 (#FFFFFF), false = 透明
/// </summary>
public class BoolToUnderlineBrushConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var isSelected = value is true;
		return new SolidColorBrush(isSelected ? Color.FromArgb(255, 255, 255, 255) : Color.FromArgb(0, 255, 255, 255));
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
