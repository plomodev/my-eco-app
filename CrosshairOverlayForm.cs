using System;
using System.Drawing;
using System.Windows.Forms;

namespace GameEcoBot;

/// <summary>
/// Fenêtre overlay transparente "clic traversant" (Click-Through) affichée au-dessus de tous les jeux.
/// N'interfère jamais avec la souris, ne vole pas le focus du jeu (WS_EX_NOACTIVATE),
/// reste au premier plan permanent (HWND_TOPMOST) et consomme 0% de ressource CPU/GPU.
/// Supporte le centrage multi-écrans et les résolutions étirées (True Stretched 4:3).
/// </summary>
public sealed class CrosshairOverlayForm : Form
{
    public const int BoxSize = 180;
    private static readonly Color ChromaKey = Color.FromArgb(255, 0, 255); // Magenta pur

    public CrosshairOverlayForm()
    {
        this.FormBorderStyle = FormBorderStyle.None;
        this.ShowInTaskbar = false;
        this.TopMost = true;
        this.StartPosition = FormStartPosition.Manual;
        this.BackColor = ChromaKey;
        this.TransparencyKey = ChromaKey;
        this.Size = new Size(BoxSize, BoxSize);
        this.DoubleBuffered = true;

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.ResizeRedraw,
            true);

        UpdatePosition();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x00080000; // WS_EX_LAYERED
            cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT (la souris traverse sans intercepter)
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW (masqué d'Alt-Tab)
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE (ne vole jamais le focus du jeu)
            return cp;
        }
    }

    public void UpdatePosition()
    {
        try
        {
            Screen targetScreen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg != IntPtr.Zero && fg != this.Handle)
            {
                Screen? fgScreen = Screen.FromHandle(fg);
                if (fgScreen != null) targetScreen = fgScreen;
            }

            Rectangle bounds = targetScreen.Bounds;
            int cx = bounds.Left + bounds.Width / 2 + CrosshairService.Instance.Settings.OffsetX;
            int cy = bounds.Top + bounds.Height / 2 + CrosshairService.Instance.Settings.OffsetY;

            this.Location = new Point(cx - BoxSize / 2, cy - BoxSize / 2);
        }
        catch
        {
            Rectangle bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            int cx = bounds.Left + bounds.Width / 2 + CrosshairService.Instance.Settings.OffsetX;
            int cy = bounds.Top + bounds.Height / 2 + CrosshairService.Instance.Settings.OffsetY;
            this.Location = new Point(cx - BoxSize / 2, cy - BoxSize / 2);
        }
    }

    public void ShowOverlay()
    {
        UpdatePosition();
        if (!this.Visible)
        {
            NativeMethods.ShowWindow(this.Handle, NativeMethods.SW_SHOWNOACTIVATE);
        }
        // Réaffirmer HWND_TOPMOST pour rester au-dessus des jeux plein écran sans bordure
        NativeMethods.SetWindowPos(this.Handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
        this.Invalidate();
    }

    public void HideOverlay()
    {
        NativeMethods.ShowWindow(this.Handle, NativeMethods.SW_HIDE);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(ChromaKey);

        float cx = BoxSize / 2f;
        float cy = BoxSize / 2f;

        CrosshairService.DrawCrosshair(e.Graphics, cx, cy, CrosshairService.Instance.Settings);
    }
}
