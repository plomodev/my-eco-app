using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GameEcoBot;

/// <summary>
/// Fenêtre hôte de fond d'écran animé intégrée au bureau Windows (WorkerW / Wallpaper Engine).
/// S'affiche directement derrière les icônes du bureau et exécute vidéos, photos animées et shaders à 60 FPS.
/// </summary>
public sealed class WallpaperHostForm : Form
{
    private WebView2? _webView;
    private bool _isWebViewReady = false;
    private IntPtr _workerW = IntPtr.Zero;

    public WallpaperHostForm()
    {
        this.FormBorderStyle = FormBorderStyle.None;
        this.ShowInTaskbar   = false;
        this.StartPosition   = FormStartPosition.Manual;
        this.BackColor       = Color.Black;

        Rectangle screenBounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        this.Location = screenBounds.Location;
        this.Size     = screenBounds.Size;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
            return cp;
        }
    }

    // ── Initialisation WebView2 ─────────────────────────────────────────────
    public async Task EnsureReadyAsync()
    {
        if (_isWebViewReady) return;

        if (_webView == null)
        {
            _webView = new WebView2 { Dock = DockStyle.Fill, BackColor = Color.Black };
            this.Controls.Add(_webView);
        }

        string userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GameEcoBot", "Wallpaper_Profile");
        Directory.CreateDirectory(userDataFolder);

        var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
        await _webView.EnsureCoreWebView2Async(env);

        _webView.CoreWebView2.Settings.IsStatusBarEnabled          = false;
        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        _webView.CoreWebView2.Settings.AreDevToolsEnabled          = false;
        _webView.CoreWebView2.Settings.IsZoomControlEnabled        = false;
        _webView.CoreWebView2.Settings.IsBuiltInErrorPageEnabled   = false;

        _isWebViewReady = true;
    }

    // ── Attachement WorkerW (appelé AVANT NavigateToString) ────────────────
    public void AttachToDesktop()
    {
        if (_workerW != IntPtr.Zero) return; // déjà attaché

        _workerW = GetWorkerWHandle();
        if (_workerW != IntPtr.Zero)
        {
            NativeMethods.SetParent(this.Handle, _workerW);
        }

        RefreshPosition();
        NativeMethods.ShowWindow(this.Handle, NativeMethods.SW_SHOWNOACTIVATE);
    }

    private void RefreshPosition()
    {
        if (!this.IsHandleCreated) return;
        Rectangle bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        NativeMethods.SetWindowPos(
            this.Handle, IntPtr.Zero,
            0, 0, bounds.Width, bounds.Height,
            NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_NOACTIVATE);
    }

    // ── Jouer une vidéo ─────────────────────────────────────────────────────
    public async Task PlayVideoAsync(string videoPath, float volume, bool muted)
    {
        await EnsureReadyAsync();
        if (_webView?.CoreWebView2 == null) return;

        // Attacher AVANT de naviguer
        AttachToDesktop();

        string directory = Path.GetDirectoryName(videoPath) ?? AppContext.BaseDirectory;
        string fileName  = Path.GetFileName(videoPath);

        try { _webView.CoreWebView2.SetVirtualHostNameToFolderMapping("wallpaper.local", directory, CoreWebView2HostResourceAccessKind.Allow); }
        catch { /* déjà mappé */ }

        string videoUrl  = "https://wallpaper.local/" + Uri.EscapeDataString(fileName);
        // Toujours démarrer muet pour contourner la politique autoplay des navigateurs, puis remettre le volume
        string volStr    = Math.Clamp(volume, 0f, 1f).ToString(System.Globalization.CultureInfo.InvariantCulture);

        string html =
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>" +
            "* { margin:0; padding:0; overflow:hidden; background:#000; box-sizing:border-box; }" +
            "body,html { width:100vw; height:100vh; }" +
            "video { position:fixed; top:0; left:0; width:100vw; height:100vh; object-fit:cover; }" +
            "</style></head><body>" +
            $"<video id=\"wpVideo\" src=\"{videoUrl}\" autoplay loop playsinline muted></video>" +
            "<script>" +
            "const v = document.getElementById('wpVideo');" +
            $"const targetVol = {volStr};" +
            "v.play().then(function() {" +
            "  v.muted = (targetVol <= 0.001); v.volume = targetVol;" +
            "}).catch(function() { v.muted = true; v.play(); });" +
            "window.setPaused = function(p) {" +
            "  if (p) v.pause(); else v.play().catch(function() { v.muted=true; v.play(); });" +
            "};" +
            "window.setVolume = function(vol) { v.volume = vol; v.muted = (vol <= 0.001); };" +
            "</script></body></html>";

        _webView.NavigateToString(html);
        await Task.Delay(400);
        RefreshPosition();
    }

    // ── Afficher une image ──────────────────────────────────────────────────
    public async Task ShowImageAsync(string imagePath, bool animate)
    {
        await EnsureReadyAsync();
        if (_webView?.CoreWebView2 == null) return;

        AttachToDesktop();

        string directory = Path.GetDirectoryName(imagePath) ?? AppContext.BaseDirectory;
        string fileName  = Path.GetFileName(imagePath);

        try { _webView.CoreWebView2.SetVirtualHostNameToFolderMapping("wallpaper.local", directory, CoreWebView2HostResourceAccessKind.Allow); }
        catch { }

        string imageUrl = "https://wallpaper.local/" + Uri.EscapeDataString(fileName);

        string animationCss = animate ? """
@keyframes panZoom {
    0%   { transform: scale(1.0) translate(0, 0); }
    50%  { transform: scale(1.06) translate(-1%, -1%); }
    100% { transform: scale(1.0) translate(0, 0); }
}
img { animation: panZoom 35s ease-in-out infinite alternate; }
""" : "";

        string html =
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>" +
            "* { margin:0; padding:0; overflow:hidden; background:#000; box-sizing:border-box; }" +
            "body,html { width:100vw; height:100vh; }" +
            "img { position:fixed; top:-4%; left:-4%; width:108vw; height:108vh; object-fit:cover; }" +
            animationCss +
            "</style></head><body>" +
            $"<img id=\"wpImage\" src=\"{imageUrl}\" />" +
            "</body></html>";

        _webView.NavigateToString(html);
        await Task.Delay(400);
        RefreshPosition();
    }

    // ── Jouer un preset Canvas ──────────────────────────────────────────────
    public async Task PlayPresetAsync(string presetId)
    {
        await EnsureReadyAsync();
        if (_webView?.CoreWebView2 == null) return;

        AttachToDesktop();

        string html = presetId switch
        {
            "matrix_rain" => GetMatrixRainHtml(),
            "deep_space"  => GetDeepSpaceHtml(),
            _             => GetMonochromeWavesHtml()
        };

        _webView.NavigateToString(html);
        await Task.Delay(400);
        RefreshPosition();
    }

    // ── Contrôles ───────────────────────────────────────────────────────────
    public async Task SetPausedAsync(bool paused)
    {
        if (_webView?.CoreWebView2 == null) return;
        try
        {
            await _webView.ExecuteScriptAsync(
                paused ? "if(window.setPaused)window.setPaused(true);"
                       : "if(window.setPaused)window.setPaused(false);");
        }
        catch { }
    }

    public async Task SetVolumeAsync(float volume)
    {
        if (_webView?.CoreWebView2 == null) return;
        try
        {
            string volStr = Math.Clamp(volume, 0f, 1f).ToString(System.Globalization.CultureInfo.InvariantCulture);
            await _webView.ExecuteScriptAsync($"if(window.setVolume)window.setVolume({volStr});");
        }
        catch { }
    }

    // ── Recherche du handle WorkerW ─────────────────────────────────────────
    private static IntPtr GetWorkerWHandle()
    {
        IntPtr progman = NativeMethods.FindWindow("Progman", null);
        if (progman == IntPtr.Zero) return IntPtr.Zero;

        // Déclenche la création du WorkerW d'arrière-plan
        NativeMethods.SendMessageTimeout(progman, 0x052C, new UIntPtr(0xD), IntPtr.Zero, 0, 1000, out _);
        NativeMethods.SendMessageTimeout(progman, 0x052C, UIntPtr.Zero, IntPtr.Zero, 0, 1000, out _);
        System.Threading.Thread.Sleep(80); // laisse Windows créer le WorkerW

        IntPtr workerW = IntPtr.Zero;

        // Méthode 1 : WorkerW frère du SHELLDLL_DefView
        NativeMethods.EnumWindows((tophandle, _) =>
        {
            IntPtr shellView = NativeMethods.FindWindowEx(tophandle, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView != IntPtr.Zero)
            {
                workerW = NativeMethods.FindWindowEx(IntPtr.Zero, tophandle, "WorkerW", null);
            }
            return workerW == IntPtr.Zero; // continuer tant que pas trouvé
        }, IntPtr.Zero);

        // Méthode 2 (secours) : WorkerW sans SHELLDLL_DefView
        if (workerW == IntPtr.Zero)
        {
            NativeMethods.EnumWindows((tophandle, _) =>
            {
                var sb = new StringBuilder(256);
                NativeMethods.GetClassName(tophandle, sb, 256);
                if (sb.ToString() == "WorkerW")
                {
                    IntPtr child = NativeMethods.FindWindowEx(tophandle, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (child == IntPtr.Zero)
                    {
                        workerW = tophandle;
                        return false;
                    }
                }
                return true;
            }, IntPtr.Zero);
        }

        return workerW != IntPtr.Zero ? workerW : progman;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  HTML Presets 60 FPS
    // ══════════════════════════════════════════════════════════════════════

    private static string GetMonochromeWavesHtml() => """
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<style>
* { margin: 0; padding: 0; overflow: hidden; background: #060606; }
canvas { display: block; width: 100vw; height: 100vh; }
</style>
</head>
<body>
<canvas id="c"></canvas>
<script>
const canvas = document.getElementById('c');
const ctx = canvas.getContext('2d');
let w, h;
let isPaused = false;
window.setPaused = function(p) { isPaused = p; };
function resize() { w = canvas.width = window.innerWidth; h = canvas.height = window.innerHeight; }
window.addEventListener('resize', resize);
resize();
let t = 0;
const waveCount = 5;
function draw() {
    if (!isPaused) {
        ctx.fillStyle = 'rgba(6, 6, 6, 0.12)';
        ctx.fillRect(0, 0, w, h);
        t += 0.008;
        for (let i = 0; i < waveCount; i++) {
            ctx.beginPath();
            const alpha = 0.15 + (i / waveCount) * 0.45;
            ctx.strokeStyle = `rgba(230, 230, 230, ${alpha})`;
            ctx.lineWidth = 1.5;
            const yOffset = h * 0.5 + (i - waveCount / 2) * 45;
            const freq = 0.0018 + i * 0.0004;
            const speed = t * (1 + i * 0.2);
            for (let x = 0; x <= w; x += 6) {
                const y = yOffset + Math.sin(x * freq + speed) * 85 + Math.cos(x * 0.001 - speed * 0.8) * 45;
                if (x === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
            }
            ctx.stroke();
        }
    }
    requestAnimationFrame(draw);
}
draw();
</script>
</body>
</html>
""";

    private static string GetMatrixRainHtml() => """
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<style>
* { margin: 0; padding: 0; overflow: hidden; background: #000; }
canvas { display: block; width: 100vw; height: 100vh; }
</style>
</head>
<body>
<canvas id="c"></canvas>
<script>
const canvas = document.getElementById('c');
const ctx = canvas.getContext('2d');
let w, h;
let isPaused = false;
window.setPaused = function(p) { isPaused = p; };
function resize() {
    w = canvas.width = window.innerWidth;
    h = canvas.height = window.innerHeight;
}
window.addEventListener('resize', resize);
resize();
const chars = '0123456789ABCDEF01アイウエオカキクケコサシスセソタチツテト';
const fontSize = 16;
let columns = Math.floor(w / fontSize);
let drops = Array(columns).fill(1);
function draw() {
    if (!isPaused) {
        ctx.fillStyle = 'rgba(0, 0, 0, 0.06)';
        ctx.fillRect(0, 0, w, h);
        ctx.font = fontSize + 'px monospace';
        for (let i = 0; i < drops.length; i++) {
            const text = chars.charAt(Math.floor(Math.random() * chars.length));
            ctx.fillStyle = (Math.random() > 0.88) ? '#ffffff' : '#22c55e';
            ctx.fillText(text, i * fontSize, drops[i] * fontSize);
            if (drops[i] * fontSize > h && Math.random() > 0.975) drops[i] = 0;
            drops[i]++;
        }
    }
    requestAnimationFrame(draw);
}
draw();
</script>
</body>
</html>
""";

    private static string GetDeepSpaceHtml() => """
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<style>
* { margin: 0; padding: 0; overflow: hidden; background: #000; }
canvas { display: block; width: 100vw; height: 100vh; }
</style>
</head>
<body>
<canvas id="c"></canvas>
<script>
const canvas = document.getElementById('c');
const ctx = canvas.getContext('2d');
let w, h;
let isPaused = false;
window.setPaused = function(p) { isPaused = p; };
function resize() { w = canvas.width = window.innerWidth; h = canvas.height = window.innerHeight; }
window.addEventListener('resize', resize);
resize();
const numStars = 650;
const stars = [];
for (let i = 0; i < numStars; i++) {
    stars.push({ x: (Math.random()-0.5)*2000, y: (Math.random()-0.5)*2000, z: Math.random()*2000 });
}
function draw() {
    if (!isPaused) {
        ctx.fillStyle = '#020202';
        ctx.fillRect(0, 0, w, h);
        const cx = w/2, cy = h/2;
        for (let star of stars) {
            star.z -= 6;
            if (star.z <= 0) { star.z=2000; star.x=(Math.random()-0.5)*2000; star.y=(Math.random()-0.5)*2000; }
            const k = 450/star.z;
            const px = star.x*k+cx, py = star.y*k+cy;
            if (px>=0 && px<w && py>=0 && py<h) {
                const size = Math.max(0.6,(1-star.z/2000)*3);
                const alpha = Math.min(1,(1-star.z/2000)*1.3);
                ctx.fillStyle = `rgba(255,255,255,${alpha})`;
                ctx.beginPath();
                ctx.arc(px,py,size,0,Math.PI*2);
                ctx.fill();
            }
        }
    }
    requestAnimationFrame(draw);
}
draw();
</script>
</body>
</html>
""";
}
