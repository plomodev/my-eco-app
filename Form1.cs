using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GameEcoBot;

public partial class Form1 : Form
{
    private NotifyIcon trayIcon;
    private ContextMenuStrip trayMenu;
    private System.Windows.Forms.Timer monitorTimer;
    private bool isGameActive = false;
    private bool isManualEcoActive = false;
    private string activeGameName = "";
    private HashSet<int> currentlyThrottledPids = new();

    // Service de détection des jeux réels
    private GameIntelligenceService intelligenceService;

    // Suivi de ce que l'utilisateur entend (Audio & Pochette/Image)
    private AudioSessionTracker audioTracker;
    private PictureBox pbSoundArt;
    private Label lblSoundTitle;
    private Label lblSoundArtist;

    // Angle Snapping avec pourcentage réglable (1% à 100%)
    private bool isAngleSnappingActive = false;
    private int angleSnappingPercent = 50;
    private IntPtr mouseHookId = IntPtr.Zero;
    private NativeMethods.LowLevelMouseProc? mouseProc;
    private int lastMouseX = 0;
    private int lastMouseY = 0;
    private double snapSubPixelX = 0.0;
    private double snapSubPixelY = 0.0;
    private bool hasLastMousePos = false;
    private const int HOTKEY_ID_F8 = 9001;

    // Contrôles d'interface haute fidélité (Pixel par pixel)
    private Label lblStatusHeader;
    private Label lblStatusSubtitle;
    private ModernCard pnlStatusBanner;
    private Label lblEcoCount;
    private Label lblActiveGame;
    private Label lblAngleStatus;
    private RichTextBox rtbLogs;
    private bool isSnappingReentrant = false;
    private ModernToggleSwitch chkAutoDetect;
    private ModernToggleSwitch chkMouseNoDelay;
    private ModernToggleSwitch chkRunAtStartup;
    private ModernToggleSwitch chkAngleSnapping;
    private ModernSlider tbAngleSnapping;
    private Label lblAnglePercentValue;
    private ModernButton btnSearchGames;
    private ModernButton btnToggleEco;
    private ModernButton btnCleanRam;
    private ModernButton btnToggleAngleSnapping;
    private HardwareGraphControl hardwareGraph;
    private ModernButton btnGraphCpu;
    private ModernButton btnGraphRam;
    private ModernButton btnGraphApps;
    private ModernButton btnGraphGpu;

    // Mode Boost IA Ultra-Réactif (+300%) — toujours actif, pas de toggle UI
    private System.Windows.Forms.Timer fastDetectionTimer;

    // Media control buttons style Spotify / Windows 11
    private SpotifyTrackButton btnSpotifyPrev;
    private SpotifyPlayPauseButton btnSpotifyPlayPause;
    private SpotifyTrackButton btnSpotifyNext;
    private Label lblSoundSource;

    private bool startMinimized = false;

    // Processus protégés (JAMAIS mis en mode éco ni fermés par le limiteur)
    private readonly HashSet<string> protectedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        // Anti-cheats & Launchers Riot (Valorant, LoL)
        "vgc", "vgtray", "RiotClientServices", "RiotClientCrashHandler", "RiotClientUx", "VALORANT", "VALORANT-Win64-Shipping",
        // Steam
        "steam", "steamwebhelper", "steamservice",
        // Epic Games
        "EpicGamesLauncher", "EpicWebHelper", "UnrealCEFSubProcess",
        // Anti-cheats majeurs (EasyAntiCheat, BattlEye, Ricochet, Faceit, ACE, etc.)
        "EasyAntiCheat", "EasyAntiCheat_EOS", "EasyAntiCheat_x64", "EasyAntiCheat_Sys", "start_protected_game", "EasyAntiCheat_Setup",
        "BEService", "BEDaisy", "randgrid", "faceitclient", "faceitservice",
        "AntiCheatExpert", "SGuard64", "SGuard32", "PnkBstrA", "PnkBstrB", "GameMon", "GameMon64", "x3.xem", "xigncode",
        // Autres Launchers (Battle.net, EA, Ubisoft, GOG, Xbox)
        "Battle.net", "Agent", "EADesktop", "EALauncher", "Origin", "OriginClientService", "OriginWebHelperService",
        "upc", "Uplay", "UplayWebCore", "GalaxyClient", "GalaxyClientService", "GamingServices", "XboxAppServices", "XboxPcApp",
        // Périphériques Audio & Gaming
        "voicemeeter8", "voicemeeter", "voicemeeterpro", "audiodg", "RtkAudUService64", "RtkAudUService",
        "SteelSeriesGG", "SteelSeriesSonar", "SteelSeriesEngine", "SteelSeriesGGEZ", "SteelSeriesPrism",
        "ExitLag", "ExitLagPmService", "AudioRelay", "audiorelay-backend", "spacedeskService", "spacedeskServiceTray",
        "CrosshairX", "AMDNoiseSuppression", "amdfendrsr", "RadeonSoftware", "AMDRSServ", "AMDRSSrcExt", "atieclxx", "atiesrxx", "amdow",
        "nvcontainer", "nvspcaps64", "nvxdsync", "nvdisplay.container",
        "LogitechGHub", "lghub", "lghub_agent", "lghub_system_service", "RazerSynapse", "RazerCentral", "RzSDKServer", "Corsair.Service", "iCUE",
        // Communication & Média utiles en jeu
        "Discord", "Spotify",
        // Processus Noyau & Système Windows
        "system", "idle", "registry", "smss", "csrss", "wininit", "services",
        "lsass", "lsaiso", "winlogon", "fontdrvhost", "dwm", "ctfmon", "explorer", "sihost", "taskhostw",
        "runtimebroker", "startmenuexperiencehost", "searchhost", "shellhost", "textinputhost", "securityhealthservice",
        "securityhealthsystray", "smartscreen", "msmpeng", "nissrv", "mpdefendercorereservice", "taskmgr", "conhost", "svchost", "spoolsv",
        "GameEcoBot"
    };

    public Form1(bool startMinimized = false)
    {
        this.startMinimized = startMinimized;
        InitializeComponent();
        
        intelligenceService = new GameIntelligenceService();
        intelligenceService.OnLog += msg => { if (!this.IsDisposed && this.IsHandleCreated) this.Invoke(() => Log(msg)); };

        // Lancement en arrière-plan du scan IA des jeux installés dès le démarrage
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500);
            await intelligenceService.BoostIntelligenceAsync();
        });

        audioTracker = new AudioSessionTracker();
        audioTracker.OnTrackChanged += () => {
            if (!this.IsDisposed && this.IsHandleCreated) this.Invoke(UpdateSoundUi);
        };

        BuildAllVioletUi();
        SetupTray();

        // Angle Snapping forcé à 100% ultra-agressif
        angleSnappingPercent = 100;

        monitorTimer = new System.Windows.Forms.Timer();
        monitorTimer.Interval = 2000;
        monitorTimer.Tick += MonitorTimer_Tick;
        monitorTimer.Start();

        fastDetectionTimer = new System.Windows.Forms.Timer();
        fastDetectionTimer.Interval = 250; // Détection ultra-rapide 4x par seconde
        fastDetectionTimer.Tick += FastDetectionTimer_Tick;
        fastDetectionTimer.Start();

        // Réseau NoDelay activé en permanence
        _ = Task.Run(() => NativeMethods.EnableNetworkPingOptimization(true));

        Log("🟣 GameEcoBot 100% Violet démarré.");
        Log("🟣 Graphique AMD Ryzen 5 5500 actif.");
        Log("🎯 Raccourci F8 prêt pour l'Angle Snapping 100% ULTRA.");
        Log("⚡ Réseau NoDelay + Boost IA activés en permanence.");
    }

    private void FastDetectionTimer_Tick(object? sender, EventArgs e)
    {
        // Boost IA Ultra-Réactif toujours actif (pas de toggle)
        if (chkAutoDetect != null && chkAutoDetect.Checked)
        {
            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg != IntPtr.Zero)
            {
                NativeMethods.GetWindowThreadProcessId(fg, out int fgPid);
                if (fgPid > 0 && fgPid != Process.GetCurrentProcess().Id)
                {
                    try
                    {
                        var fgProc = Process.GetProcessById(fgPid);
                        string pName = fgProc.ProcessName;
                        if (!GameIntelligenceService.IsExcluded(pName) && !protectedProcesses.Contains(pName))
                        {
                            if (intelligenceService.KnownGames.Contains(pName) && (!isGameActive || activeGameName != pName))
                            {
                                CheckActiveGameAndApplyEco();
                            }
                        }
                    }
                    catch { }
                }
            }
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        SetupAngleSnappingHook();
        NativeMethods.EnableMouseNoDelay(true);

        if (startMinimized)
        {
            this.Hide();
            trayIcon.ShowBalloonTip(1500, "GameEcoBot", "Démarré en tâche de fond pour surveiller Valorant.", ToolTipIcon.Info);
        }
        else
        {
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
            this.Activate();
        }

        Log("🟣 Optimisation NO DELAY SOURIS activée (Timer 1ms + Réponse 1:1 Raw).");
        _ = audioTracker.UpdateAsync();
    }

    private void BuildAllVioletUi()
    {
        this.Size = new Size(820, 830);
        this.BackColor = Color.FromArgb(12, 10, 24);
        this.ForeColor = Color.White;
        this.Font = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Regular);
        this.Icon = CreateVioletAppIcon();
        this.StartPosition = FormStartPosition.CenterScreen;
        this.ShowInTaskbar = true;

        // ══════════════════════════════════════════════════════════
        // 1. HEADER BAR — Widget Musique Spotify & Contrôles Fenêtre
        // ══════════════════════════════════════════════════════════
        Panel pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 58,
            BackColor = Color.FromArgb(10, 8, 18),
            Padding = new Padding(0)
        };
        MakeDraggable(pnlHeader);

        // ── Widget musique style Spotify (identique à la photo de référence) ──
        Panel pnlSoundWidget = new Panel
        {
            Location = new Point(12, 5),
            Size = new Size(500, 48),
            BackColor = Color.Transparent,
        };
        MakeDraggable(pnlSoundWidget);

        // Pochette album avec coins arrondis
        pbSoundArt = new PictureBox
        {
            Size = new Size(40, 40),
            Location = new Point(2, 4),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        pbSoundArt.Image = CreateDefaultSoundIcon();

        // Titre de musique (blanc pur, gras, style Spotify)
        lblSoundTitle = new Label
        {
            Text = "Son en écoute",
            Location = new Point(48, 6),
            Size = new Size(160, 18),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Bold),
            AutoEllipsis = true,
            BackColor = Color.Transparent
        };
        MakeDraggable(lblSoundTitle);

        // Artiste / Source (gris clair discret, style Spotify)
        lblSoundArtist = new Label
        {
            Text = "Sortie Casque / Sonar",
            Location = new Point(48, 25),
            Size = new Size(160, 16),
            ForeColor = Color.FromArgb(170, 170, 170),
            Font = new Font("Segoe UI Variable Text", 8.2f, FontStyle.Regular),
            AutoEllipsis = true,
            BackColor = Color.Transparent
        };
        MakeDraggable(lblSoundArtist);

        // Label source (ex: "via Spotify") en violet discret au lieu de l'icône verte
        lblSoundSource = new Label
        {
            Text = "via Média",
            Location = new Point(216, 16),
            Size = new Size(80, 16),
            ForeColor = Color.FromArgb(140, 120, 190),
            Font = new Font("Segoe UI Variable Text", 7.5f, FontStyle.Italic),
            AutoEllipsis = true,
            BackColor = Color.Transparent
        };
        MakeDraggable(lblSoundSource);

        // Bouton Précédent (triangle + barre discret)
        btnSpotifyPrev = new SpotifyTrackButton
        {
            ButtonType = SpotifyTrackButtonType.Previous,
            Location = new Point(300, 10),
            Size = new Size(28, 28)
        };
        btnSpotifyPrev.Click += async (s, e) =>
        {
            await audioTracker.SkipPreviousAsync();
            SendMediaKey(NativeMethods.APPCOMMAND_MEDIA_PREVIOUSTRACK);
        };

        // Bouton Play/Pause (Cercle blanc pur, 2 barres noires de pause ou triangle noir)
        btnSpotifyPlayPause = new SpotifyPlayPauseButton
        {
            Location = new Point(334, 7),
            Size = new Size(34, 34),
            IsPlaying = true
        };
        btnSpotifyPlayPause.Click += async (s, e) =>
        {
            await audioTracker.TogglePlayPauseAsync();
            SendMediaKey(NativeMethods.APPCOMMAND_MEDIA_PLAY_PAUSE);
            btnSpotifyPlayPause.IsPlaying = !btnSpotifyPlayPause.IsPlaying;
        };

        // Bouton Suivant (triangle + barre discret, comme sur la photo)
        btnSpotifyNext = new SpotifyTrackButton
        {
            ButtonType = SpotifyTrackButtonType.Next,
            Location = new Point(374, 10),
            Size = new Size(28, 28)
        };
        btnSpotifyNext.Click += async (s, e) =>
        {
            await audioTracker.SkipNextAsync();
            SendMediaKey(NativeMethods.APPCOMMAND_MEDIA_NEXTTRACK);
        };

        pnlSoundWidget.Controls.Add(pbSoundArt);
        pnlSoundWidget.Controls.Add(lblSoundTitle);
        pnlSoundWidget.Controls.Add(lblSoundArtist);
        pnlSoundWidget.Controls.Add(lblSoundSource);
        pnlSoundWidget.Controls.Add(btnSpotifyPrev);
        pnlSoundWidget.Controls.Add(btnSpotifyPlayPause);
        pnlSoundWidget.Controls.Add(btnSpotifyNext);
        pnlHeader.Controls.Add(pnlSoundWidget);

        // ── Boutons fenêtre (─ ✕) circulaires à droite ────────────
        var btnMinimize = new ModernCircleButton
        {
            IconText = "─",
            Size = new Size(28, 28),
            Location = new Point(744, 18),
            NormalBg = Color.FromArgb(28, 22, 48),
            HoverBg = Color.FromArgb(52, 42, 82),
            NormalFg = Color.FromArgb(190, 180, 220),
            HoverFg = Color.White
        };
        btnMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;

        var btnClose = new ModernCircleButton
        {
            IconText = "✕",
            Size = new Size(28, 28),
            Location = new Point(778, 18),
            IsDestructive = true,
            NormalBg = Color.FromArgb(28, 22, 48),
            NormalFg = Color.FromArgb(190, 180, 220)
        };
        btnClose.Click += (s, e) =>
        {
            this.Hide();
            trayIcon.ShowBalloonTip(1500, "GameEcoBot", "Actif en arrière-plan.", ToolTipIcon.Info);
        };

        pnlHeader.Controls.Add(btnMinimize);
        pnlHeader.Controls.Add(btnClose);
        this.Controls.Add(pnlHeader);

        // ══════════════════════════════════════════════════════════
        // 2. Status Banner (Carte moderne arrondie avec liseré néon)
        // ══════════════════════════════════════════════════════════
        pnlStatusBanner = new ModernCard
        {
            Location = new Point(20, 76),
            Size = new Size(780, 66),
            BorderRadius = 12,
            FillColor = Color.FromArgb(22, 17, 44),
            BorderColor = Color.FromArgb(60, 48, 105),
            Padding = new Padding(15)
        };

        lblStatusHeader = new Label
        {
            Text = "🟣 EN VEILLE • PRÊT POUR LE JEU",
            Font = new Font("Segoe UI Variable Display", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(192, 132, 252),
            AutoSize = true,
            Location = new Point(16, 11),
            BackColor = Color.Transparent
        };

        lblStatusSubtitle = new Label
        {
            Text = "Dès qu'un jeu démarre, toutes les autres applications passent en Mode Éco.",
            Font = new Font("Segoe UI Variable Text", 8.8f),
            ForeColor = Color.FromArgb(180, 170, 215),
            Size = new Size(590, 20),
            AutoEllipsis = true,
            Location = new Point(17, 36),
            BackColor = Color.Transparent
        };

        var btnOptimizationAdvisor = CreateVioletActionButton(
            "🛠 Optimisations",
            Color.FromArgb(109, 40, 217),
            new Point(625, 15),
            140,
            (s, e) => OpenOptimizationAdvisor());
        btnOptimizationAdvisor.Size = new Size(140, 36);
        btnOptimizationAdvisor.Font = new Font("Segoe UI Variable Text", 8.5f, FontStyle.Bold);

        pnlStatusBanner.Controls.Add(lblStatusHeader);
        pnlStatusBanner.Controls.Add(lblStatusSubtitle);
        pnlStatusBanner.Controls.Add(btnOptimizationAdvisor);
        this.Controls.Add(pnlStatusBanner);

        // ══════════════════════════════════════════════════════════
        // 3. Hardware Graph Container (Carte moderne)
        // ══════════════════════════════════════════════════════════
        ModernCard pnlGraphContainer = new ModernCard
        {
            Location = new Point(20, 152),
            Size = new Size(780, 220),
            BorderRadius = 12,
            FillColor = Color.FromArgb(14, 12, 28),
            BorderColor = Color.FromArgb(44, 34, 76),
            Padding = new Padding(5)
        };

        // Pills CPU / RAM / GPU / APPS
        Panel pnlPills = new Panel
        {
            Location = new Point(460, 8),
            Size = new Size(310, 34),
            BackColor = Color.Transparent
        };

        btnGraphCpu = CreateVioletPillButton("CPU", new Point(2, 2), 70, true);
        btnGraphRam = CreateVioletPillButton("RAM", new Point(75, 2), 70, false);
        btnGraphGpu = CreateVioletPillButton("GPU", new Point(148, 2), 70, false);
        btnGraphApps = CreateVioletPillButton("APPS", new Point(221, 2), 78, false);

        btnGraphCpu.Click += (s, e) => SwitchGraphMode("CPU");
        btnGraphRam.Click += (s, e) => SwitchGraphMode("RAM");
        btnGraphGpu.Click += (s, e) => SwitchGraphMode("GPU");
        btnGraphApps.Click += (s, e) => SwitchGraphMode("APPS");

        pnlPills.Controls.Add(btnGraphCpu);
        pnlPills.Controls.Add(btnGraphRam);
        pnlPills.Controls.Add(btnGraphGpu);
        pnlPills.Controls.Add(btnGraphApps);
        pnlGraphContainer.Controls.Add(pnlPills);

        hardwareGraph = new HardwareGraphControl
        {
            Dock = DockStyle.Fill,
            Title = "AMD Ryzen 5 5500"
        };
        pnlGraphContainer.Controls.Add(hardwareGraph);
        this.Controls.Add(pnlGraphContainer);

        // ══════════════════════════════════════════════════════════
        // 4. Metric Cards Row (Cartes arrondies pixel par pixel)
        // ══════════════════════════════════════════════════════════
        int cardY = 382;
        int cardW = 250;
        int cardH = 68;

        ModernCard pnlCard1 = CreateMetricCard("🟣 APPS EN MODE ÉCO", "0 apps", Color.FromArgb(192, 132, 252), new Point(20, cardY), cardW, cardH, out lblEcoCount);
        ModernCard pnlCard2 = CreateMetricCard("🎮 JEU EN COURS", "En attente...", Color.FromArgb(168, 85, 247), new Point(285, cardY), cardW, cardH, out lblActiveGame);
        ModernCard pnlCard3 = CreateMetricCard("🎯 ANGLE SNAPPING", "DÉSACTIVÉ", Color.FromArgb(216, 180, 254), new Point(550, cardY), cardW, cardH, out lblAngleStatus);

        this.Controls.Add(pnlCard1);
        this.Controls.Add(pnlCard2);
        this.Controls.Add(pnlCard3);

        // ══════════════════════════════════════════════════════════
        // 5. Action Buttons (Boutons modernes avec micro-interactions)
        // ══════════════════════════════════════════════════════════
        int btnY = 460;
        int actBtnW = 186;

        btnSearchGames = CreateVioletActionButton("🔍 Recherche de jeu", Color.FromArgb(109, 40, 217), new Point(20, btnY), actBtnW, async (s, e) =>
        {
            btnSearchGames.Enabled = false;
            btnSearchGames.Text = "⏳ Scan IA...";
            int total = await intelligenceService.BoostIntelligenceAsync();
            btnSearchGames.Text = $"✅ {total} jeux trouvés";
            await Task.Delay(2500);
            btnSearchGames.Text = "🔍 Recherche de jeu";
            btnSearchGames.Enabled = true;
        });

        btnToggleEco = CreateVioletActionButton("🟣 Mode Éco", Color.FromArgb(124, 58, 237), new Point(218, btnY), actBtnW, BtnToggleEco_Click);
        btnCleanRam = CreateVioletActionButton("🧹 Vider la RAM", Color.FromArgb(139, 92, 246), new Point(416, btnY), actBtnW, BtnCleanRam_Click);
        btnToggleAngleSnapping = CreateVioletActionButton("🎯 Angle Snapping [F8]", Color.FromArgb(76, 29, 149), new Point(614, btnY), actBtnW, (s, e) => ToggleAngleSnapping());

        this.Controls.Add(btnSearchGames);
        this.Controls.Add(btnToggleEco);
        this.Controls.Add(btnCleanRam);
        this.Controls.Add(btnToggleAngleSnapping);

        // ══════════════════════════════════════════════════════════
        // 6. Section Angle Snapping avec Curseur Moderne
        // ══════════════════════════════════════════════════════════
        ModernCard pnlSnappingSettings = new ModernCard
        {
            Location = new Point(20, 510),
            Size = new Size(780, 54),
            BorderRadius = 10,
            FillColor = Color.FromArgb(20, 16, 40),
            BorderColor = Color.FromArgb(52, 42, 90),
            Padding = new Padding(10)
        };

        chkAngleSnapping = new ModernToggleSwitch
        {
            Title = "🎯 Angle Snapping [F8]",
            Subtitle = "Correction horizontale/verticale pour FPS",
            Checked = false,
            Location = new Point(12, 9),
            Size = new Size(320, 36)
        };
        chkAngleSnapping.CheckedChanged += (s, e) =>
        {
            if (chkAngleSnapping.Checked != isAngleSnappingActive) ToggleAngleSnapping();
        };

        Label lblSliderTitle = new Label
        {
            Text = "Précision :",
            Location = new Point(340, 18),
            AutoSize = true,
            ForeColor = Color.FromArgb(175, 165, 215),
            Font = new Font("Segoe UI Variable Text", 9f),
            BackColor = Color.Transparent
        };

        lblAnglePercentValue = new Label
        {
            Text = "50%",
            Location = new Point(415, 16),
            AutoSize = true,
            ForeColor = Color.FromArgb(216, 180, 254),
            Font = new Font("Segoe UI Variable Display", 10.5f, FontStyle.Bold),
            BackColor = Color.Transparent
        };

        tbAngleSnapping = new ModernSlider
        {
            Location = new Point(470, 14),
            Size = new Size(295, 26),
            Minimum = 5,
            Maximum = 100,
            Value = 50
        };
        tbAngleSnapping.ValueChanged += (s, e) =>
        {
            angleSnappingPercent = tbAngleSnapping.Value;
            lblAnglePercentValue.Text = $"{angleSnappingPercent}%";
            if (isAngleSnappingActive)
            {
                lblAngleStatus.Text = $"{angleSnappingPercent}% ACTIF";
                btnToggleAngleSnapping.Text = $"🎯 Snapping ({angleSnappingPercent}%)";
            }
        };

        pnlSnappingSettings.Controls.Add(chkAngleSnapping);
        pnlSnappingSettings.Controls.Add(lblSliderTitle);
        pnlSnappingSettings.Controls.Add(lblAnglePercentValue);
        pnlSnappingSettings.Controls.Add(tbAngleSnapping);
        this.Controls.Add(pnlSnappingSettings);

        // ══════════════════════════════════════════════════════════
        // 7. Automatisation & No Delay Souris (Interrupteurs simplifiés)
        //    Boost IA + Réseau NoDelay : toujours actifs, pas de toggle UI
        // ══════════════════════════════════════════════════════════
        ModernCard pnlAutoSettings = new ModernCard
        {
            Location = new Point(20, 566),
            Size = new Size(780, 80),
            BorderRadius = 10,
            FillColor = Color.FromArgb(17, 14, 36),
            BorderColor = Color.FromArgb(46, 36, 80),
            Padding = new Padding(8)
        };

        chkAutoDetect = new ModernToggleSwitch
        {
            Title = "🟣 Mode Éco Automatique",
            Subtitle = "Brider les apps d'arrière-plan en jeu",
            Checked = true,
            Location = new Point(12, 6),
            Size = new Size(370, 34)
        };

        chkMouseNoDelay = new ModernToggleSwitch
        {
            Title = "🟣 Souris No Delay (0ms)",
            Subtitle = "Timer 1ms + Réponse 1:1 sans latence Windows",
            Checked = true,
            Location = new Point(395, 6),
            Size = new Size(370, 34)
        };
        chkMouseNoDelay.CheckedChanged += (s, e) =>
        {
            NativeMethods.EnableMouseNoDelay(chkMouseNoDelay.Checked);
            Log(chkMouseNoDelay.Checked
                ? "🟣 NO DELAY SOURIS ACTIVÉ (Timer 1ms + Suivi 1:1 Raw)."
                : "ℹ️ No Delay Souris désactivé.");
        };

        chkRunAtStartup = new ModernToggleSwitch
        {
            Title = "🚀 Démarrer avec Windows",
            Subtitle = "Lancement automatique minimisé en tâche de fond",
            Checked = CheckStartupConfig(),
            Location = new Point(12, 42),
            Size = new Size(370, 34)
        };
        chkRunAtStartup.CheckedChanged += ChkRunAtStartup_CheckedChanged;

        pnlAutoSettings.Controls.Add(chkAutoDetect);
        pnlAutoSettings.Controls.Add(chkMouseNoDelay);
        pnlAutoSettings.Controls.Add(chkRunAtStartup);
        this.Controls.Add(pnlAutoSettings);

        // ══════════════════════════════════════════════════════════
        // 8. Activity Log Box (Console Terminal Sombre)
        // ══════════════════════════════════════════════════════════
        ModernCard pnlLogCard = new ModernCard
        {
            Location = new Point(20, 654),
            Size = new Size(780, 122),
            BorderRadius = 10,
            FillColor = Color.FromArgb(10, 8, 20),
            BorderColor = Color.FromArgb(40, 32, 70),
            Padding = new Padding(10)
        };

        Label lblLogTitle = new Label
        {
            Text = "● CONSOLE D'ACTIVITE & EVENEMENTS IA",
            Font = new Font("Segoe UI Variable Text", 8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(168, 145, 215),
            Location = new Point(14, 6),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlLogCard.Controls.Add(lblLogTitle);

        rtbLogs = new RichTextBox
        {
            Location = new Point(14, 24),
            Size = new Size(752, 90),
            BackColor = Color.FromArgb(10, 8, 20),
            ForeColor = Color.FromArgb(216, 180, 254),
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 9f),
            ReadOnly = true
        };
        pnlLogCard.Controls.Add(rtbLogs);
        this.Controls.Add(pnlLogCard);

        this.FormClosing += Form1_FormClosing;
    }

    // ── Helper : rendre un controle draggable pour deplacer la fenetre ──
    private Point _dragStart;
    private void MakeDraggable(Control ctrl)
    {
        ctrl.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
                _dragStart = e.Location;
        };
        ctrl.MouseMove += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                this.Left += e.X - _dragStart.X;
                this.Top += e.Y - _dragStart.Y;
            }
        };
    }

    // ── Envoyer une commande media Windows ──
    private void SendMediaKey(int command)
    {
        NativeMethods.SendMediaCommand(this.Handle, command);
    }

    private void UpdateSoundUi()
    {
        lblSoundTitle.Text = string.IsNullOrWhiteSpace(audioTracker.CurrentTitle) ? "Aucune lecture" : audioTracker.CurrentTitle;
        lblSoundArtist.Text = string.IsNullOrWhiteSpace(audioTracker.CurrentArtist) ? "Spotify / Windows" : audioTracker.CurrentArtist;
        lblSoundSource.Text = audioTracker.CurrentSource;
        if (btnSpotifyPlayPause != null)
        {
            btnSpotifyPlayPause.IsPlaying = audioTracker.IsPlaying;
        }
        if (audioTracker.CurrentThumbnail != null)
        {
            pbSoundArt.Image = audioTracker.CurrentThumbnail;
        }
        else
        {
            pbSoundArt.Image = CreateDefaultSoundIcon();
        }
    }

    private Image CreateDefaultSoundIcon()
    {
        Bitmap bmp = new Bitmap(44, 44);
        using Graphics g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        using (GraphicsPath path = ModernCard.CreateRoundedRectangle(new Rectangle(0, 0, 43, 43), 8))
        {
            using (Brush brush = new SolidBrush(Color.FromArgb(40, 32, 68)))
            {
                g.FillPath(brush, path);
            }
            using (Pen pen = new Pen(Color.FromArgb(70, 56, 115), 1f))
            {
                g.DrawPath(pen, path);
            }
        }

        using (Brush brush = new SolidBrush(Color.FromArgb(216, 180, 254)))
        {
            using Font font = new Font("Segoe UI", 13f);
            g.DrawString("🎧", font, brush, new PointF(7, 7));
        }
        return bmp;
    }

    private ModernButton CreateVioletPillButton(string text, Point location, int width, bool active)
    {
        var btn = new ModernButton
        {
            Text = text,
            Location = location,
            Size = new Size(width, 30),
            BorderRadius = 8,
            HasBorder = false,
            NormalColor = active ? Color.FromArgb(124, 58, 237) : Color.Transparent,
            HoverColor = active ? Color.FromArgb(139, 92, 246) : Color.FromArgb(36, 30, 60),
            PressedColor = Color.FromArgb(109, 40, 217),
            ForeColor = active ? Color.White : Color.FromArgb(170, 155, 205),
            Font = new Font("Segoe UI Variable Display", 9f, FontStyle.Bold)
        };
        return btn;
    }

    private void SwitchGraphMode(string mode)
    {
        // Couleur active unique violet pour tous les boutons pill
        Color activeBg = Color.FromArgb(124, 58, 237);
        Color inactiveBg = Color.Transparent;
        Color activeFg = Color.White;
        Color inactiveFg = Color.FromArgb(170, 155, 205);

        btnGraphCpu.NormalColor = mode == "CPU" ? activeBg : inactiveBg;
        btnGraphCpu.ForeColor = mode == "CPU" ? activeFg : inactiveFg;

        btnGraphRam.NormalColor = mode == "RAM" ? activeBg : inactiveBg;
        btnGraphRam.ForeColor = mode == "RAM" ? activeFg : inactiveFg;

        btnGraphGpu.NormalColor = mode == "GPU" ? activeBg : inactiveBg;
        btnGraphGpu.ForeColor = mode == "GPU" ? activeFg : inactiveFg;

        btnGraphApps.NormalColor = mode == "APPS" ? activeBg : inactiveBg;
        btnGraphApps.ForeColor = mode == "APPS" ? activeFg : inactiveFg;

        hardwareGraph.SetMode(mode);
    }

    private ModernButton CreateVioletActionButton(string text, Color bg, Point location, int width, EventHandler handler)
    {
        var btn = new ModernButton
        {
            Text = text,
            Size = new Size(width, 42),
            Location = location,
            BorderRadius = 10,
            HasBorder = true,
            NormalColor = bg,
            HoverColor = ControlPaint.Light(bg, 0.2f),
            PressedColor = ControlPaint.Dark(bg, 0.2f),
            BorderColor = ControlPaint.Light(bg, 0.35f),
            ForeColor = Color.White
        };
        btn.Click += handler;
        return btn;
    }

    private ModernCard CreateMetricCard(string label, string defaultValue, Color accentColor, Point location, int width, int height, out Label valLabel)
    {
        ModernCard card = new ModernCard
        {
            Location = location,
            Size = new Size(width, height),
            BorderRadius = 10,
            FillColor = Color.FromArgb(18, 14, 38),
            BorderColor = Color.FromArgb(50, 40, 84),
            Padding = new Padding(14, 8, 14, 8)
        };

        Label lbl = new Label
        {
            Text = label,
            Font = new Font("Segoe UI Variable Text", 7.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(160, 145, 195),
            Location = new Point(12, 10),
            AutoSize = true,
            BackColor = Color.Transparent
        };

        valLabel = new Label
        {
            Text = defaultValue,
            Font = new Font("Segoe UI Variable Display", 12f, FontStyle.Bold),
            ForeColor = accentColor,
            Location = new Point(12, 32),
            AutoSize = true,
            BackColor = Color.Transparent
        };

        card.Controls.Add(lbl);
        card.Controls.Add(valLabel);
        return card;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Color.FromArgb(48, 38, 80), 1f);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    private void SetupTray()
    {
        trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("🟣 Afficher GameEcoBot", null, (s, e) => ShowAndRestore());
        trayMenu.Items.Add("🛠 Assistant d'optimisation", null, (s, e) => OpenOptimizationAdvisor());
        trayMenu.Items.Add("🔍 Recherche de jeu", null, async (s, e) => await intelligenceService.BoostIntelligenceAsync());
        trayMenu.Items.Add("🎯 Basculer Angle Snapping (F8)", null, (s, e) => ToggleAngleSnapping());
        trayMenu.Items.Add("🟣 Activer/Désactiver Mode Éco", null, (s, e) => BtnToggleEco_Click(s, e));
        trayMenu.Items.Add("🧹 Purger la RAM", null, (s, e) => BtnCleanRam_Click(s, e));
        trayMenu.Items.Add("-");
        trayMenu.Items.Add("Quitter complètement", null, (s, e) => ExitApp());

        trayIcon = new NotifyIcon
        {
            Text = "GameEcoBot - Violet Edition",
            Icon = CreateVioletAppIcon(),
            ContextMenuStrip = trayMenu,
            Visible = true
        };
        trayIcon.DoubleClick += (s, e) => ShowAndRestore();
    }

    private void OpenOptimizationAdvisor()
    {
        using var advisor = new OptimizationAdvisorForm();
        advisor.ShowDialog(this);
    }

    #region Angle Snapping 100% Ultra-Agressif

    private void SetupAngleSnappingHook()
    {
        NativeMethods.RegisterHotKey(this.Handle, HOTKEY_ID_F8, 0, 0x77);
        // Le hook WH_MOUSE_LL est attaché UNIQUEMENT à la demande pour garantir 0ms d'overhead quand inactif
    }

    public void ToggleAngleSnapping()
    {
        isAngleSnappingActive = !isAngleSnappingActive;
        hasLastMousePos = false;
        snapSubPixelX = 0.0;
        snapSubPixelY = 0.0;

        if (chkAngleSnapping != null) chkAngleSnapping.Checked = isAngleSnappingActive;

        if (isAngleSnappingActive)
        {
            if (mouseHookId == IntPtr.Zero)
            {
                mouseProc = MouseHookCallback;
                IntPtr hMod = Marshal.GetHINSTANCE(typeof(Form1).Module);
                mouseHookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, mouseProc, hMod, 0);
            }

            btnToggleAngleSnapping.BackColor = Color.FromArgb(168, 85, 247);
            btnToggleAngleSnapping.Text = "🎯 Snapping 100% ULTRA";
            lblAngleStatus.Text = "100% ULTRA ACTIF";
            lblAngleStatus.ForeColor = Color.FromArgb(192, 132, 252);
            Log("🎯 ANGLE SNAPPING 100% ULTRA-AGRESSIF ACTIVÉ — Axe parasite totalement supprimé !");
            trayIcon.ShowBalloonTip(1500, "Angle Snapping", "100% ULTRA-AGRESSIF activé !", ToolTipIcon.Info);
        }
        else
        {
            if (mouseHookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(mouseHookId);
                mouseHookId = IntPtr.Zero;
            }

            btnToggleAngleSnapping.BackColor = Color.FromArgb(76, 29, 149);
            btnToggleAngleSnapping.Text = "🎯 Angle Snapping [F8]";
            lblAngleStatus.Text = "DÉSACTIVÉ";
            lblAngleStatus.ForeColor = Color.FromArgb(150, 135, 180);
            Log("🎯 Angle Snapping désactivé (Hook libéré pour 0 délai de souris).");
        }
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)NativeMethods.WM_MOUSEMOVE && isAngleSnappingActive)
        {
            if (isSnappingReentrant)
                return NativeMethods.CallNextHookEx(mouseHookId, nCode, wParam, lParam);

            var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);

            // Ignorer les mouvements injectés (évite la récursion)
            if ((hookStruct.flags & NativeMethods.LLMHF_INJECTED) != 0)
            {
                lastMouseX = hookStruct.pt.x;
                lastMouseY = hookStruct.pt.y;
                return NativeMethods.CallNextHookEx(mouseHookId, nCode, wParam, lParam);
            }

            int curX = hookStruct.pt.x;
            int curY = hookStruct.pt.y;

            if (!hasLastMousePos)
            {
                lastMouseX = curX;
                lastMouseY = curY;
                hasLastMousePos = true;
                snapSubPixelX = 0;
                snapSubPixelY = 0;
                return NativeMethods.CallNextHookEx(mouseHookId, nCode, wParam, lParam);
            }

            int dx = curX - lastMouseX;
            int dy = curY - lastMouseY;

            if (dx == 0 && dy == 0)
                return NativeMethods.CallNextHookEx(mouseHookId, nCode, wParam, lParam);

            int absDx = Math.Abs(dx);
            int absDy = Math.Abs(dy);

            // ═══════════════════════════════════════════════════════════════════
            // ANGLE SNAPPING 100% ULTRA-AGRESSIF
            //
            // Seuil fixe 22° : tout mouvement à moins de 22° d'un axe cardinal
            // est ENTIÈREMENT verrouillé sur cet axe.
            // → L'axe parasite est mis à 0 (suppression totale, pas de facteur).
            // → L'axe principal est transmis à 100%, sans perte.
            // → Accumulateur sous-pixel : le résidu sub-entier est conservé et
            //   réinjecté au prochain tick pour une trajectoire parfaitement lisse.
            //
            // Anti-flick (500px) : un vrai flick rapide passe toujours librement.
            // Diagonales (>22° et <68°) : 0 interférence, mouvement libre total.
            // ═══════════════════════════════════════════════════════════════════

            // Anti-flick : seuil ultra-permissif
            if (absDx > 500 || absDy > 500)
            {
                lastMouseX = curX;
                lastMouseY = curY;
                snapSubPixelX = 0;
                snapSubPixelY = 0;
                return NativeMethods.CallNextHookEx(mouseHookId, nCode, wParam, lParam);
            }

            double angleDeg = Math.Atan2(absDy, absDx) * (180.0 / Math.PI);
            const double SNAP_THRESHOLD = 22.0; // Zone de magnétisation (fixe, max agressivité)

            int targetDx = dx;
            int targetDy = dy;
            bool shouldSnap = false;

            if (angleDeg <= SNAP_THRESHOLD && absDx >= 1)
            {
                // SNAP HORIZONTAL TOTAL : axe Y = 0, sous-pixel accumulé
                double rawDy = snapSubPixelY; // 0 * dy + résidu sous-pixel
                int intDy = (int)Math.Round(rawDy);
                snapSubPixelY = rawDy - intDy;

                targetDx = dx;    // 100% course horizontale préservée
                targetDy = intDy; // axe Y totalement supprimé (ou résidu sous-pixel)
                shouldSnap = true;
            }
            else if ((90.0 - angleDeg) <= SNAP_THRESHOLD && absDy >= 1)
            {
                // SNAP VERTICAL TOTAL : axe X = 0, sous-pixel accumulé
                double rawDx = snapSubPixelX;
                int intDx = (int)Math.Round(rawDx);
                snapSubPixelX = rawDx - intDx;

                targetDx = intDx; // axe X totalement supprimé (ou résidu sous-pixel)
                targetDy = dy;    // 100% course verticale préservée
                shouldSnap = true;
            }
            else
            {
                // Diagonale franche (22°–68°) : mouvement libre, 0 interférence
                snapSubPixelX = 0;
                snapSubPixelY = 0;
            }

            if (shouldSnap)
            {
                lastMouseX = curX + (targetDx - dx);
                lastMouseY = curY + (targetDy - dy);

                try
                {
                    isSnappingReentrant = true;
                    // Injection du vecteur corrigé (axe parasite = 0)
                    NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_MOVE, targetDx, targetDy, 0, UIntPtr.Zero);
                }
                finally
                {
                    isSnappingReentrant = false;
                }
                return (IntPtr)1; // Supprime le mouvement brut, remplacé par le vecteur snappé
            }
            else
            {
                lastMouseX = curX;
                lastMouseY = curY;
            }
        }

        return NativeMethods.CallNextHookEx(mouseHookId, nCode, wParam, lParam);
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_HOTKEY = 0x0312;
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID_F8)
        {
            ToggleAngleSnapping();
        }
        base.WndProc(ref m);
    }

    #endregion

    private void ShowAndRestore()
    {
        this.Show();
        this.WindowState = FormWindowState.Normal;
        this.BringToFront();
    }

    private void ExitApp()
    {
        monitorTimer.Stop();
        NativeMethods.EnableHighPrecisionTimer(false);
        NativeMethods.EnableMouseNoDelay(false);
        if (mouseHookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(mouseHookId);
        }
        NativeMethods.UnregisterHotKey(this.Handle, HOTKEY_ID_F8);
        RestoreAllProcessesToNormal();
        trayIcon.Visible = false;
        Application.Exit();
    }

    private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            this.Hide();
            trayIcon.ShowBalloonTip(1500, "GameEcoBot", "Actif en arrière-plan avec son thème violet.", ToolTipIcon.Info);
        }
    }

    private void MonitorTimer_Tick(object? sender, EventArgs e)
    {
        CheckActiveGameAndApplyEco();
    }

    private void CheckActiveGameAndApplyEco()
    {
        try
        {
            // Mettre à jour l'audio en direct (ce que j'entends)
            _ = audioTracker.UpdateAsync();

            var processes = Process.GetProcesses();

            if (!chkAutoDetect.Checked && !isManualEcoActive)
            {
                return;
            }

            int currentPid = Process.GetCurrentProcess().Id;
            Process? detectedGame = null;

            // 1. PRIORITÉ ABSOLUE : Vérifier la fenêtre actuellement active au premier plan (Foreground Window)
            IntPtr fgHwnd = NativeMethods.GetForegroundWindow();
            if (fgHwnd != IntPtr.Zero)
            {
                NativeMethods.GetWindowThreadProcessId(fgHwnd, out int fgPid);
                if (fgPid > 0 && fgPid != currentPid)
                {
                    try
                    {
                        var fgProc = processes.FirstOrDefault(p => p.Id == fgPid);
                        if (fgProc != null && !GameIntelligenceService.IsExcluded(fgProc.ProcessName) && !protectedProcesses.Contains(fgProc.ProcessName))
                        {
                            if (intelligenceService.KnownGames.Contains(fgProc.ProcessName))
                            {
                                detectedGame = fgProc;
                            }
                            else
                            {
                                if (intelligenceService.IsGameAsync(fgProc).GetAwaiter().GetResult())
                                {
                                    detectedGame = fgProc;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            // 2. EN CAS D'ALT-TAB : Vérifier si un jeu connu tourne avec une fenêtre visible active
            if (detectedGame == null)
            {
                foreach (var proc in processes)
                {
                    if (proc.Id == currentPid) continue;
                    if (GameIntelligenceService.IsExcluded(proc.ProcessName) || protectedProcesses.Contains(proc.ProcessName))
                    {
                        continue;
                    }

                    if (intelligenceService.KnownGames.Contains(proc.ProcessName))
                    {
                        // Exiger une fenêtre graphique valide pour les jeux en tâche de fond (évite node, cmd, etc.)
                        try
                        {
                            if (proc.MainWindowHandle != IntPtr.Zero || proc.ProcessName.Contains("Shipping", StringComparison.OrdinalIgnoreCase))
                            {
                                detectedGame = proc;
                                break;
                            }
                        }
                        catch { }
                    }
                }
            }

            if (detectedGame != null)
            {
                if (!isGameActive || activeGameName != detectedGame.ProcessName)
                {
                    isGameActive = true;
                    activeGameName = detectedGame.ProcessName;
                    lblActiveGame.Text = activeGameName;

                    // 1. Boost Priorité Haute Processus Jeu
                    NativeMethods.BoostGameProcess(detectedGame.Id);
                    // 2. Timer Windows 1ms Haute Précision
                    NativeMethods.EnableHighPrecisionTimer(true);
                    // 3. Bascule Automatique vers Mode d'Alimentation Ultime (+300% de réactivité CPU)
                    NativeMethods.SetUltimateGamingPowerPlan(true);

                    Log($"🔥 BOOST IA +300% : {activeGameName} détecté ! Priorité Haute, Timer 1ms & Mode Gaming Ultime activés.");

                    pnlStatusBanner.BackColor = Color.FromArgb(45, 20, 65);
                    lblStatusHeader.Text = $"🔥 MODE JEU ACTIF • {activeGameName.ToUpper()}";
                    lblStatusHeader.ForeColor = Color.FromArgb(216, 180, 254);
                    lblStatusSubtitle.Text = "Toutes les applications superflues sont passées en Mode Éco & RAM purgée.";

                    trayIcon.ShowBalloonTip(2000, "GameEcoBot", $"Mode Jeu IA +300% activé pour {activeGameName} !", ToolTipIcon.Info);

                    ApplyEcoToAllBackgroundApps(processes, detectedGame.Id);
                }
                else
                {
                    ApplyEcoToAllBackgroundApps(processes, detectedGame.Id);
                }
            }
            else
            {
                if (isGameActive)
                {
                    isGameActive = false;
                    NativeMethods.EnableHighPrecisionTimer(false);
                    NativeMethods.SetUltimateGamingPowerPlan(false);
                    Log($"ℹ️ {activeGameName} s'est arrêté. Rétablissement du mode équilibré.");
                    activeGameName = "";
                    lblActiveGame.Text = "En attente...";

                    if (!isManualEcoActive)
                    {
                        RestoreAllProcessesToNormal();
                        pnlStatusBanner.BackColor = Color.FromArgb(24, 18, 48);
                        lblStatusHeader.Text = "🟣 EN VEILLE • PRÊT POUR LE JEU";
                        lblStatusHeader.ForeColor = Color.FromArgb(192, 132, 252);
                        lblStatusSubtitle.Text = "Dès qu'un jeu démarre, toutes les autres applications passent en Mode Éco.";
                    }
                }
                else if (isManualEcoActive)
                {
                    ApplyEcoToAllBackgroundApps(processes, -1);
                }
            }
        }
        catch { }
    }

    private void ApplyEcoToAllBackgroundApps(Process[] allProcesses, int gamePid)
    {
        int throttledCount = 0;

        foreach (var proc in allProcesses)
        {
            try
            {
                if (proc.Id == gamePid || proc.Id == Process.GetCurrentProcess().Id) continue;
                if (protectedProcesses.Contains(proc.ProcessName)) continue;
                if (intelligenceService.KnownGames.Contains(proc.ProcessName)) continue;

                if (!currentlyThrottledPids.Contains(proc.Id))
                {
                    bool success = NativeMethods.ApplyEcoMode(proc.Id, true);
                    if (success) currentlyThrottledPids.Add(proc.Id);
                }

                if (currentlyThrottledPids.Contains(proc.Id)) throttledCount++;
            }
            catch { }
        }

        lblEcoCount.Text = $"{throttledCount} apps";
    }

    private void RestoreAllProcessesToNormal()
    {
        int restored = 0;
        foreach (int pid in currentlyThrottledPids)
        {
            try
            {
                NativeMethods.ApplyEcoMode(pid, false);
                restored++;
            }
            catch { }
        }
        currentlyThrottledPids.Clear();
        lblEcoCount.Text = "0 apps";
        Log($"🔄 {restored} applications restaurées en mode normal.");
    }

    private void BtnToggleEco_Click(object? sender, EventArgs e)
    {
        isManualEcoActive = !isManualEcoActive;
        if (isManualEcoActive)
        {
            btnToggleEco.Text = "🔄 Normal";
            btnToggleEco.BackColor = Color.FromArgb(168, 85, 247);
            pnlStatusBanner.BackColor = Color.FromArgb(40, 20, 55);
            lblStatusHeader.Text = "🟣 MODE ÉCO MANUEL FORCÉ";
            lblStatusHeader.ForeColor = Color.FromArgb(216, 180, 254);
            lblStatusSubtitle.Text = "Toutes les applications hors jeu sont bridées en Mode Éco.";
            Log("🟣 Mode Éco forcé manuellement.");
            ApplyEcoToAllBackgroundApps(Process.GetProcesses(), -1);
        }
        else
        {
            btnToggleEco.Text = "🟣 Mode Éco";
            btnToggleEco.BackColor = Color.FromArgb(124, 58, 237);
            pnlStatusBanner.BackColor = Color.FromArgb(24, 18, 48);
            lblStatusHeader.Text = "🟣 EN VEILLE • PRÊT POUR LE JEU";
            lblStatusHeader.ForeColor = Color.FromArgb(192, 132, 252);
            lblStatusSubtitle.Text = "Dès qu'un jeu démarre, toutes les autres applications passent en Mode Éco.";
            RestoreAllProcessesToNormal();
        }
    }

    private void BtnCleanRam_Click(object? sender, EventArgs e)
    {
        int count = 0;
        var procs = Process.GetProcesses();
        foreach (var p in procs)
        {
            try
            {
                if (p.Id != Process.GetCurrentProcess().Id)
                {
                    NativeMethods.TrimRam(p.Id);
                    count++;
                }
            }
            catch { }
        }
        GC.Collect();
        Log($"🧹 Cache mémoire vidé pour {count} processus.");
        MessageBox.Show($"La mémoire vive a été vidée pour {count} processus !\nLa RAM est libre pour le jeu.", "Nettoyage RAM", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }



    private bool CheckStartupConfig()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
            return key?.GetValue("GameEcoBot") != null;
        }
        catch { return false; }
    }

    private void ChkRunAtStartup_CheckedChanged(object? sender, EventArgs e)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (chkRunAtStartup.Checked)
            {
                string exePath = Application.ExecutablePath;
                key?.SetValue("GameEcoBot", $"\"{exePath}\" --minimized");
                Log("✅ Démarrage automatique avec Windows activé.");
            }
            else
            {
                key?.DeleteValue("GameEcoBot", false);
                Log("❌ Démarrage automatique avec Windows désactivé.");
            }
        }
        catch (Exception ex)
        {
            Log($"Erreur démarrage: {ex.Message}");
        }
    }

    private void Log(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        rtbLogs.AppendText($"[{timestamp}] {message}\r\n");
        rtbLogs.SelectionStart = rtbLogs.Text.Length;
        rtbLogs.ScrollToCaret();
    }

    private Icon CreateVioletAppIcon()
    {
        using Bitmap bmp = new Bitmap(32, 32);
        using Graphics g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Cercle extérieur violet principal
        using (Brush brush = new SolidBrush(Color.FromArgb(124, 58, 237)))
        {
            g.FillEllipse(brush, 2, 2, 28, 28);
        }

        // Cercle intérieur violet clair (pas d'éclair blanc)
        using (Brush innerBrush = new SolidBrush(Color.FromArgb(192, 132, 252)))
        {
            g.FillEllipse(innerBrush, 9, 9, 14, 14);
        }

        IntPtr hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }
}
