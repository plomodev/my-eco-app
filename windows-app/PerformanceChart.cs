using System.Drawing.Drawing2D;

namespace GameEcoBot;

internal sealed class PerformanceChart : Control
{
    private const int Capacity = 60;
    private readonly Queue<(double? Cpu, double Ram)> _samples = new();

    public PerformanceChart()
    {
        DoubleBuffered = true;
        BackColor = UiTheme.Surface;
        ForeColor = UiTheme.Text;
        SetStyle(
            ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw,
            true);
    }

    public void AddSample(double? cpu, double ram)
    {
        _samples.Enqueue((cpu, ram));
        while (_samples.Count > Capacity)
        {
            _samples.Dequeue();
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (ClientSize.Width < 80 || ClientSize.Height < 80)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var plot = new Rectangle(46, 18, Math.Max(20, Width - 66), Math.Max(20, Height - 54));

        using var gridPen = new Pen(Color.FromArgb(70, UiTheme.Muted), 1f)
        {
            DashPattern = [3f, 5f],
        };
        using var labelFont = UiTheme.CreateFont(8f);
        using var labelBrush = new SolidBrush(UiTheme.Muted);
        foreach (var percent in new[] { 100, 75, 50, 25, 0 })
        {
            var y = plot.Top + (int)(plot.Height * (100 - percent) / 100d);
            e.Graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            e.Graphics.DrawString(
                $"{percent}%",
                labelFont,
                labelBrush,
                2,
                y - labelFont.Height / 2f);
        }

        DrawSeries(
            e.Graphics,
            plot,
            _samples.Select(sample => (double?)sample.Ram).ToArray(),
            UiTheme.Cyan);
        DrawSeries(
            e.Graphics,
            plot,
            _samples.Select(sample => sample.Cpu).ToArray(),
            UiTheme.VioletLight);

        if (_samples.Count < 2)
        {
            using var waitFont = UiTheme.CreateFont(9f);
            using var waitBrush = new SolidBrush(UiTheme.Muted);
            e.Graphics.DrawString(
                "Les mesures arrivent…",
                waitFont,
                waitBrush,
                plot.Left + 10,
                plot.Top + 10);
        }
    }

    private static void DrawSeries(
        Graphics graphics,
        Rectangle plot,
        IReadOnlyList<double?> values,
        Color color)
    {
        if (values.Count < 2)
        {
            return;
        }

        using var pen = new Pen(color, 2.2f)
        {
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };

        var points = new List<PointF>();
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is not { } value)
            {
                if (points.Count > 1)
                {
                    graphics.DrawLines(pen, points.ToArray());
                }

                points.Clear();
                continue;
            }

            var x = plot.Left + (values.Count == 1 ? 0 : plot.Width * i / (float)(values.Count - 1));
            var y = plot.Bottom - (float)(Math.Clamp(value, 0, 100) / 100d * plot.Height);
            points.Add(new PointF(x, y));
        }

        if (points.Count > 1)
        {
            graphics.DrawLines(pen, points.ToArray());
        }
    }
}
