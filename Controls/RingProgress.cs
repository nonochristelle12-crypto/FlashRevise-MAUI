using Microsoft.Maui.Graphics;

namespace FlashRevise.Controls;

/// <summary>Anneau de progression dessiné (objectif du jour, taux de réussite).</summary>
public sealed class RingProgress : GraphicsView
{
    public static readonly BindableProperty ProgressProperty = BindableProperty.Create(
        nameof(Progress), typeof(double), typeof(RingProgress), 0d,
        propertyChanged: (bindable, _, _) => ((RingProgress)bindable).Invalidate());

    public static readonly BindableProperty RingColorProperty = BindableProperty.Create(
        nameof(RingColor), typeof(Color), typeof(RingProgress), Color.FromArgb("#6C4CF1"),
        propertyChanged: (bindable, _, _) => ((RingProgress)bindable).Invalidate());

    public static readonly BindableProperty TrackColorProperty = BindableProperty.Create(
        nameof(TrackColor), typeof(Color), typeof(RingProgress), Color.FromArgb("#2C2F42"),
        propertyChanged: (bindable, _, _) => ((RingProgress)bindable).Invalidate());

    public static readonly BindableProperty StrokeSizeProperty = BindableProperty.Create(
        nameof(StrokeSize), typeof(double), typeof(RingProgress), 8d,
        propertyChanged: (bindable, _, _) => ((RingProgress)bindable).Invalidate());

    public static readonly BindableProperty CaptionProperty = BindableProperty.Create(
        nameof(Caption), typeof(string), typeof(RingProgress), string.Empty,
        propertyChanged: (bindable, _, _) => ((RingProgress)bindable).Invalidate());

    public static readonly BindableProperty CaptionColorProperty = BindableProperty.Create(
        nameof(CaptionColor), typeof(Color), typeof(RingProgress), Colors.White,
        propertyChanged: (bindable, _, _) => ((RingProgress)bindable).Invalidate());

    public RingProgress()
    {
        Drawable = new RingDrawable(this);
    }

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public Color RingColor
    {
        get => (Color)GetValue(RingColorProperty);
        set => SetValue(RingColorProperty, value);
    }

    public Color TrackColor
    {
        get => (Color)GetValue(TrackColorProperty);
        set => SetValue(TrackColorProperty, value);
    }

    public double StrokeSize
    {
        get => (double)GetValue(StrokeSizeProperty);
        set => SetValue(StrokeSizeProperty, value);
    }

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public Color CaptionColor
    {
        get => (Color)GetValue(CaptionColorProperty);
        set => SetValue(CaptionColorProperty, value);
    }

    private sealed class RingDrawable(RingProgress owner) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var size = Math.Min(dirtyRect.Width, dirtyRect.Height);
            var stroke = (float)owner.StrokeSize;
            var inset = stroke / 2 + 1;
            var rect = new RectF(
                dirtyRect.Center.X - size / 2 + inset,
                dirtyRect.Center.Y - size / 2 + inset,
                size - inset * 2,
                size - inset * 2);

            canvas.StrokeSize = stroke;
            canvas.StrokeLineCap = LineCap.Round;

            canvas.StrokeColor = owner.TrackColor;
            canvas.DrawArc(rect, 0, 360, false, false);

            var progress = (float)Math.Clamp(owner.Progress, 0, 1);
            if (progress > 0)
            {
                canvas.StrokeColor = owner.RingColor;
                canvas.DrawArc(rect, -90, -90 + progress * 360, false, false);
            }

            if (!string.IsNullOrWhiteSpace(owner.Caption))
            {
                canvas.FontSize = Math.Max(12, size * 0.24f);
                canvas.FontColor = owner.CaptionColor;
                canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
                canvas.DrawString(
                    owner.Caption,
                    rect,
                    HorizontalAlignment.Center,
                    VerticalAlignment.Center);
            }
        }
    }
}
