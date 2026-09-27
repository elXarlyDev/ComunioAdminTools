using ComunioAdminTools.Models;

namespace ComunioAdminTools.Controls;

public partial class ExcesoPlantilla : ContentView
{
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource), typeof(IEnumerable<ExcesoPlantillaModel>), typeof(ExcesoPlantilla));

    public IEnumerable<ExcesoPlantillaModel>? ItemsSource
    {
        get => (IEnumerable<ExcesoPlantillaModel>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ExcesoPlantilla()
    {
        InitializeComponent();
    }
}
