using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MochiSprout.Controllers;
using MochiSprout.Models;
namespace MochiSprout.Views;

/// <summary>Datos derivados de una tarjeta de preset. Valores: [preset, controlador, sello de refresco]. Parámetro: summary · exported · missing.</summary>
public sealed class PresetInfoConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not ProjectProfile profile || values[1] is not PresetController controller)
            return parameter as string == "summary" ? "" : Visibility.Collapsed;
        return (parameter as string) switch
        {
            "summary" => controller.PresetPackageSummary(profile),
            "exported" => controller.IsExported(profile) ? Visibility.Visible : Visibility.Collapsed,
            "missing" => controller.MissingFileCount(profile) > 0 ? Visibility.Visible : Visibility.Collapsed,
            "missingText" => controller.MissingFileCount(profile) switch { 1 => "1 archivo no se encuentra", var n => $"{n} archivos no se encuentran" },
            _ => Binding.DoNothing
        };
    }
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Visible cuando el valor coincide (texto) con el parámetro; con «!» al principio invierte.</summary>
public sealed class MatchVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var p = parameter?.ToString() ?? "";
        var invert = p.StartsWith('!');
        var match = string.Equals(value?.ToString(), invert ? p[1..] : p, StringComparison.OrdinalIgnoreCase);
        return match ^ invert ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
