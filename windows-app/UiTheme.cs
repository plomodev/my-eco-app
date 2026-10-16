using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;

namespace GameEcoBot;

internal static class UiTheme
{
    private static readonly PrivateFontCollection AshFonts = new();
    private static readonly FontFamily? Ash = LoadAsh();

    public static readonly Color Background = Color.FromArgb(12, 10, 24);
    public static readonly Color Sidebar = Color.FromArgb(10, 8, 18);
    public static readonly Color Surface = Color.FromArgb(22, 17, 44);
    public static readonly Color SurfaceRaised = Color.FromArgb(34, 26, 59);
    public static readonly Color Border = Color.FromArgb(60, 48, 105);
    public static readonly Color Violet = Color.FromArgb(124, 58, 237);
    public static readonly Color VioletLight = Color.FromArgb(192, 132, 252);
    public static readonly Color Text = Color.FromArgb(245, 243, 255);
    public static readonly Color Muted = Color.FromArgb(181, 168, 211);
    public static readonly Color Green = Color.FromArgb(74, 222, 128);
    public static readonly Color Cyan = Color.FromArgb(103, 232, 249);

    public static Font CreateFont(float size, FontStyle style = FontStyle.Regular)
    {
        try
        {
            if (Ash is not null && Ash.IsStyleAvailable(style))
            {
                return new Font(Ash, size, style, GraphicsUnit.Point);
            }
        }
        catch
        {
            // Fall through to the Windows UI font if the supplied font is unavailable.
        }

        return new Font("Segoe UI", size, style, GraphicsUnit.Point);
    }

    public static GraphicsPath RoundedPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(1, radius * 2);
        var arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.X;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static FontFamily? LoadAsh()
    {
        try
        {
            var resource = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("GameEcoBot.Assets.Ash.ttf");
            if (resource is null)
            {
                return null;
            }

            var fontPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GameEcoBot",
                "Cache",
                "Ash.ttf");
            Directory.CreateDirectory(Path.GetDirectoryName(fontPath)!);

            using (resource)
            using (var output = File.Create(fontPath))
            {
                resource.CopyTo(output);
            }

            AshFonts.AddFontFile(fontPath);
            return AshFonts.Families.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}

internal sealed class GlassPanel : Panel
{
    public GlassPanel()
    {
        DoubleBuffered = true;
        BackColor = UiTheme.Surface;
        ForeColor = UiTheme.Text;
        Padding = new Padding(18);
        SetStyle(
            ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw,
            true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (ClientSize.Width < 2 || ClientSize.Height < 2)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = UiTheme.RoundedPath(
            new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1),
            18);
        using var brush = new LinearGradientBrush(
            ClientRectangle,
            UiTheme.SurfaceRaised,
            UiTheme.Surface,
            135f);
        using var pen = new Pen(UiTheme.Border, 1f);
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(pen, path);
    }
}
