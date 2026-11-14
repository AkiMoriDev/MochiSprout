using System.Windows.Controls;
using System.Windows.Data;
using MochiSprout.Controllers;
namespace MochiSprout.Views;
public sealed class NewPresetTile {}
public partial class PresetsView : UserControl {public PresetsView(){InitializeComponent();DataContextChanged+=(_,_)=>{if(DataContext is PresetController c)OwnCards.ItemsSource=new CompositeCollection{new CollectionContainer{Collection=c.PresetView},new NewPresetTile()};};}}
