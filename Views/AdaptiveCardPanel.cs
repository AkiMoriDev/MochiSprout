using System.Windows;
using System.Windows.Controls;
namespace MochiSprout.Views;
/// <summary>Grilla de hasta tres columnas (mínimo ~260 px por columna) con altura compartida por fila.</summary>
public sealed class AdaptiveCardPanel : Panel
{
 public static readonly DependencyProperty MinRowHeightProperty=DependencyProperty.Register(nameof(MinRowHeight),typeof(double),typeof(AdaptiveCardPanel),new FrameworkPropertyMetadata(150d,FrameworkPropertyMetadataOptions.AffectsMeasure));
 public double MinRowHeight{get=>(double)GetValue(MinRowHeightProperty);set=>SetValue(MinRowHeightProperty,value);}
 private int columns; private double width; private readonly List<double> rows=[];
 protected override Size MeasureOverride(Size available){var usable=double.IsInfinity(available.Width)?1000:available.Width;columns=Math.Clamp((int)((usable+16)/276),1,3);width=Math.Max(0,(usable-16*(columns-1))/columns);rows.Clear();for(int i=0;i<InternalChildren.Count;i++){InternalChildren[i].Measure(new Size(width,double.PositiveInfinity));if(i%columns==0)rows.Add(MinRowHeight);rows[^1]=Math.Max(rows[^1],InternalChildren[i].DesiredSize.Height);}return new Size(usable,rows.Sum()+Math.Max(0,rows.Count-1)*16);}
 protected override Size ArrangeOverride(Size finalSize){double y=0;for(int i=0;i<InternalChildren.Count;i++){var r=i/columns;InternalChildren[i].Arrange(new Rect((i%columns)*(width+16),y,width,rows[r]));if(i%columns==columns-1)y+=rows[r]+16;}return finalSize;}
}
