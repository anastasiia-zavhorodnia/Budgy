using System.Windows.Input;

namespace Budgy.Controls;

/// <summary>
/// Група кнопок-сегментів з підсвіткою активної.
/// Segments="Заголовок:Ключ|Заголовок:Ключ", SelectedKey – ключ активного сегмента (з ViewModel),
/// Command викликається з ключем натиснутого сегмента.
/// </summary>
public class SegmentedControl : ContentView
{
    public static readonly BindableProperty SegmentsProperty = BindableProperty.Create(
        nameof(Segments), typeof(string), typeof(SegmentedControl), string.Empty,
        propertyChanged: (b, _, _) => ((SegmentedControl)b).Rebuild());

    public static readonly BindableProperty SelectedKeyProperty = BindableProperty.Create(
        nameof(SelectedKey), typeof(string), typeof(SegmentedControl), string.Empty,
        propertyChanged: (b, _, _) => ((SegmentedControl)b).UpdateVisuals());

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(SegmentedControl));

    private readonly Grid _grid = new() { ColumnSpacing = 8 };
    private readonly List<(string Key, Button Button)> _buttons = new();

    public SegmentedControl() => Content = _grid;

    public string Segments
    {
        get => (string)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public string SelectedKey
    {
        get => (string)GetValue(SelectedKeyProperty);
        set => SetValue(SelectedKeyProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    private void Rebuild()
    {
        _grid.Children.Clear();
        _grid.ColumnDefinitions.Clear();
        _buttons.Clear();

        var parts = (Segments ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var pair = parts[i].Split(':', 2);
            var title = pair[0].Trim();
            var key = (pair.Length > 1 ? pair[1] : pair[0]).Trim();

            var button = new Button { Text = title, CornerRadius = 10, HeightRequest = 40, Padding = new Thickness(8, 0) };
            button.Clicked += (_, _) =>
            {
                if (Command?.CanExecute(key) == true) Command.Execute(key);
            };

            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _grid.Add(button, i, 0);
            _buttons.Add((key, button));
        }
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        var primary = Resource("Primary", "#5B3FD9");
        var soft = Resource("PrimarySoft", "#EEEBFF");

        foreach (var (key, button) in _buttons)
        {
            var selected = key == SelectedKey;
            button.BackgroundColor = selected ? primary : soft;
            button.TextColor = selected ? Colors.White : primary;
            button.FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None;
        }
    }

    private static Color Resource(string name, string fallbackHex)
        => Application.Current?.Resources.TryGetValue(name, out var value) == true && value is Color color
            ? color
            : Color.FromArgb(fallbackHex);
}
