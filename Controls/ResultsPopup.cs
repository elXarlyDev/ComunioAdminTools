using System.Collections;
using Microsoft.Maui.Controls.Shapes;

namespace ComunioAdminTools.Controls;

/// <summary>A reusable overlay hosting a data-bound results view.</summary>
public sealed class ResultsPopup : ContentView
{
    private readonly Label titleLabel;
    private readonly ContentView body;
    private TaskCompletionSource? completion;

    public ResultsPopup()
    {
        IsVisible = false;
        BackgroundColor = Color.FromArgb("#80000000");
        Padding = 20;
        titleLabel = new Label { FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#0B4D2C") };
        body = new ContentView();
        var closeButton = new Button { Text = "Cerrar", BackgroundColor = Color.FromArgb("#0B4D2C"), TextColor = Colors.White };
        closeButton.Clicked += (_, _) => Close();
        var layout = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) },
            RowSpacing = 16
        };
        layout.Add(titleLabel, 0, 0);
        layout.Add(body, 0, 1);
        layout.Add(closeButton, 0, 2);
        Content = new Border
        {
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb("#D7DED9"),
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = 20,
            MaximumWidthRequest = 600,
            MaximumHeightRequest = 520,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = layout
        };
    }

    public Task ShowAsync(string title, IEnumerable dataSource, View resultsView, BindableProperty itemsSourceProperty)
    {
        if (completion is not null)
            throw new InvalidOperationException("Ya hay un popup abierto.");

        resultsView.SetValue(itemsSourceProperty, dataSource);
        titleLabel.Text = title;
        body.Content = resultsView;
        completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IsVisible = true;
        return completion.Task;
    }

    public void Close()
    {
        IsVisible = false;
        body.Content = null;
        var pending = completion;
        completion = null;
        pending?.TrySetResult();
    }
}
