using System.Drawing.Drawing2D;

namespace GameEcoBot;

internal sealed class UsageBar : Control
{
    private double _value;

    public UsageBar(Color fillColor)
    {
        FillColor = fillColor;
        Height = 7;
        BackColor = Color.FromArgb(54, 43, 82);
        SetStyle(
            ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw,
            true);
    }

    public Color FillColor { get; }

    public void SetValue(double value)
    {
        _value = Math.Clamp(value, 0, 100);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 2 || Height < 2)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var track = UiTheme.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), Height / 2);
        using var trackBrush = new SolidBrush(Color.FromArgb(54, 43, 82));
        e.Graphics.FillPath(trackBrush, track);

        var fillWidth = Math.Clamp((int)Math.Round(Width * _value / 100d), 0, Width);
        if (fillWidth < 2)
        {
            return;
        }

        using var fill = UiTheme.RoundedPath(new Rectangle(0, 0, fillWidth - 1, Height - 1), Height / 2);
        using var fillBrush = new LinearGradientBrush(
            new Rectangle(0, 0, fillWidth, Height),
            FillColor,
            UiTheme.VioletLight,
            0f);
        e.Graphics.FillPath(fillBrush, fill);
    }
}
