using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GameEcoBot;

/// <summary>
/// Conteneur de défilement 100% violet sans aucune barre blanche Win32.
/// - WS_VSCROLL et WS_HSCROLL sont définitivement retirés des styles Win32.
/// - ShowScrollBar masque tout résidu de barre native du système d'exploitation.
/// - La barre de défilement est un contrôle GDI+ violet sur-mesure intégré au panneau.
/// </summary>
public sealed class DarkScrollPanel : Panel, IMessageFilter
{
    private readonly Panel _innerPanel;
    private readonly VioletScrollBarControl _scrollBar;
    private int _scrollOffset = 0;
    private int _minContentHeight = 1700;
    private bool _redirecting = false;
    private bool _filterRegistered = false;

    private const int SB_BOTH = 3;
    private const int WS_VSCROLL = 0x00200000;
    private const int WS_HSCROLL = 0x00100000;

    [DllImport("user32.dll")]
    private static extern int ShowScrollBar(IntPtr hWnd, int wBar, [MarshalAs(UnmanagedType.Bool)] bool bShow);

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.Style &= ~WS_VSCROLL; // Retrait absolu de la barre blanche Windows
            cp.Style &= ~WS_HSCROLL;
            return cp;
        }
    }

    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public override bool AutoScroll
    {
        get => false;
        set => base.AutoScroll = false; // Jamais de barre native !
    }

    public Panel ContentPanel => _innerPanel;

    public Color TrackColor
    {
        get => _scrollBar.TrackColor;
        set => _scrollBar.TrackColor = value;
    }

    public Color ThumbColor
    {
        get => _scrollBar.ThumbColor;
        set => _scrollBar.ThumbColor = value;
    }

    public DarkScrollPanel()
    {
        this.DoubleBuffered = true;
        base.AutoScroll = false;
        this.BackColor = Color.FromArgb(10, 10, 10);

        _innerPanel = new Panel
        {
            Location = Point.Empty,
            Size = new Size(Math.Max(100, this.ClientSize.Width - 14), _minContentHeight),
            BackColor = Color.FromArgb(10, 10, 10),
            Anchor = AnchorStyles.None // Positionné manuellement via UpdateLayout
        };

        typeof(Panel).InvokeMember("DoubleBuffered",
            System.Reflection.BindingFlags.SetProperty |
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic,
            null, _innerPanel, new object[] { true });

        base.Controls.Add(_innerPanel);

        _scrollBar = new VioletScrollBarControl(this)
        {
            Width = 14,
            Dock = DockStyle.Right,
            BackColor = Color.FromArgb(10, 10, 10)
        };
        base.Controls.Add(_scrollBar);
        _scrollBar.BringToFront();

        try
        {
            Application.AddMessageFilter(this);
            _filterRegistered = true;
        }
        catch { }

        this.Resize += (s, e) => UpdateLayout();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try { ShowScrollBar(this.Handle, SB_BOTH, false); } catch { }
    }

    public new Size AutoScrollMinSize
    {
        get => new Size(0, _minContentHeight);
        set
        {
            _minContentHeight = Math.Max(value.Height, 600);
            UpdateContentHeight();
            UpdateLayout();
        }
    }

    public new void ScrollControlIntoView(Control? c)
    {
        if (c == null) return;
        Point loc = c.Location;
        Control? current = c.Parent;
        while (current != null && current != _innerPanel && current != this)
        {
            loc.Offset(current.Location);
            current = current.Parent;
        }
        SetScrollOffset(loc.Y - 24);
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (_redirecting) return;
        if (e.Control != null && e.Control != _innerPanel && e.Control != _scrollBar)
        {
            _redirecting = true;
            try
            {
                this.Controls.Remove(e.Control);
                _innerPanel.Controls.Add(e.Control);
                UpdateContentHeight();
                UpdateLayout();
            }
            finally
            {
                _redirecting = false;
            }
        }
    }

    public void UpdateContentHeight()
    {
        int maxBottom = _minContentHeight;
        foreach (Control c in _innerPanel.Controls)
        {
            if (c.Visible && c.Bottom > maxBottom)
                maxBottom = c.Bottom + 40;
        }
        if (_innerPanel.Height != maxBottom)
        {
            _innerPanel.Height = maxBottom;
        }
    }

    private void UpdateLayout()
    {
        int sbWidth = _scrollBar.Width;
        int targetW = Math.Max(100, this.ClientSize.Width - sbWidth);

        int maxScroll = Math.Max(0, _innerPanel.Height - this.ClientSize.Height);
        if (_scrollOffset > maxScroll)
            _scrollOffset = maxScroll;

        _innerPanel.Bounds = new Rectangle(0, -_scrollOffset, targetW, _innerPanel.Height);
        _scrollBar.Invalidate();
    }

    public void SetScrollOffset(int newOffset)
    {
        int maxScroll = Math.Max(0, _innerPanel.Height - this.ClientSize.Height);
        _scrollOffset = Math.Clamp(newOffset, 0, maxScroll);
        _innerPanel.Top = -_scrollOffset;
        _scrollBar.Invalidate();
    }

    public int ScrollOffset => _scrollOffset;
    public int ContentHeight => _innerPanel.Height;
    public int VisibleHeight => this.ClientSize.Height;

    public bool PreFilterMessage(ref Message m)
    {
        const int WM_MOUSEWHEEL = 0x020A;
        if (m.Msg == WM_MOUSEWHEEL)
        {
            if (this.IsDisposed || !this.Visible || !this.IsHandleCreated) return false;
            Point screenPos = Cursor.Position;
            if (this.RectangleToScreen(this.ClientRectangle).Contains(screenPos))
            {
                short delta = (short)((m.WParam.ToInt64() >> 16) & 0xffff);
                int lines = delta / 120;
                SetScrollOffset(_scrollOffset - (lines * 75));
                return true;
            }
        }
        return false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _filterRegistered)
        {
            try
            {
                Application.RemoveMessageFilter(this);
                _filterRegistered = false;
            }
            catch { }
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Contrôle de barre de défilement propriétaire 100% violette avec thumb capsule arrondi.
    /// </summary>
    private sealed class VioletScrollBarControl : Control
    {
        private readonly DarkScrollPanel _parent;
        private bool _isDragging = false;
        private int _dragStartY = 0;
        private int _dragStartOffset = 0;
        private bool _isHovered = false;

        public Color TrackColor { get; set; } = Color.FromArgb(10, 10, 10);
        public Color ThumbColor { get; set; } = Color.FromArgb(50, 50, 50);
        public Color ThumbHoverColor { get; set; } = Color.FromArgb(80, 80, 80);
        public Color ThumbDragColor { get; set; } = Color.FromArgb(120, 120, 120);

        public VioletScrollBarControl(DarkScrollPanel parent)
        {
            _parent = parent;
            this.SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            this.Cursor = Cursors.Default;
        }

        private Rectangle GetThumbRect()
        {
            int h = this.ClientSize.Height;
            int total = _parent.ContentHeight;
            int visible = _parent.VisibleHeight;
            if (total <= visible || h <= 0)
                return Rectangle.Empty;

            int thumbH = Math.Max(36, (int)((double)visible / total * h));
            int availableY = h - thumbH;
            int maxScroll = total - visible;
            int thumbY = maxScroll > 0 ? (int)((double)_parent.ScrollOffset / maxScroll * availableY) : 0;
            thumbY = Math.Clamp(thumbY, 0, availableY);

            int thumbW = 8;
            int thumbX = (this.ClientSize.Width - thumbW) / 2;
            return new Rectangle(thumbX, thumbY, thumbW, thumbH);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Piste de défilement violette sombre (fond identique)
            using (var trackBrush = new SolidBrush(TrackColor))
            {
                g.FillRectangle(trackBrush, this.ClientRectangle);
            }

            // Ligne de guidage discrète monochrome
            using (var guidePen = new Pen(Color.FromArgb(24, 24, 24), 1f))
            {
                int midX = this.ClientSize.Width / 2;
                g.DrawLine(guidePen, midX, 4, midX, this.ClientSize.Height - 4);
            }

            var thumbRect = GetThumbRect();
            if (thumbRect.IsEmpty) return;

            Color currentThumbColor = _isDragging ? ThumbDragColor : (_isHovered ? ThumbHoverColor : ThumbColor);

            using (var path = CreateRoundedRect(thumbRect, thumbRect.Width / 2))
            using (var thumbBrush = new SolidBrush(currentThumbColor))
            {
                g.FillPath(thumbBrush, path);
            }
        }

        private static GraphicsPath CreateRoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            this.Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            this.Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            var thumbRect = GetThumbRect();
            if (thumbRect.Contains(e.Location))
            {
                _isDragging = true;
                _dragStartY = e.Y;
                _dragStartOffset = _parent.ScrollOffset;
                this.Capture = true;
                this.Invalidate();
            }
            else
            {
                // Clic au-dessus ou en-dessous du thumb = Page Up / Page Down
                if (e.Y < thumbRect.Top)
                {
                    _parent.SetScrollOffset(_parent.ScrollOffset - _parent.VisibleHeight + 40);
                }
                else if (e.Y > thumbRect.Bottom)
                {
                    _parent.SetScrollOffset(_parent.ScrollOffset + _parent.VisibleHeight - 40);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDragging)
            {
                int h = this.ClientSize.Height;
                var thumbRect = GetThumbRect();
                int thumbH = thumbRect.Height;
                int availableY = h - thumbH;
                int maxScroll = _parent.ContentHeight - _parent.VisibleHeight;

                if (availableY > 0 && maxScroll > 0)
                {
                    int deltaY = e.Y - _dragStartY;
                    double deltaOffset = ((double)deltaY / availableY) * maxScroll;
                    _parent.SetScrollOffset(_dragStartOffset + (int)deltaOffset);
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDragging)
            {
                _isDragging = false;
                this.Capture = false;
                this.Invalidate();
            }
        }
    }
}