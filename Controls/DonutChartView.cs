using Budgy.Models;

namespace Budgy.Controls;

/// <summary>Кругова (кільцева) діаграма витрат за категоріями на основі Microsoft.Maui.Graphics — без сторонніх пакетів.</summary>
public class DonutChartView : GraphicsView
{
    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items), typeof(IEnumerable<CategoryStat>), typeof(DonutChartView), null,
        propertyChanged: (b, _, _) => ((DonutChartView)b).OnItemsChanged());

    private readonly DonutDrawable _drawable = new();

    public DonutChartView() => Drawable = _drawable;

    public IEnumerable<CategoryStat>? Items
    {
        get => (IEnumerable<CategoryStat>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    private void OnItemsChanged()
    {
        _drawable.Items = Items?.ToList() ?? new List<CategoryStat>();
        Invalidate();
    }
}

public class DonutDrawable : IDrawable
{
    public IReadOnlyList<CategoryStat> Items { get; set; } = Array.Empty<CategoryStat>();

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var size = Math.Min(dirtyRect.Width, dirtyRect.Height);
        if (size <= 8) return;

        float cx = dirtyRect.Center.X;
        float cy = dirtyRect.Center.Y;
        float outer = size / 2f - 4f;
        float inner = outer * 0.62f;
        var total = Items.Sum(i => i.Percent);

        // порожній стан — сіре кільце
        if (Items.Count == 0 || total <= 0)
        {
            canvas.StrokeColor = Color.FromArgb("#E5E7EB");
            canvas.StrokeSize = outer - inner;
            canvas.DrawCircle(cx, cy, (outer + inner) / 2f);
            return;
        }

        var gap = Items.Count > 1 ? 1.2 : 0.0; // невеликий проміжок між секторами
        double start = -90;                     // початок угорі, далі за годинниковою стрілкою

        foreach (var item in Items)
        {
            var sweep = 360.0 * item.Percent / total;
            var a0 = start + gap / 2;
            var a1 = start + sweep - gap / 2;
            start += sweep;
            if (a1 <= a0) continue;

            canvas.FillColor = ParseColor(item.ColorHex);
            canvas.FillPath(BuildSegment(cx, cy, outer, inner, a0, a1));
        }
    }

    private static PathF BuildSegment(float cx, float cy, float outer, float inner, double a0, double a1)
    {
        var path = new PathF();
        var steps = Math.Max(2, (int)Math.Ceiling((a1 - a0) / 2.0));

        for (var i = 0; i <= steps; i++) // зовнішня дуга
        {
            var (x, y) = PointOn(cx, cy, outer, a0 + (a1 - a0) * i / steps);
            if (i == 0) path.MoveTo(x, y); else path.LineTo(x, y);
        }
        for (var i = steps; i >= 0; i--) // внутрішня дуга у зворотному напрямку
        {
            var (x, y) = PointOn(cx, cy, inner, a0 + (a1 - a0) * i / steps);
            path.LineTo(x, y);
        }
        path.Close();
        return path;
    }

    private static (float X, float Y) PointOn(float cx, float cy, float radius, double degrees)
    {
        var rad = degrees * Math.PI / 180.0;
        return (cx + (float)(radius * Math.Cos(rad)), cy + (float)(radius * Math.Sin(rad)));
    }

    private static Color ParseColor(string? hex)
    {
        try { return Color.FromArgb(string.IsNullOrWhiteSpace(hex) ? "#5B3FD9" : hex); }
        catch { return Colors.Gray; }
    }
}
