using ComunioAdminTools.Models;

namespace ComunioAdminTools.Controls;

public partial class PuntosJornadaView : ContentView
{
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource), typeof(IEnumerable<PuntosJornadaModel>), typeof(PuntosJornadaView));

    public IEnumerable<PuntosJornadaModel>? ItemsSource
    {
        get => (IEnumerable<PuntosJornadaModel>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public PuntosJornadaView()
    {
        InitializeComponent();
    }
}
