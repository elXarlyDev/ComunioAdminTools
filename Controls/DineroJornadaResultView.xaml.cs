using System.Collections.Generic;
using ComunioAdminTools.Models;

namespace ComunioAdminTools.Controls;

public partial class DineroJornadaResultView : ContentView
{
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource), typeof(IEnumerable<DineroJornadaModel>), typeof(DineroJornadaResultView));

    public IEnumerable<DineroJornadaModel>? ItemsSource
    {
        get => (IEnumerable<DineroJornadaModel>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public DineroJornadaResultView()
    {
        InitializeComponent();
    }
}
