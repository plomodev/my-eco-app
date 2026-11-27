using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GameEcoBot;

/// <summary>
/// Carte moderne avec coins arrondis et bordure néon subtile dessinée pixel par pixel.
/// </summary>
public class ModernCard : Panel
{
    public int BorderRadius { get; set; } = 12;
    private Color _fillColor = Color.FromArgb(22, 17, 44);
    public Color FillColor
    {
        get => _fillColor;
        set { _fillColor = value; Invalidate(); }
    }

    public override Color BackColor
    {
        get => FillColor;
        set { FillColor = value; base.BackColor = value; }
    }

    public Color BorderColor { get; set; } = Color.FromArgb(60, 48, 105);

    public ModernCard()
    {
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                       ControlStyles.UserPaint |
                       ControlStyles.OptimizedDoubleBuffer |
                       ControlStyles.SupportsTransparentBackColor |
                       ControlStyles.ResizeRedraw, true);
        base.BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(GetEffectiveParentColor(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = CreateRoundedRectangle(rect, BorderRadius);

        using (var brush = new SolidBrush(FillColor))
        {
            g.FillPath(brush, path);
        }

        using (var pen = new Pen(BorderColor, 1f))
        {
            g.DrawPath(pen, path);
        }

        base.OnPaint(e);
    }

    public static Color GetEffectiveParentColor(Control ctrl)
    {
        Control? cur = ctrl.Parent;
        while (cur != null)
        {
            if (cur is ModernCard card && card.FillColor != Color.Transparent && card.FillColor.A > 0)
                return card.FillColor;

            if (cur.BackColor != Color.Transparent && cur.BackColor.A > 0)
                return cur.BackColor;

            cur = cur.Parent;
        }
        return Color.FromArgb(12, 10, 24);
    }

    public static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        if (d > rect.Width) d = rect.Width;
        if (d > rect.Height) d = rect.Height;

        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>
/// Bouton moderne avec coins arrondis, dégradé subtil, bordure et micro-animations de survol.
/// </summary>
public class ModernButton : Button
{
    public int BorderRadius { get; set; } = 10;
    private Color _normalColor = Color.FromArgb(36, 27, 58);
    public Color NormalColor
    {
        get => _normalColor;
        set
        {
            _normalColor = value;
            if (value != Color.Transparent && value.A > 0)
            {
                HoverColor = ControlPaint.Light(value, 0.2f);
                PressedColor = ControlPaint.Dark(value, 0.2f);
                BorderColor = ControlPaint.Light(value, 0.35f);
            }
            else
            {
                // Transparent / inactive pill state
                HoverColor = Color.FromArgb(52, 42, 90);
                PressedColor = Color.FromArgb(28, 21, 48);
                BorderColor = Color.Transparent;
            }
            Invalidate();
        }
    }

    public override Color BackColor
    {
        get => base.BackColor;
        set
        {
            NormalColor = value;
            base.BackColor = Color.Transparent; // WinForms Button needs this to avoid default painting
        }
    }

    public Color HoverColor { get; set; } = Color.FromArgb(52, 42, 90);
    public Color PressedColor { get; set; } = Color.FromArgb(28, 21, 48);
    public Color BorderColor { get; set; } = Color.FromArgb(60, 48, 105);
    public bool HasBorder { get; set; } = true;

    private bool isHovered = false;
    private bool isPressed = false;

    public ModernButton()
    {
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                       ControlStyles.UserPaint |
                       ControlStyles.OptimizedDoubleBuffer |
                       ControlStyles.SupportsTransparentBackColor |
                       ControlStyles.ResizeRedraw, true);
        base.BackColor = Color.Transparent;
        this.FlatStyle = FlatStyle.Flat;
        this.FlatAppearance.BorderSize = 0;
        this.ForeColor = Color.White;
        this.Font = AppFonts.GetBoldFont(9.5f);
        this.Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        isHovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        isHovered = false;
        isPressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            isPressed = true;
            Invalidate();
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        isPressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(ModernCard.GetEffectiveParentColor(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = ModernCard.CreateRoundedRectangle(rect, BorderRadius);

        Color bg = isPressed ? PressedColor : (isHovered ? HoverColor : NormalColor);
        if (bg != Color.Transparent && bg.A > 0)
        {
            using (var brush = new SolidBrush(bg))
            {
                g.FillPath(brush, path);
            }
        }

        if (HasBorder && BorderColor != Color.Transparent && BorderColor.A > 0)
        {
            Color bColor = isHovered ? Color.FromArgb(192, 132, 252) : BorderColor;
            using var pen = new Pen(bColor, 1f);
            g.DrawPath(pen, path);
        }

        // Texte centré
        TextRenderer.DrawText(g, Text, Font, rect, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

/// <summary>
/// Bouton circulaire moderne (Media Play/Pause, Précédent, Suivant, Fermer, Réduire).
/// </summary>
public class ModernCircleButton : Control
{
    public string IconText { get; set; } = "";
    public bool IsPrimary { get; set; } = false; // Blanc pour Play/Pause, Sombre pour le reste
    public Color NormalBg { get; set; } = Color.FromArgb(36, 27, 58);
    public Color HoverBg { get; set; } = Color.FromArgb(52, 42, 90);
    public Color NormalFg { get; set; } = Color.FromArgb(181, 168, 211);
    public Color HoverFg { get; set; } = Color.White;
    public bool IsDestructive { get; set; } = false; // Pour bouton fermer (rouge au survol)

    private bool isHovered = false;
    private bool isPressed = false;

    public ModernCircleButton()
    {
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                       ControlStyles.UserPaint |
                       ControlStyles.OptimizedDoubleBuffer |
                       ControlStyles.SupportsTransparentBackColor |
                       ControlStyles.ResizeRedraw, true);
        base.BackColor = Color.Transparent;
        this.Cursor = Cursors.Hand;
        this.Size = new Size(32, 32);
        this.Font = AppFonts.GetBoldFont(10f);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        isHovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        isHovered = false;
        isPressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        isPressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        isPressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(ModernCard.GetEffectiveParentColor(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        int size = Math.Min(Width, Height) - 1;
        int x = (Width - size) / 2;
        int y = (Height - size) / 2;
        var rect = new Rectangle(x, y, size, size);

        Color bg;
        Color fg;

        if (IsPrimary)
        {
            bg = isPressed ? Color.FromArgb(210, 210, 210) : (isHovered ? Color.FromArgb(240, 240, 240) : Color.White);
            fg = Color.Black;
        }
        else if (IsDestructive && isHovered)
        {
            bg = Color.FromArgb(225, 29, 72); // Rose/Rouge néon
            fg = Color.White;
        }
        else
        {
            bg = isPressed ? Color.FromArgb(52, 42, 90) : (isHovered ? HoverBg : NormalBg);
            fg = isHovered ? HoverFg : NormalFg;
        }

        using (var brush = new SolidBrush(bg))
        {
            g.FillEllipse(brush, rect);
        }

        // Bordure discrète si non-primaire
        if (!IsPrimary && !isHovered)
        {
            using var pen = new Pen(Color.FromArgb(40, 32, 64), 1f);
            g.DrawEllipse(pen, rect);
        }

        // Dessiner l'icône/texte
        TextRenderer.DrawText(g, IconText, Font, rect, fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

/// <summary>
/// Interrupteur à bascule moderne (Style iOS / Windows 11) remplaçant les vieilles CheckBox.
/// </summary>
public class ModernToggleSwitch : Control
{
    private bool isChecked = false;
    public bool Checked
    {
        get => isChecked;
        set
        {
            if (isChecked != value)
            {
                isChecked = value;
                Invalidate();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public Color ActiveTrackColor { get; set; } = Color.FromArgb(124, 58, 237);
    public Color InactiveTrackColor { get; set; } = Color.FromArgb(36, 27, 58);

    public event EventHandler? CheckedChanged;

    public ModernToggleSwitch()
    {
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                       ControlStyles.UserPaint |
                       ControlStyles.OptimizedDoubleBuffer |
                       ControlStyles.SupportsTransparentBackColor |
                       ControlStyles.ResizeRedraw, true);
        base.BackColor = Color.Transparent;
        this.Cursor = Cursors.Hand;
        this.Height = 36;
        this.Font = AppFonts.GetFont(9.5f);
    }

    protected override void OnClick(EventArgs e)
    {
        Checked = !Checked;
        base.OnClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(ModernCard.GetEffectiveParentColor(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        // Dimensions de la pilule toggle
        int pillW = 42;
        int pillH = 22;
        int pillY = (Height - pillH) / 2;
        var pillRect = new Rectangle(0, pillY, pillW, pillH);

        using var path = ModernCard.CreateRoundedRectangle(pillRect, pillH / 2);

        Color trackColor = Checked ? ActiveTrackColor : InactiveTrackColor;
        using (var brush = new SolidBrush(trackColor))
        {
            g.FillPath(brush, path);
        }

        using (var pen = new Pen(Checked ? Color.FromArgb(192, 132, 252) : Color.FromArgb(60, 48, 105), 1f))
        {
            g.DrawPath(pen, path);
        }

        // Thumb (cercle noir si actif, gris si inactif)
        int thumbSize = 16;
        int thumbY = pillY + (pillH - thumbSize) / 2;
        int thumbX = Checked ? (pillW - thumbSize - 3) : 3;

        using (var thumbBrush = new SolidBrush(Checked ? Color.White : Color.FromArgb(181, 168, 211)))
        {
            g.FillEllipse(thumbBrush, thumbX, thumbY, thumbSize, thumbSize);
        }

        // Titre & Sous-titre
        int textX = pillW + 12;
        int textW = Width - textX;

        if (string.IsNullOrEmpty(Subtitle))
        {
            var textRect = new Rectangle(textX, 0, textW, Height);
            TextRenderer.DrawText(g, Title, Font, textRect, Color.White,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        else
        {
            using (var titleFont = AppFonts.GetBoldFont(9f))
            using (var subFont = AppFonts.GetBodyFont(8f))
            {
                TextRenderer.DrawText(g, Title, titleFont, new Point(textX, pillY - 2), Color.White);
                TextRenderer.DrawText(g, Subtitle, subFont, new Point(textX, pillY + 14), Color.FromArgb(181, 168, 211));
            }
        }
    }
}

/// <summary>
/// Curseur slider moderne haute fidélité (barre néon et poignée circulaire fluide).
/// </summary>
public class ModernSlider : Control
{
    private int val = 100;
    public int Minimum { get; set; } = 5;
    public int Maximum { get; set; } = 100;

    public int Value
    {
        get => val;
        set
        {
            int clamped = Math.Max(Minimum, Math.Min(Maximum, value));
            if (val != clamped)
            {
                val = clamped;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public Color ActiveColor { get; set; } = Color.FromArgb(124, 58, 237);
    public Color InactiveColor { get; set; } = Color.FromArgb(36, 27, 58);

    public event EventHandler? ValueChanged;
    private bool isDragging = false;

    public ModernSlider()
    {
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                       ControlStyles.UserPaint |
                       ControlStyles.OptimizedDoubleBuffer |
                       ControlStyles.SupportsTransparentBackColor |
                       ControlStyles.ResizeRedraw, true);
        base.BackColor = Color.Transparent;
        this.Cursor = Cursors.Hand;
        this.Height = 28;
    }

    private void UpdateValueFromMouse(int mouseX)
    {
        int pad = 12;
        int trackW = Width - (pad * 2);
        if (trackW <= 0) return;

        float ratio = Math.Max(0f, Math.Min(1f, (float)(mouseX - pad) / trackW));
        Value = (int)Math.Round(Minimum + ratio * (Maximum - Minimum));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            isDragging = true;
            UpdateValueFromMouse(e.X);
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (isDragging)
        {
            UpdateValueFromMouse(e.X);
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        isDragging = false;
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(ModernCard.GetEffectiveParentColor(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int pad = 12;
        int trackH = 6;
        int trackY = (Height - trackH) / 2;
        int trackW = Width - (pad * 2);

        // Track inactif
        var bgRect = new Rectangle(pad, trackY, trackW, trackH);
        using (var bgPath = ModernCard.CreateRoundedRectangle(bgRect, trackH / 2))
        using (var bgBrush = new SolidBrush(InactiveColor))
        {
            g.FillPath(bgBrush, bgPath);
        }

        // Track actif
        float progress = (float)(Value - Minimum) / (Maximum - Minimum);
        int activeW = (int)(trackW * progress);

        if (activeW >= 2)
        {
            var activeRect = new Rectangle(pad, trackY, activeW, trackH);
            using var activePath = ModernCard.CreateRoundedRectangle(activeRect, trackH / 2);
            using var activeBrush = new LinearGradientBrush(activeRect, Color.FromArgb(109, 40, 217), ActiveColor, 0f);
            g.FillPath(activeBrush, activePath);
        }
        else if (activeW == 1)
        {
            var activeRect = new Rectangle(pad, trackY, activeW, trackH);
            using var activePath = ModernCard.CreateRoundedRectangle(activeRect, trackH / 2);
            using var activeBrush = new SolidBrush(ActiveColor);
            g.FillPath(activeBrush, activePath);
        }

        // Thumb
        int thumbRadius = 9;
        int thumbX = pad + (int)(trackW * progress);
        int thumbY = Height / 2;

        // Aura lumineuse autour du thumb
        using (var glowBrush = new SolidBrush(Color.FromArgb(30, 124, 58, 237)))
        {
            g.FillEllipse(glowBrush, thumbX - thumbRadius - 4, thumbY - thumbRadius - 4, (thumbRadius + 4) * 2, (thumbRadius + 4) * 2);
        }

        // Corps du thumb
        using (var thumbBrush = new SolidBrush(Color.White))
        {
            g.FillEllipse(thumbBrush, thumbX - thumbRadius, thumbY - thumbRadius, thumbRadius * 2, thumbRadius * 2);
        }

        // Centre sombre
        using (var centerBrush = new SolidBrush(Color.FromArgb(22, 17, 44)))
        {
            g.FillEllipse(centerBrush, thumbX - 3, thumbY - 3, 6, 6);
        }
    }
}

/// <summary>
/// Bouton Play/Pause Spotify pixel-perfect :
/// Cercle blanc pur avec icône noire peinte vectoriellement (2 barres verticales pour pause, 1 triangle pour play).
/// </summary>
public class SpotifyPlayPauseButton : Panel
{
    private bool isPlaying = false;
    private bool isHovered = false;
    private bool isPressed = false;

    public bool IsPlaying
    {
        get => isPlaying;
        set
        {
            if (isPlaying != value)
            {
                isPlaying = value;
                Invalidate();
            }
        }
    }

    public SpotifyPlayPauseButton()
    {
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                       ControlStyles.UserPaint |
                       ControlStyles.OptimizedDoubleBuffer |
                       ControlStyles.SupportsTransparentBackColor |
                       ControlStyles.ResizeRedraw, true);
        base.BackColor = Color.Transparent;
        this.Cursor = Cursors.Hand;
        this.Size = new Size(34, 34);
    }

    protected override void OnMouseEnter(EventArgs e) { isHovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { isHovered = false; isPressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { isPressed = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { isPressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(ModernCard.GetEffectiveParentColor(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int size = Math.Min(Width, Height) - 1;
        int x = (Width - size) / 2;
        int y = (Height - size) / 2;
        var circleRect = new Rectangle(x, y, size, size);

        // Cercle blanc pur (ou légèrement assombri au press)
        Color bgColor = isPressed ? Color.FromArgb(215, 215, 215) : (isHovered ? Color.FromArgb(248, 248, 248) : Color.White);
        using (var brush = new SolidBrush(bgColor))
        {
            g.FillEllipse(brush, circleRect);
        }

        int cx = Width / 2;
        int cy = Height / 2;

        using var fgBrush = new SolidBrush(Color.Black);

        if (isPlaying)
        {
            // Deux barres verticales noires (PAUSE) - rendu net pixel-perfect
            int barWidth = 3;
            int barHeight = 13;
            int gap = 4;
            int barY = cy - barHeight / 2;
            int bar1X = cx - gap / 2 - barWidth;
            int bar2X = cx + gap / 2;

            g.FillRectangle(fgBrush, bar1X, barY, barWidth, barHeight);
            g.FillRectangle(fgBrush, bar2X, barY, barWidth, barHeight);
        }
        else
        {
            // Triangle noir (PLAY) orienté à droite
            int triWidth = 12;
            int triHeight = 14;
            int leftX = cx - triWidth / 2 + 1;
            PointF[] pts = new PointF[]
            {
                new PointF(leftX, cy - triHeight / 2),
                new PointF(leftX + triWidth, cy),
                new PointF(leftX, cy + triHeight / 2)
            };
            g.FillPolygon(fgBrush, pts);
        }
    }
}

public enum SpotifyTrackButtonType { Next, Previous }

/// <summary>
/// Boutons Suivant / Précédent Spotify pixel-perfect :
/// Icône grise/blanche vectorielle (triangle + barre) sur fond discret/transparent.
/// </summary>
public class SpotifyTrackButton : Panel
{
    public SpotifyTrackButtonType ButtonType { get; set; } = SpotifyTrackButtonType.Next;
    private bool isHovered = false;
    private bool isPressed = false;

    public SpotifyTrackButton()
    {
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                       ControlStyles.UserPaint |
                       ControlStyles.OptimizedDoubleBuffer |
                       ControlStyles.SupportsTransparentBackColor |
                       ControlStyles.ResizeRedraw, true);
        base.BackColor = Color.Transparent;
        this.Cursor = Cursors.Hand;
        this.Size = new Size(28, 28);
    }

    protected override void OnMouseEnter(EventArgs e) { isHovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { isHovered = false; isPressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { isPressed = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { isPressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(ModernCard.GetEffectiveParentColor(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color iconColor = isPressed ? Color.FromArgb(160, 160, 160) : (isHovered ? Color.White : Color.FromArgb(185, 185, 185));
        using var brush = new SolidBrush(iconColor);

        int cx = Width / 2;
        int cy = Height / 2;

        if (ButtonType == SpotifyTrackButtonType.Next)
        {
            // Triangle vers la droite + barre verticale droite
            int triW = 9;
            int triH = 11;
            int barW = 2;
            int startX = cx - 5;

            PointF[] triangle = new PointF[]
            {
                new PointF(startX, cy - triH / 2),
                new PointF(startX + triW, cy),
                new PointF(startX, cy + triH / 2)
            };
            g.FillPolygon(brush, triangle);
            g.FillRectangle(brush, startX + triW + 1, cy - triH / 2, barW, triH);
        }
        else
        {
            // Barre verticale gauche + triangle vers la gauche
            int triW = 9;
            int triH = 11;
            int barW = 2;
            int endX = cx + 5;

            g.FillRectangle(brush, endX - triW - 1 - barW, cy - triH / 2, barW, triH);
            PointF[] triangle = new PointF[]
            {
                new PointF(endX, cy - triH / 2),
                new PointF(endX - triW, cy),
                new PointF(endX, cy + triH / 2)
            };
            g.FillPolygon(brush, triangle);
        }
    }
}

/// <summary>
/// Bouton checkmark vert Spotify (Liked / Sauvegardé) :
/// Cercle vert vif (#1DB954) avec coche noire au centre.
/// </summary>
public class SpotifyCheckButton : Panel
{
    private bool isHovered = false;
    private bool isPressed = false;

    public SpotifyCheckButton()
    {
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                       ControlStyles.UserPaint |
                       ControlStyles.OptimizedDoubleBuffer |
                       ControlStyles.SupportsTransparentBackColor |
                       ControlStyles.ResizeRedraw, true);
        base.BackColor = Color.Transparent;
        this.Cursor = Cursors.Hand;
        this.Size = new Size(20, 20);
    }

    protected override void OnMouseEnter(EventArgs e) { isHovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { isHovered = false; isPressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { isPressed = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { isPressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(ModernCard.GetEffectiveParentColor(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int size = Math.Min(Width, Height) - 1;
        int x = (Width - size) / 2;
        int y = (Height - size) / 2;
        var rect = new Rectangle(x, y, size, size);

        // Rond vert Spotify (#1ED760 / #1DB954)
        Color green = isPressed ? Color.FromArgb(24, 155, 70) : (isHovered ? Color.FromArgb(30, 215, 96) : Color.FromArgb(29, 185, 84));
        using (var brush = new SolidBrush(green))
        {
            g.FillEllipse(brush, rect);
        }

        // Checkmark noir au centre
        using (var pen = new Pen(Color.Black, 1.8f))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            float cx = Width / 2f;
            float cy = Height / 2f;
            PointF p1 = new PointF(cx - 3.5f, cy);
            PointF p2 = new PointF(cx - 0.5f, cy + 3f);
            PointF p3 = new PointF(cx + 4f, cy - 2.5f);
            g.DrawLines(pen, new PointF[] { p1, p2, p3 });
        }
    }
}

