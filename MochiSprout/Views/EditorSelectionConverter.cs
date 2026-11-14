using System.Globalization;
using System.Windows.Data;
namespace MochiSprout.Views;
public sealed class EditorSelectionConverter : IValueConverter
{
 public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>string.Equals(value?.ToString(),parameter?.ToString(),StringComparison.OrdinalIgnoreCase);
 public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
}
