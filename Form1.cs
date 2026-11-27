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
    private bool isDetectionRunning = false;
    private string activeGameName = "";
    private HashSet<int> currentlyThrottledPids = new();

    // Ultra Eco Manager (0% CPU/GPU background) et Service Cleaner
    private UltraEcoManager ultraEcoManager;
    private bool isUltraEcoActive = false;
    private bool isServiceCleanActive = false;
    private ModernButton btnUltraEco;
    private ModernButton btnServiceClean;
    private Label lblUltraEcoStatus;
    private Label lblServiceStatus;

    // Service de détection des jeux réels
    private GameIntelligenceService intelligenceService;

    // Suivi de ce que l'utilisateur entend (Audio & Lecteur)
    private AudioSessionTracker audioTracker;
    private Label lblSoundTitle;
    private Label lblSoundArtist;

    // Angle Snapping ULTRA avec pourcentage réglable (1% à 100%)
    // Angle Snapping ULTRA avec pourcentage réglable (1% à 100%)
    private bool isAngleSnappingActive = false;
    private int angleSnappingPercent = 100;
    private IntPtr mouseHookId = IntPtr.Zero;
    private NativeMethods.LowLevelMouseProc? mouseProc;
    private int lastMouseX = 0;
    private int lastMouseY = 0;
    private bool hasLastMousePos = false;
    private const int HOTKEY_ID_F8 = 9001;
    private const int HOTKEY_ID_F9 = 9002;
    private const int HOTKEY_ID_F10 = 9003;
    private Thread? angleHookThread = null;
    private uint angleHookThreadId = 0;
    private volatile bool isSnappingInjected = false;

    // Clavier SOCD (Snap Tap / Rapid Tap) [F10]
    private readonly SocdKeyboardService socdService = new();
    private ModernToggleSwitch chkSocd;
    private ModernToggleSwitch chkSocdVertical;
    private ModernButton btnSocdAzerty;
    private ModernButton btnSocdQwerty;
    private Label lblSocdStatus;

    // Viseur Crosshair Compétitif (Overlay au-dessus des jeux)
    private Label lblCrosshairStatus;
    private ModernButton btnToggleCrosshair;
    private ModernToggleSwitch chkCrosshair;
    private CrosshairPreviewControl crosshairPreview;
    private Label lblCrosshairSizeVal;
    private Label lblCrosshairThickVal;
    private Label lblCrosshairGapVal;
    private ModernCard pnlCrosshairCard;

    // Contrôles d'interface haute fidélité (Pixel par pixel)
    private Label lblStatusHeader;
    private Label lblStatusDetail;
    private ModernCard pnlStatusBanner;
    private Label lblEcoCount;
    private Label lblActiveGame;
    private Label lblAngleStatus;
    private Label lblCpuValue;
    private Label lblRamValue;
    private Label lblGpuValue;
    private Panel? pnlCpuProgressTrack;
    private Panel? pnlCpuProgressFill;
    private Panel? pnlRamProgressTrack;
    private Panel? pnlRamProgressFill;
    private Panel? pnlGpuProgressTrack;
    private Panel? pnlGpuProgressFill;
    private RichTextBox rtbLogs;
    private ModernToggleSwitch chkAutoDetect;
    private ModernToggleSwitch chkMouseNoDelay;
    private ModernToggleSwitch chkRunAtStartup;
    private ModernToggleSwitch chkAutoTurbo;
    private ModernToggleSwitch chkAngleSnapping;
    private ModernSlider tbAngleSnapping;
    private Label lblAnglePercentValue;
    private ModernButton btnSearchGames;
    private ModernButton btnToggleEco;
    private ModernButton btnCleanRam;
    private ModernButton btnToggleAngleSnapping;
    private TaskManagerWebControl hardwareGraph;
    private ModernButton btnGraphCpu;
    private ModernButton btnGraphRam;
    private ModernButton btnGraphApps;
    private ModernButton btnGraphGpu;

    // Vérification rapide du jeu affiché au premier plan.
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

        ultraEcoManager = new UltraEcoManager(protectedProcesses, intelligenceService);

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

        // Lancement automatique de Spotify en mode minimisé dès l'ouverture
        _ = Task.Run(() => SpotifyLauncher.StartSpotifyMinimized());

        BuildAllVioletUi();
        SetupTray();
        // Restaure un plan laissé actif par une ancienne version ou un arrêt inattendu.
        bool startupPlanRestored = NativeMethods.RestoreNormalPowerPlan();
        if (!startupPlanRestored) Log("[ATTENTION] Windows n'a pas pu restaurer le plan normal.");

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

        Log("[OK] GameEcoBot démarré.");
        Log("[SYSTEME] Configuration active.");
        Log("[INFO] Raccourci F8 configuré pour l'Angle Snapping.");
        Log("[RESEAU] Optimisation réseau active.");

        // Démarrage du Wallpaper Engine (fond d'écran vidéo/photo avec pause auto en jeu)
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500);
            if (!this.IsDisposed && this.IsHandleCreated)
            {
                this.Invoke(() => _ = WallpaperEngineService.Instance.InitializeOnStartupAsync());
            }
        });
    }

    private async void FastDetectionTimer_Tick(object? sender, EventArgs e)
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
                    if (!GameIntelligenceService.IsExcluded(pName))
                    {
                        if (intelligenceService.KnownGames.Contains(pName))
                        {
                            if (!isGameActive || activeGameName != pName)
                            {
                                await CheckActiveGameAndApplyEcoAsync();
                            }
                        }
                        else
                        {
                            if (await Task.Run(() => intelligenceService.IsGameAsync(fgProc)))
                            {
                                if (!isGameActive || activeGameName != pName)
                                {
                                    await CheckActiveGameAndApplyEcoAsync();
                                }
                            }
                        }
                    }
                }
                catch { }
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
        }
        else
        {
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
            this.Activate();
        }

        Log("[SOURIS] Latence souris réduite (Timer 1ms).");
        _ = audioTracker.UpdateAsync();
    }

    private void BuildAllVioletUi()
    {
        Rectangle workArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 960);
        this.Size = new Size(Math.Min(1280, workArea.Width), Math.Min(960, workArea.Height));
        this.MinimumSize = new Size(Math.Min(1050, workArea.Width), Math.Min(760, workArea.Height));
        this.BackColor = Color.FromArgb(12, 10, 24);
        this.ForeColor = Color.White;
        this.Font = AppFonts.GetBodyFont(9.5f);
        this.Icon = CreateVioletAppIcon();
        this.StartPosition = FormStartPosition.CenterScreen;
        this.ShowInTaskbar = true;

        const int sidebarWidth = 220;
        const int headerHeight = 64;
        int contentWidth = this.ClientSize.Width - sidebarWidth;

        Panel pnlSidebar = new Panel
        {
            Location = Point.Empty,
            Size = new Size(sidebarWidth, this.ClientSize.Height),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left,
            BackColor = Color.FromArgb(10, 8, 18)
        };
        DarkScrollPanel pnlMainContent = new DarkScrollPanel
        {
            Location = new Point(sidebarWidth, headerHeight),
            Size = new Size(contentWidth, this.ClientSize.Height - headerHeight),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.FromArgb(12, 10, 24),
            AutoScrollMinSize = new Size(0, 2840)
        };

        // ══════════════════════════════════════════════════════════
        // 1. HEADER BAR — Widget Musique & Contrôles Fenêtre
        // ══════════════════════════════════════════════════════════
        Panel pnlHeader = new Panel
        {
            Location = new Point(sidebarWidth, 0),
            Size = new Size(contentWidth, headerHeight),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.FromArgb(19, 15, 36),
            Padding = new Padding(0)
        };
        MakeDraggable(pnlHeader);

        // ── Widget musique style Spotify (identique à la photo de référence) ──
        Panel pnlSoundWidget = new Panel
        {
            Location = new Point(20, 7),
            Size = new Size(500, 48),
            BackColor = Color.Transparent,
        };
        MakeDraggable(pnlSoundWidget);

        // Titre de musique (blanc pur, gras, style Spotify)
        lblSoundTitle = new Label
        {
            Text = "Aucune lecture",
            Location = new Point(4, 6),
            Size = new Size(160, 18),
            ForeColor = Color.White,
            Font = AppFonts.GetBoldFont(9.5f),
            AutoEllipsis = true,
            BackColor = Color.Transparent
        };
        MakeDraggable(lblSoundTitle);

        // Artiste / Source (gris clair discret, style Spotify)
        lblSoundArtist = new Label
        {
            Text = "Spotify",
            Location = new Point(4, 25),
            Size = new Size(160, 16),
            ForeColor = Color.FromArgb(170, 170, 170),
            Font = AppFonts.GetBodyFont(8.2f),
            AutoEllipsis = true,
            BackColor = Color.Transparent
        };
        MakeDraggable(lblSoundArtist);

        // Label source (gris discret)
        lblSoundSource = new Label
        {
            Text = "Spotify",
            Location = new Point(174, 16),
            Size = new Size(80, 16),
            ForeColor = Color.FromArgb(160, 160, 160),
            Font = AppFonts.GetBodyFont(7.5f, FontStyle.Italic),
            AutoEllipsis = true,
            BackColor = Color.Transparent
        };
        MakeDraggable(lblSoundSource);

        // Bouton Précédent (triangle + barre discret)
        btnSpotifyPrev = new SpotifyTrackButton
        {
            ButtonType = SpotifyTrackButtonType.Previous,
            Location = new Point(260, 10),
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
            Location = new Point(294, 7),
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
            Location = new Point(334, 10),
            Size = new Size(28, 28)
        };
        btnSpotifyNext.Click += async (s, e) =>
        {
            await audioTracker.SkipNextAsync();
            SendMediaKey(NativeMethods.APPCOMMAND_MEDIA_NEXTTRACK);
        };

        pnlSoundWidget.Controls.Add(lblSoundTitle);
        pnlSoundWidget.Controls.Add(lblSoundArtist);
        pnlSoundWidget.Controls.Add(lblSoundSource);
        pnlSoundWidget.Controls.Add(btnSpotifyPrev);
        pnlSoundWidget.Controls.Add(btnSpotifyPlayPause);
        pnlSoundWidget.Controls.Add(btnSpotifyNext);
        pnlHeader.Controls.Add(pnlSoundWidget);

        // Boutons fenetre circulaires a droite
        var btnMinimize = new ModernCircleButton
        {
            IconText = "-",
            Size = new Size(28, 28),
            Location = new Point(pnlHeader.ClientSize.Width - 108, 18),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            NormalBg = Color.FromArgb(24, 24, 24),
            HoverBg = Color.FromArgb(48, 48, 48),
            NormalFg = Color.FromArgb(180, 180, 180),
            HoverFg = Color.White
        };
        btnMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;

        var btnMaximize = new ModernCircleButton
        {
            IconText = "+",
            Size = new Size(28, 28),
            Location = new Point(pnlHeader.ClientSize.Width - 73, 18),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            NormalBg = Color.FromArgb(24, 24, 24),
            HoverBg = Color.FromArgb(48, 48, 48),
            NormalFg = Color.FromArgb(180, 180, 180),
            HoverFg = Color.White
        };
        btnMaximize.Click += (s, e) => this.WindowState = this.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;

        var btnClose = new ModernCircleButton
        {
            IconText = "X",
            Size = new Size(28, 28),
            Location = new Point(pnlHeader.ClientSize.Width - 38, 18),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            IsDestructive = true,
            NormalBg = Color.FromArgb(24, 24, 24),
            NormalFg = Color.FromArgb(180, 180, 180)
        };
        btnClose.Click += (s, e) =>
        {
            this.Hide();
        };

        pnlHeader.Controls.Add(btnMinimize);
        pnlHeader.Controls.Add(btnMaximize);
        pnlHeader.Controls.Add(btnClose);

        // ══════════════════════════════════════════════════════════
        // 2. Status Banner
        // ══════════════════════════════════════════════════════════
        Label lblPageEyebrow = new Label
        {
            Text = "TABLEAU DE BORD",
            Font = AppFonts.GetBoldFont(8.5f),
            ForeColor = Color.FromArgb(140, 140, 140),
            Location = new Point(24, 16),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        Label lblPageTitle = new Label
        {
            Text = "Surveillance du PC",
            Font = AppFonts.GetTitleFont(21f),
            ForeColor = Color.White,
            Location = new Point(24, 34),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlMainContent.Controls.Add(lblPageEyebrow);
        pnlMainContent.Controls.Add(lblPageTitle);

        Label lblPageSubtitle = new Label
        {
            Text = "Vue d'ensemble des performances et des commandes",
            Font = AppFonts.GetBodyFont(8.5f),
            ForeColor = Color.FromArgb(181, 168, 211),
            Location = new Point(24, 65),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlMainContent.Controls.Add(lblPageSubtitle);

        pnlStatusBanner = new ModernCard
        {
            Location = new Point(24, 84),
            Size = new Size(contentWidth - 48, 64),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BorderRadius = 12,
            FillColor = Color.FromArgb(22, 17, 44),
            BorderColor = Color.FromArgb(60, 48, 105),
            Padding = new Padding(15)
        };

        lblStatusHeader = new Label
        {
            Text = "EN VEILLE - PRET POUR LE JEU",
            Font = AppFonts.GetBoldFont(12f),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(18, 9),
            BackColor = Color.Transparent
        };

        lblStatusDetail = new Label
        {
            Text = "CPU / RAM : suivi en direct  |  GPU : non configure",
            Font = AppFonts.GetBodyFont(8f),
            ForeColor = Color.FromArgb(181, 168, 211),
            AutoSize = true,
            Location = new Point(18, 35),
            BackColor = Color.Transparent
        };

        Panel pnlStatusAccent = new Panel
        {
            Location = new Point(0, 12),
            Size = new Size(3, 40),
            BackColor = Color.FromArgb(124, 58, 237)
        };


        var btnOptimizationAdvisor = CreateVioletActionButton(
            "Optimisations",
            Color.FromArgb(28, 28, 28),
            new Point(contentWidth - 204, 14),
            140,
            (s, e) => OpenOptimizationAdvisor());
        btnOptimizationAdvisor.Size = new Size(140, 36);
        btnOptimizationAdvisor.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnOptimizationAdvisor.Font = AppFonts.GetBoldFont(8.5f);
        btnOptimizationAdvisor.NormalColor = Color.FromArgb(124, 58, 237);
        btnOptimizationAdvisor.HoverColor = Color.FromArgb(168, 85, 247);
        btnOptimizationAdvisor.PressedColor = Color.FromArgb(109, 40, 217);
        btnOptimizationAdvisor.BorderColor = Color.FromArgb(60, 48, 105);

        pnlStatusBanner.Controls.Add(pnlStatusAccent);
        pnlStatusBanner.Controls.Add(lblStatusHeader);
        pnlStatusBanner.Controls.Add(lblStatusDetail);
        pnlStatusBanner.Controls.Add(btnOptimizationAdvisor);
        pnlMainContent.Controls.Add(pnlStatusBanner);

        // ══════════════════════════════════════════════════════════

        hardwareGraph = new TaskManagerWebControl
        {
            Dock = DockStyle.Fill
        };
        hardwareGraph.OnLogMessage += (s, msg) => Log(msg);
        hardwareGraph.OnRamCleanRequested += BtnCleanRam_Click;
        hardwareGraph.OnEcoToggleRequested += BtnToggleEco_Click;
        hardwareGraph.MetricsUpdated += (s, e) => UpdateHardwareMetrics();

        Panel pnlHardwareMetrics = new Panel
        {
            Location = new Point(24, 164),
            Size = new Size(contentWidth - 48, 116),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.Transparent
        };
        ModernCard cpuCard = CreateResourceMetricCard("PROCESSEUR - CPU", "N/D", "EN DIRECT", Color.FromArgb(124, 58, 237), true, out lblCpuValue, out pnlCpuProgressTrack, out pnlCpuProgressFill);
        ModernCard ramCard = CreateResourceMetricCard("MEMOIRE - RAM", "N/D", "EN DIRECT", Color.FromArgb(168, 85, 247), true, out lblRamValue, out pnlRamProgressTrack, out pnlRamProgressFill);
        ModernCard gpuCard = CreateResourceMetricCard("CARTE GRAPHIQUE - GPU", "N/D", "NON MESURE", Color.FromArgb(181, 168, 211), false, out lblGpuValue, out pnlGpuProgressTrack, out pnlGpuProgressFill);

        void LayoutHardwareCards()
        {
            int gap = 16;
            int cardWidth = (pnlHardwareMetrics.ClientSize.Width - gap * 2) / 3;
            cpuCard.Location = new Point(0, 0);
            ramCard.Location = new Point(cardWidth + gap, 0);
            gpuCard.Location = new Point((cardWidth + gap) * 2, 0);
            cpuCard.Size = ramCard.Size = gpuCard.Size = new Size(cardWidth, 116);
        }
        pnlHardwareMetrics.Resize += (s, e) => LayoutHardwareCards();
        LayoutHardwareCards();
        pnlHardwareMetrics.Controls.Add(cpuCard);
        pnlHardwareMetrics.Controls.Add(ramCard);
        pnlHardwareMetrics.Controls.Add(gpuCard);
        pnlMainContent.Controls.Add(pnlHardwareMetrics);
        UpdateHardwareMetrics();

        // ══════════════════════════════════════════════════════════
        // 2b. Cartes Résumé d'État (4 colonnes dynamiques)
        // ══════════════════════════════════════════════════════════
        Panel pnlSummaryRow = new Panel
        {
            Location = new Point(24, 294),
            Size = new Size(contentWidth - 48, 72),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.Transparent
        };
        int summaryGap = 12;
        int summaryCardWidth = (pnlSummaryRow.ClientSize.Width - summaryGap * 3) / 4;
        ModernCard pnlCard1 = CreateMetricCard("APPS EN MODE ECO", "0 apps", Color.White, new Point(0, 0), summaryCardWidth, 72, out lblEcoCount);
        ModernCard pnlCard2 = CreateMetricCard("JEU EN COURS", "En attente...", Color.FromArgb(220, 220, 220), new Point(summaryCardWidth + summaryGap, 0), summaryCardWidth, 72, out lblActiveGame);
        ModernCard pnlCard3 = CreateMetricCard("ANGLE SNAPPING", "DESACTIVE", Color.FromArgb(180, 180, 180), new Point((summaryCardWidth + summaryGap) * 2, 0), summaryCardWidth, 72, out lblAngleStatus);
        ModernCard pnlCard4 = CreateMetricCard("VISEUR OVERLAY", "DESACTIVE", Color.FromArgb(180, 180, 180), new Point((summaryCardWidth + summaryGap) * 3, 0), summaryCardWidth, 72, out lblCrosshairStatus);
        void LayoutSummaryCards()
        {
            summaryCardWidth = (pnlSummaryRow.ClientSize.Width - summaryGap * 3) / 4;
            pnlCard1.Location = new Point(0, 0);
            pnlCard2.Location = new Point(summaryCardWidth + summaryGap, 0);
            pnlCard3.Location = new Point((summaryCardWidth + summaryGap) * 2, 0);
            pnlCard4.Location = new Point((summaryCardWidth + summaryGap) * 3, 0);
            pnlCard1.Size = pnlCard2.Size = pnlCard3.Size = pnlCard4.Size = new Size(summaryCardWidth, 72);
        }
        pnlSummaryRow.Resize += (s, e) => LayoutSummaryCards();
        LayoutSummaryCards();
        pnlSummaryRow.Controls.Add(pnlCard1);
        pnlSummaryRow.Controls.Add(pnlCard2);
        pnlSummaryRow.Controls.Add(pnlCard3);
        pnlSummaryRow.Controls.Add(pnlCard4);
        pnlMainContent.Controls.Add(pnlSummaryRow);

        // (La section Viseur Crosshair X Studio complète avec ses 238 presets est construite et ajoutée plus bas avant Wallpaper Engine)

        // ══════════════════════════════════════════════════════════
        // 4. Section Header des Graphiques avec Pills CPU / RAM / GPU / APPS
        // ══════════════════════════════════════════════════════════
        Panel pnlGraphHeader = new Panel
        {
            Location = new Point(24, 390),
            Size = new Size(contentWidth - 48, 36),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.Transparent
        };

        Label lblGraphTitle = new Label
        {
            Text = "GRAPHIQUES DE PERFORMANCES EN DIRECT",
            Font = AppFonts.GetBoldFont(8.5f),
            ForeColor = Color.FromArgb(140, 140, 140),
            Location = new Point(0, 8),
            AutoSize = true,
            BackColor = Color.Transparent
        };

        Panel pnlPills = new Panel
        {
            Location = new Point(pnlGraphHeader.ClientSize.Width - 310, 0),
            Size = new Size(310, 34),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.Transparent,
            Visible = true
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

        pnlGraphHeader.Controls.Add(lblGraphTitle);
        pnlGraphHeader.Controls.Add(pnlPills);
        pnlMainContent.Controls.Add(pnlGraphHeader);

        // Hardware Graph Container (Task Manager Fluent Dark)
        ModernCard pnlGraphContainer = new ModernCard
        {
            Location = new Point(24, 430),
            Size = new Size(contentWidth - 48, 480),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BorderRadius = 12,
            FillColor = Color.FromArgb(14, 14, 14),
            BorderColor = Color.FromArgb(38, 38, 38),
            Padding = new Padding(2)
        };
        pnlGraphContainer.Controls.Add(hardwareGraph);
        pnlMainContent.Controls.Add(pnlGraphContainer);

        // ══════════════════════════════════════════════════════════
        // 5. Action Buttons (Boutons modernes avec micro-interactions)
        // ══════════════════════════════════════════════════════════
        Panel pnlActionsRow = new Panel
        {
            Location = new Point(24, 926),
            Size = new Size(contentWidth - 48, 68),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.Transparent
        };
        Label lblActionsTitle = new Label
        {
            Text = "ACTIONS RAPIDES",
            Font = AppFonts.GetBoldFont(8.5f),
            ForeColor = Color.FromArgb(181, 168, 211),
            Location = new Point(0, 0),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlActionsRow.Controls.Add(lblActionsTitle);

        int actionGap = 10;
        int actBtnW = (pnlActionsRow.ClientSize.Width - actionGap * 4) / 5;

        btnSearchGames = CreateVioletActionButton("Recherche de jeu", Color.FromArgb(28, 28, 28), new Point(0, 22), actBtnW, async (s, e) =>
        {
            btnSearchGames.Enabled = false;
            btnSearchGames.Text = "Scan IA en cours...";
            int total = await intelligenceService.BoostIntelligenceAsync();
            btnSearchGames.Text = $"{total} jeux trouves";
            await Task.Delay(2500);
            btnSearchGames.Text = "Recherche de jeu";
            btnSearchGames.Enabled = true;
        });

        btnToggleEco = CreateVioletActionButton("Activer Mode Eco", Color.FromArgb(28, 28, 28), new Point(actBtnW + actionGap, 22), actBtnW, BtnToggleEco_Click);
        btnCleanRam = CreateVioletActionButton("Vider la RAM", Color.FromArgb(28, 28, 28), new Point((actBtnW + actionGap) * 2, 22), actBtnW, BtnCleanRam_Click);
        btnToggleAngleSnapping = CreateVioletActionButton("Angle Snapping [F8]", Color.FromArgb(28, 28, 28), new Point((actBtnW + actionGap) * 3, 22), actBtnW, (s, e) => ToggleAngleSnapping());
        btnToggleCrosshair = CreateVioletActionButton("Viseur [F9]", Color.FromArgb(28, 28, 28), new Point((actBtnW + actionGap) * 4, 22), actBtnW, (s, e) => ToggleCrosshair());

        void LayoutActionButtons()
        {
            actBtnW = (pnlActionsRow.ClientSize.Width - actionGap * 4) / 5;
            btnSearchGames.Location = new Point(0, 22);
            btnToggleEco.Location = new Point(actBtnW + actionGap, 22);
            btnCleanRam.Location = new Point((actBtnW + actionGap) * 2, 22);
            btnToggleAngleSnapping.Location = new Point((actBtnW + actionGap) * 3, 22);
            btnToggleCrosshair.Location = new Point((actBtnW + actionGap) * 4, 22);
            btnSearchGames.Size = btnToggleEco.Size = btnCleanRam.Size = btnToggleAngleSnapping.Size = btnToggleCrosshair.Size = new Size(actBtnW, 42);
        }
        pnlActionsRow.Resize += (s, e) => LayoutActionButtons();
        LayoutActionButtons();
        pnlActionsRow.Controls.Add(btnSearchGames);
        pnlActionsRow.Controls.Add(btnToggleEco);
        pnlActionsRow.Controls.Add(btnCleanRam);
        pnlActionsRow.Controls.Add(btnToggleAngleSnapping);
        pnlActionsRow.Controls.Add(btnToggleCrosshair);
        pnlMainContent.Controls.Add(pnlActionsRow);

        // ══════════════════════════════════════════════════════════
        // 6. Section Angle Snapping avec Curseur Moderne
        // ══════════════════════════════════════════════════════════
        ModernCard pnlSnappingSettings = new ModernCard
        {
            Location = new Point(24, 1010),
            Size = new Size(contentWidth - 48, 62),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BorderRadius = 10,
            FillColor = Color.FromArgb(22, 17, 44),
            BorderColor = Color.FromArgb(60, 48, 105),
            Padding = new Padding(10)
        };

        chkAngleSnapping = new ModernToggleSwitch
        {
            Title = "Angle Snapping [F8]",
            Checked = false,
            Location = new Point(12, 9),
            Size = new Size(320, 36),
            Font = AppFonts.GetFont(9.5f)
        };
        chkAngleSnapping.CheckedChanged += (s, e) =>
        {
            if (chkAngleSnapping.Checked != isAngleSnappingActive) ToggleAngleSnapping();
        };

        Label lblSliderTitle = new Label
        {
            Text = "Force :",
            Location = new Point(340, 18),
            AutoSize = true,
            ForeColor = Color.FromArgb(150, 150, 150),
            Font = AppFonts.GetBodyFont(9f),
            BackColor = Color.Transparent
        };

        lblAnglePercentValue = new Label
        {
            Text = "100%",
            Location = new Point(415, 16),
            AutoSize = true,
            ForeColor = Color.White,
            Font = AppFonts.GetBoldFont(10.5f),
            BackColor = Color.Transparent
        };

        tbAngleSnapping = new ModernSlider
        {
            Location = new Point(470, 18),
            Size = new Size(pnlSnappingSettings.ClientSize.Width - 490, 26),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Minimum = 5,
            Maximum = 100,
            Value = 100
        };
        tbAngleSnapping.ValueChanged += (s, e) =>
        {
            angleSnappingPercent = tbAngleSnapping.Value;
            lblAnglePercentValue.Text = $"{angleSnappingPercent}%";
            if (isAngleSnappingActive)
            {
                lblAngleStatus.Text = $"{angleSnappingPercent}% ACTIF";
                btnToggleAngleSnapping.Text = $"Snapping ({angleSnappingPercent}%)";
            }
        };

        pnlSnappingSettings.Controls.Add(chkAngleSnapping);
        pnlSnappingSettings.Controls.Add(lblSliderTitle);
        pnlSnappingSettings.Controls.Add(lblAnglePercentValue);
        pnlSnappingSettings.Controls.Add(tbAngleSnapping);
        pnlMainContent.Controls.Add(pnlSnappingSettings);

        ModernCard pnlAutoSettings = new ModernCard
        {
            Location = new Point(24, 1088),
            Size = new Size(contentWidth - 48, 96),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BorderRadius = 10,
            FillColor = Color.FromArgb(16, 16, 16),
            BorderColor = Color.FromArgb(40, 40, 40),
            Padding = new Padding(8)
        };

        chkAutoDetect = new ModernToggleSwitch
        {
            Title = "Mode Eco Automatique",
            Checked = true,
            Location = new Point(12, 6),
            Size = new Size(370, 34),
            Font = AppFonts.GetFont(9.5f)
        };

        chkMouseNoDelay = new ModernToggleSwitch
        {
            Title = "Souris No Delay (0ms)",
            Checked = true,
            Location = new Point(480, 8),
            Size = new Size(pnlAutoSettings.ClientSize.Width - 500, 34),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        chkMouseNoDelay.CheckedChanged += (s, e) =>
        {
            NativeMethods.EnableMouseNoDelay(chkMouseNoDelay.Checked);
            Log(chkMouseNoDelay.Checked
                ? "[SOURIS] NO DELAY SOURIS ACTIVÉ (Timer 1ms + Suivi 1:1 Raw)."
                : "[SOURIS] No Delay Souris désactivé.");
        };

        chkRunAtStartup = new ModernToggleSwitch
        {
            Title = "Démarrer avec Windows",
            Checked = CheckStartupConfig(),
            Location = new Point(12, 50),
            Size = new Size(420, 34)
        };
        chkRunAtStartup.CheckedChanged += ChkRunAtStartup_CheckedChanged;

        chkAutoTurbo = new ModernToggleSwitch
        {
            Title = "Turbo CPU en jeu",
            Checked = true,
            Location = new Point(480, 50),
            Size = new Size(pnlAutoSettings.ClientSize.Width - 500, 34),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Font = AppFonts.GetFont(9.5f)
        };
        chkAutoTurbo.CheckedChanged += (s, e) =>
        {
            if (chkAutoTurbo.Checked)
            {
                if (isGameActive)
                {
                    bool activated = NativeMethods.SetUltimateGamingPowerPlan(true);
                    lblStatusHeader.Text = $"MODE JEU ACTIF - {activeGameName.ToUpper()}";
                    lblStatusHeader.ForeColor = Color.White;
                    Log(activated ? $"Plan de jeu activé pour {activeGameName}." : "[ATTENTION] Windows n'a pas pu activer le plan de jeu.");
                }
                else
                {
                    Log("Turbo CPU activé uniquement pendant les jeux détectés.");
                }
            }
            else
            {
                bool restored = NativeMethods.RestoreNormalPowerPlan();
                Log(restored ? "Turbo CPU en jeu désactivé; plan normal restauré." : "[ATTENTION] Windows n'a pas pu restaurer le plan normal.");
                if (isGameActive)
                {
                    lblStatusHeader.Text = $"{activeGameName.ToUpper()} - CPU NORMAL";
                    lblStatusHeader.ForeColor = Color.FromArgb(180, 180, 180);
                }
            }
        };

        pnlAutoSettings.Controls.Add(chkAutoDetect);
        pnlAutoSettings.Controls.Add(chkMouseNoDelay);
        pnlAutoSettings.Controls.Add(chkRunAtStartup);
        pnlAutoSettings.Controls.Add(chkAutoTurbo);
        pnlMainContent.Controls.Add(pnlAutoSettings);

        // ══════════════════════════════════════════════════════════
        // 7.5 GESTION DES PROCESSUS ET SERVICES
        // ══════════════════════════════════════════════════════════
        ModernCard pnlExtremeCard = new ModernCard
        {
            Location = new Point(24, 1200),
            Size = new Size(contentWidth - 48, 130),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BorderRadius = 10,
            FillColor = Color.FromArgb(16, 16, 16),
            BorderColor = Color.FromArgb(40, 40, 40),
            Padding = new Padding(14)
        };

        Label lblExtremeTitle = new Label
        {
            Text = "GESTION DES PROCESSUS ET SERVICES",
            Font = AppFonts.GetBoldFont(8.5f),
            ForeColor = Color.White,
            Location = new Point(14, 10),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlExtremeCard.Controls.Add(lblExtremeTitle);

        Label lblExtremeDesc = new Label
        {
            Text = "Baisse la priorité des applications en arrière-plan pendant le jeu.",
            Font = AppFonts.GetBodyFont(7.8f),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(14, 28),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlExtremeCard.Controls.Add(lblExtremeDesc);

        lblUltraEcoStatus = new Label
        {
            Text = "INACTIF",
            Font = AppFonts.GetBoldFont(8f),
            ForeColor = Color.FromArgb(120, 120, 120),
            Location = new Point(pnlExtremeCard.ClientSize.Width - 180, 12),
            Size = new Size(165, 18),
            TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.Transparent
        };
        pnlExtremeCard.Controls.Add(lblUltraEcoStatus);

        btnUltraEco = CreateVioletActionButton("Mode Eco arrière-plan", Color.FromArgb(28, 28, 28), Point.Empty, 100, (s, e) =>
        {
            isUltraEcoActive = !isUltraEcoActive;
            if (isUltraEcoActive)
            {
                int gamePid = -1;
                if (isGameActive)
                {
                    try
                    {
                        var gp = Process.GetProcessesByName(activeGameName);
                        if (gp.Length > 0) gamePid = gp[0].Id;
                    }
                    catch { }
                }
                int count = ultraEcoManager.Activate(Process.GetProcesses(), gamePid);
                btnUltraEco.Text = $"Mode Eco actif ({count})";
                btnUltraEco.NormalColor = Color.FromArgb(16, 185, 129);
                lblUltraEcoStatus.Text = $"{count} APPS GÉRÉES";
                lblUltraEcoStatus.ForeColor = Color.FromArgb(74, 222, 128);
                Log($"[PROCESSUS] Mode Eco activé sur {count} applications d'arrière-plan.");
            }
            else
            {
                int restored = ultraEcoManager.Deactivate();
                btnUltraEco.Text = "Mode Eco arrière-plan";
                btnUltraEco.NormalColor = Color.FromArgb(28, 28, 28);
                lblUltraEcoStatus.Text = "INACTIF";
                lblUltraEcoStatus.ForeColor = Color.FromArgb(120, 120, 120);
                Log($"[PROCESSUS] Priorité normale restaurée sur {restored} applications.");
            }
        });

        lblServiceStatus = new Label
        {
            Text = "",
            Font = AppFonts.GetBoldFont(7.5f),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(14, 105),
            Size = new Size(pnlExtremeCard.ClientSize.Width - 28, 16),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.Transparent
        };
        pnlExtremeCard.Controls.Add(lblServiceStatus);

        btnServiceClean = CreateVioletActionButton("Optimiser Services Windows", Color.FromArgb(28, 28, 28), Point.Empty, 100, (s, e) =>
        {
            isServiceCleanActive = !isServiceCleanActive;
            if (isServiceCleanActive)
            {
                if (!GamingServiceManager.IsAdministrator())
                {
                    Log("[SERVICES] Lancez l'application en Administrateur pour gérer les services.");
                    lblServiceStatus.Text = "Droits Administrateur requis";
                    lblServiceStatus.ForeColor = Color.FromArgb(239, 68, 68);
                    isServiceCleanActive = false;
                    return;
                }
                var (stopped, already, failed, report) = GamingServiceManager.EnableGamingMode();
                btnServiceClean.Text = $"Services optimisés ({stopped})";
                btnServiceClean.NormalColor = Color.FromArgb(16, 185, 129);
                lblServiceStatus.Text = $"{stopped} services arrêtés, {already} déjà inactifs";
                lblServiceStatus.ForeColor = Color.FromArgb(74, 222, 128);
                Log($"[SERVICES] {stopped} services superflus arrêtés pour le jeu.");
            }
            else
            {
                var (restored, failed, report) = GamingServiceManager.RestoreServices();
                btnServiceClean.Text = "Optimiser Services Windows";
                btnServiceClean.NormalColor = Color.FromArgb(28, 28, 28);
                lblServiceStatus.Text = $"{restored} services restaurés";
                lblServiceStatus.ForeColor = Color.FromArgb(148, 163, 184);
                Log($"[SERVICES] {restored} services Windows redémarrés.");
            }
        });

        void LayoutExtremeButtons()
        {
            int cardW = pnlExtremeCard.ClientSize.Width;
            int pad = 14;
            int gap = 12;
            int btnW = (cardW - pad * 2 - gap) / 2;
            btnUltraEco.Bounds = new Rectangle(pad, 52, btnW, 40);
            btnServiceClean.Bounds = new Rectangle(pad + btnW + gap, 52, btnW, 40);
        }
        pnlExtremeCard.Resize += (s, e) => LayoutExtremeButtons();
        LayoutExtremeButtons();

        btnUltraEco.Font = AppFonts.GetBoldFont(8.5f);
        btnServiceClean.Font = AppFonts.GetBoldFont(8.5f);

        pnlExtremeCard.Controls.Add(btnUltraEco);
        pnlExtremeCard.Controls.Add(btnServiceClean);
        pnlMainContent.Controls.Add(pnlExtremeCard);

        // ══════════════════════════════════════════════════════════
        // 7.8 MODULE AUDIO & MICROPHONE (STYLE STEELSERIES SONAR)
        // ══════════════════════════════════════════════════════════
        ModernCard pnlSonarAudioCard = new ModernCard
        {
            Location = new Point(24, 1346),
            Size = new Size(contentWidth - 48, 248),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BorderRadius = 10,
            FillColor = Color.FromArgb(16, 16, 16),
            BorderColor = Color.FromArgb(40, 40, 40),
            Padding = new Padding(14)
        };

        Label lblAudioTitle = new Label
        {
            Text = "AUDIO ET MICROPHONE (SONAR)",
            Font = AppFonts.GetBoldFont(8.5f),
            ForeColor = Color.White,
            Location = new Point(14, 10),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlSonarAudioCard.Controls.Add(lblAudioTitle);

        Label lblAudioSubtitle = new Label
        {
            Text = "Volume casque, boost bruits de pas (Footsteps), routage des canaux et micro.",
            Font = AppFonts.GetBodyFont(7.8f),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(14, 28),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlSonarAudioCard.Controls.Add(lblAudioSubtitle);

        int curHpVol = SonarAudioService.GetHeadphoneVolume();
        Label lblHpVol = new Label
        {
            Text = $"Volume Casque : {curHpVol}%",
            Font = AppFonts.GetBoldFont(8f),
            ForeColor = Color.FromArgb(220, 220, 220),
            Location = new Point(14, 52),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        ModernSlider sliderHpVol = new ModernSlider
        {
            Minimum = 0,
            Maximum = 100,
            Value = curHpVol,
            Location = new Point(14, 70),
            Height = 24
        };
        sliderHpVol.ValueChanged += (s, e) =>
        {
            SonarAudioService.SetHeadphoneVolume(sliderHpVol.Value);
            lblHpVol.Text = $"Volume Casque : {sliderHpVol.Value}%";
        };

        Label lblPreamp = new Label
        {
            Text = $"Boost Gain Casque (Preamp APO) : +{SonarAudioService.PreampGainDb} dB",
            Font = AppFonts.GetBoldFont(8f),
            ForeColor = Color.FromArgb(220, 220, 220),
            Location = new Point(14, 100),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        ModernSlider sliderPreamp = new ModernSlider
        {
            Minimum = 0,
            Maximum = 15,
            Value = SonarAudioService.PreampGainDb,
            Location = new Point(14, 118),
            Height = 24
        };
        sliderPreamp.ValueChanged += (s, e) =>
        {
            SonarAudioService.PreampGainDb = sliderPreamp.Value;
            SonarAudioService.ApplyApoConfig();
            lblPreamp.Text = $"Boost Gain Casque (Preamp APO) : +{sliderPreamp.Value} dB";
        };

        ModernToggleSwitch chkFootsteps = new ModernToggleSwitch
        {
            Title = "Bruits de pas (Footsteps)",
            Checked = SonarAudioService.IsFootstepsBoostActive,
            Location = new Point(14, 150),
            Size = new Size(240, 32),
            Font = AppFonts.GetFont(8.5f)
        };
        chkFootsteps.CheckedChanged += (s, e) =>
        {
            SonarAudioService.IsFootstepsBoostActive = chkFootsteps.Checked;
            SonarAudioService.ApplyApoConfig();
            Log(chkFootsteps.Checked ? "[AUDIO] Égaliseur Bruits de pas activé." : "[AUDIO] Égaliseur Bruits de pas désactivé.");
        };

        ModernToggleSwitch chkAntiPerte = new ModernToggleSwitch
        {
            Title = "Mode Anti-Perte (Fusion L+R)",
            Checked = SonarAudioService.IsMonoAntiPerteActive,
            Location = new Point(14, 186),
            Size = new Size(240, 32),
            Font = AppFonts.GetFont(8.5f)
        };
        chkAntiPerte.CheckedChanged += (s, e) =>
        {
            SonarAudioService.IsMonoAntiPerteActive = chkAntiPerte.Checked;
            SonarAudioService.ApplyApoConfig();
            Log(chkAntiPerte.Checked ? "[AUDIO] Mode Anti-Perte (Fusion L+R) activé." : "[AUDIO] Mode Stéréo standard activé.");
        };

        int curMicVol = SonarAudioService.GetMicrophoneVolume();
        Label lblMicVol = new Label
        {
            Text = $"Volume Microphone : {curMicVol}%",
            Font = AppFonts.GetBoldFont(8f),
            ForeColor = Color.FromArgb(220, 220, 220),
            Location = new Point(480, 52),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        ModernSlider sliderMicVol = new ModernSlider
        {
            Minimum = 0,
            Maximum = 100,
            Value = curMicVol,
            Location = new Point(480, 70),
            Height = 24
        };
        sliderMicVol.ValueChanged += (s, e) =>
        {
            SonarAudioService.SetMicrophoneVolume(sliderMicVol.Value);
            lblMicVol.Text = $"Volume Microphone : {sliderMicVol.Value}%";
        };

        ModernToggleSwitch chkNoDucking = new ModernToggleSwitch
        {
            Title = "Anti-baisse de son (Discord)",
            Checked = true,
            Location = new Point(480, 106),
            Size = new Size(280, 32),
            Font = AppFonts.GetFont(8.5f)
        };
        chkNoDucking.CheckedChanged += (s, e) =>
        {
            SonarAudioService.OptimizeWindowsAudioDucking();
            Log("[AUDIO] Réglage anti-atténuation Windows appliqué.");
        };

                ModernToggleSwitch chkClaritySound = new ModernToggleSwitch
        {
            Title = "Clarity High Pitch (Son le plus clair)",
            Checked = SonarAudioService.IsClarityHighPitchActive,
            Location = new Point(480, 142),
            Size = new Size(280, 32),
            Font = AppFonts.GetBoldFont(8.5f)
        };
        chkClaritySound.CheckedChanged += (s, e) =>
        {
            var (success, msg) = SonarAudioService.ApplyClarityHighPitch(chkClaritySound.Checked);
            Log($"[SONAR] {msg}");
        };

ModernButton btnApplyAudio = CreateVioletActionButton("Appliquer Audio & Micro", Color.FromArgb(28, 28, 28), Point.Empty, 180, (s, e) =>
        {
            SonarAudioService.ApplyApoConfig();
            SonarAudioService.OptimizeWindowsAudioDucking();
            Log("[AUDIO] Paramètres Casque et Micro synchronisés avec succès.");
        });
        btnApplyAudio.Font = AppFonts.GetBoldFont(8.5f);

        void LayoutAudioControls()
        {
            int cardW = pnlSonarAudioCard.ClientSize.Width;
            int pad = 14;
            int gap = 24;
            int colW = Math.Max(180, (cardW - pad * 2 - gap) / 2);

            sliderHpVol.Width = colW;
            sliderPreamp.Width = colW;
            chkFootsteps.Width = colW;
            chkAntiPerte.Width = colW;

            int rightX = pad + colW + gap;
            lblMicVol.Location = new Point(rightX, 52);
            sliderMicVol.Location = new Point(rightX, 70);
            sliderMicVol.Width = colW;

            chkNoDucking.Location = new Point(rightX, 106);
            chkNoDucking.Width = colW;

            chkClaritySound.Location = new Point(rightX, 142);
            chkClaritySound.Width = colW;

            btnApplyAudio.Bounds = new Rectangle(rightX, 192, Math.Min(220, colW), 36);
        }
        pnlSonarAudioCard.Resize += (s, e) => LayoutAudioControls();
        LayoutAudioControls();

        pnlSonarAudioCard.Controls.Add(lblHpVol);
        pnlSonarAudioCard.Controls.Add(sliderHpVol);
        pnlSonarAudioCard.Controls.Add(lblPreamp);
        pnlSonarAudioCard.Controls.Add(sliderPreamp);
        pnlSonarAudioCard.Controls.Add(chkFootsteps);
        pnlSonarAudioCard.Controls.Add(chkAntiPerte);

        pnlSonarAudioCard.Controls.Add(lblMicVol);
        pnlSonarAudioCard.Controls.Add(sliderMicVol);
        pnlSonarAudioCard.Controls.Add(chkNoDucking);
        pnlSonarAudioCard.Controls.Add(chkClaritySound);
        pnlSonarAudioCard.Controls.Add(btnApplyAudio);

        pnlMainContent.Controls.Add(pnlSonarAudioCard);

        // ══════════════════════════════════════════════════════════
        // 8. RÉSOLUTIONS & TRUE STRETCHED (4:3, 5:4, 5:2)
        // ══════════════════════════════════════════════════════════
        ModernCard pnlResolutionCard = new ModernCard
        {
            Location = new Point(24, 1614),
            Size = new Size(contentWidth - 48, 195),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BorderRadius = 10,
            FillColor = Color.FromArgb(16, 16, 16),
            BorderColor = Color.FromArgb(40, 40, 40),
            Padding = new Padding(14)
        };

        Label lblResTitle = new Label
        {
            Text = "RÉSOLUTIONS & TRUE STRETCHED",
            Font = AppFonts.GetBoldFont(8.5f),
            ForeColor = Color.White,
            Location = new Point(14, 10),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlResolutionCard.Controls.Add(lblResTitle);

        Label lblResSubtitle = new Label
        {
            Text = "Plein écran étiré compétitif (4:3, 5:4, 5:2). Fréquence maximale (Hz) préservée.",
            Font = AppFonts.GetBodyFont(7.8f),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(14, 28),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlResolutionCard.Controls.Add(lblResSubtitle);

        var (curW, curH, curHz) = TrueStretchedManager.GetCurrentResolution();
        Label lblResStatus = new Label
        {
            Text = $"Actuel : {curW}x{curH} @ {curHz}Hz",
            Font = AppFonts.GetBoldFont(8f),
            ForeColor = Color.FromArgb(74, 222, 128),
            Location = new Point(pnlResolutionCard.ClientSize.Width - 230, 12),
            Size = new Size(215, 18),
            TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.Transparent
        };
        pnlResolutionCard.Controls.Add(lblResStatus);

        void ApplyAndLogRes(int w, int h)
        {
            var (success, msg) = TrueStretchedManager.ApplyResolution(w, h);
            Log($"[RESOLUTION] {msg}");
            var (nw, nh, nhz) = TrueStretchedManager.GetCurrentResolution();
            lblResStatus.Text = $"Actuel : {nw}x{nh} @ {nhz}Hz";
        }

        ModernButton btnRes1440 = CreateVioletActionButton("1440 x 1080 (4:3 HD)", Color.FromArgb(28, 28, 28), Point.Empty, 100, (s, e) => ApplyAndLogRes(1440, 1080));
        ModernButton btnRes1280 = CreateVioletActionButton("1280 x 960 (4:3 Pro)", Color.FromArgb(28, 28, 28), Point.Empty, 100, (s, e) => ApplyAndLogRes(1280, 960));
        ModernButton btnRes1024 = CreateVioletActionButton("1024 x 768 (4:3 FPS)", Color.FromArgb(28, 28, 28), Point.Empty, 100, (s, e) => ApplyAndLogRes(1024, 768));

        ModernButton btnRes1920_768 = CreateVioletActionButton("1920 x 768 (5:2 Ultra)", Color.FromArgb(28, 28, 28), Point.Empty, 100, (s, e) => ApplyAndLogRes(1920, 768));
        ModernButton btnRes1680_1050 = CreateVioletActionButton("1680 x 1050 (16:10 Écran)", Color.FromArgb(79, 70, 229), Point.Empty, 100, (s, e) => ApplyAndLogRes(1680, 1050));
        ModernButton btnRes1680_672 = CreateVioletActionButton("1680 x 672 (5:2 Écran)", Color.FromArgb(28, 28, 28), Point.Empty, 100, (s, e) => ApplyAndLogRes(1680, 672));
        ModernButton btnRes1280_1024 = CreateVioletActionButton("1280 x 1024 (5:4 Max)", Color.FromArgb(28, 28, 28), Point.Empty, 100, (s, e) => ApplyAndLogRes(1280, 1024));

        ModernButton btnRestoreNative = CreateVioletActionButton("Rétablir Résolution Native", Color.FromArgb(16, 185, 129), Point.Empty, 180, (s, e) =>
        {
            var (success, msg) = TrueStretchedManager.RestoreNative();
            Log($"[RESOLUTION] {msg}");
            var (nw, nh, nhz) = TrueStretchedManager.GetCurrentResolution();
            lblResStatus.Text = $"Actuel : {nw}x{nh} @ {nhz}Hz";
        });

        Label lblCustom = new Label
        {
            Text = "Personnalisée :",
            Font = AppFonts.GetBodyFont(8f),
            ForeColor = Color.FromArgb(200, 200, 200),
            AutoSize = true,
            BackColor = Color.Transparent
        };

        TextBox txtCustomW = new TextBox
        {
            Text = "1440",
            Size = new Size(50, 22),
            BackColor = Color.FromArgb(20, 20, 20),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = AppFonts.GetBodyFont(8.5f),
            TextAlign = HorizontalAlignment.Center
        };

        Label lblX = new Label
        {
            Text = "x",
            Font = AppFonts.GetBodyFont(8f),
            ForeColor = Color.FromArgb(160, 160, 160),
            AutoSize = true,
            BackColor = Color.Transparent
        };

        TextBox txtCustomH = new TextBox
        {
            Text = "1080",
            Size = new Size(50, 22),
            BackColor = Color.FromArgb(20, 20, 20),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = AppFonts.GetBodyFont(8.5f),
            TextAlign = HorizontalAlignment.Center
        };

        ModernButton btnApplyCustom = CreateVioletActionButton("Appliquer", Color.FromArgb(28, 28, 28), Point.Empty, 80, (s, e) =>
        {
            if (int.TryParse(txtCustomW.Text.Trim(), out int cw) && int.TryParse(txtCustomH.Text.Trim(), out int ch))
            {
                ApplyAndLogRes(cw, ch);
            }
            else
            {
                Log("[RESOLUTION] Veuillez saisir des dimensions valides (ex: 1440 x 1080).");
            }
        });

        void LayoutResolutionControls()
        {
            int cardW = pnlResolutionCard.ClientSize.Width;
            int pad = 14;
            int gap = 8;
            int colW = Math.Max(80, (cardW - pad * 2 - gap * 2) / 3);

            btnRes1440.Bounds = new Rectangle(pad, 52, colW, 36);
            btnRes1280.Bounds = new Rectangle(pad + colW + gap, 52, colW, 36);
            btnRes1024.Bounds = new Rectangle(pad + (colW + gap) * 2, 52, colW, 36);

            btnRes1920_768.Bounds = new Rectangle(pad, 94, colW, 36);
            btnRes1680_1050.Bounds = new Rectangle(pad + colW + gap, 94, colW, 36);
            btnRes1280_1024.Bounds = new Rectangle(pad + (colW + gap) * 2, 94, colW, 36);

            int natW = Math.Min(220, cardW / 2 - 20);
            btnRestoreNative.Bounds = new Rectangle(pad, 140, natW, 36);

            int startX = pad + natW + 16;
            lblCustom.Location = new Point(startX, 150);
            txtCustomW.Location = new Point(startX + 95, 148);
            lblX.Location = new Point(startX + 152, 150);
            txtCustomH.Location = new Point(startX + 168, 148);
            btnApplyCustom.Bounds = new Rectangle(startX + 228, 140, 85, 36);
        }

        pnlResolutionCard.Resize += (s, e) => LayoutResolutionControls();
        LayoutResolutionControls();

        btnRes1440.Font = AppFonts.GetBoldFont(8f);
        btnRes1280.Font = AppFonts.GetBoldFont(8f);
        btnRes1024.Font = AppFonts.GetBoldFont(8f);
        btnRes1920_768.Font = AppFonts.GetBoldFont(8f);
        btnRes1680_1050.Font = AppFonts.GetBoldFont(8f);
        btnRes1280_1024.Font = AppFonts.GetBoldFont(8f);
        btnRestoreNative.Font = AppFonts.GetBoldFont(8.2f);
        btnApplyCustom.Font = AppFonts.GetBoldFont(8f);

        pnlResolutionCard.Controls.Add(btnRes1440);
        pnlResolutionCard.Controls.Add(btnRes1280);
        pnlResolutionCard.Controls.Add(btnRes1024);
        pnlResolutionCard.Controls.Add(btnRes1920_768);
        pnlResolutionCard.Controls.Add(btnRes1680_1050);
        pnlResolutionCard.Controls.Add(btnRes1280_1024);
        pnlResolutionCard.Controls.Add(btnRestoreNative);
        pnlResolutionCard.Controls.Add(lblCustom);
        pnlResolutionCard.Controls.Add(txtCustomW);
        pnlResolutionCard.Controls.Add(lblX);
        pnlResolutionCard.Controls.Add(txtCustomH);
        pnlResolutionCard.Controls.Add(btnApplyCustom);
        pnlMainContent.Controls.Add(pnlResolutionCard);

        // ══════════════════════════════════════════════════════════
        // 7.9 CLAVIER SOCD / SNAP TAP [F10]
        // ══════════════════════════════════════════════════════════
        ModernCard pnlSocdCard = new ModernCard
        {
            Location = new Point(24, 1825),
            Size = new Size(contentWidth - 48, 172),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BorderRadius = 10,
            FillColor = Color.FromArgb(16, 16, 16),
            BorderColor = Color.FromArgb(40, 40, 40),
            Padding = new Padding(14)
        };

        Label lblSocdTitle = new Label
        {
            Text = "CLAVIER SOCD / SNAP TAP [F10]",
            Font = AppFonts.GetBoldFont(8.5f),
            ForeColor = Color.White,
            Location = new Point(14, 12),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlSocdCard.Controls.Add(lblSocdTitle);

        Label lblSocdDesc = new Label
        {
            Text = "Priorité au dernier appui (Q/D ou A/D) pour des counter-strafes instantanés.",
            Font = AppFonts.GetBodyFont(7.8f),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(14, 30),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlSocdCard.Controls.Add(lblSocdDesc);

        lblSocdStatus = new Label
        {
            Text = "DESACTIVE",
            Font = AppFonts.GetBoldFont(8f),
            ForeColor = Color.FromArgb(120, 120, 120),
            Location = new Point(pnlSocdCard.ClientSize.Width - 220, 12),
            Size = new Size(205, 18),
            TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.Transparent
        };
        pnlSocdCard.Controls.Add(lblSocdStatus);

        chkSocd = new ModernToggleSwitch
        {
            Title = "Activer SOCD [F10]",
            Checked = false,
            Location = new Point(14, 56),
            Size = new Size(240, 36),
            Font = AppFonts.GetFont(9.5f)
        };
        chkSocd.CheckedChanged += (s, e) =>
        {
            if (chkSocd.Checked != socdService.IsEnabled) ToggleSocd();
        };

        chkSocdVertical = new ModernToggleSwitch
        {
            Title = "Axe vertical (Z/S)",
            Checked = false,
            Location = new Point(265, 56),
            Size = new Size(235, 36),
            Font = AppFonts.GetFont(9f)
        };
        chkSocdVertical.CheckedChanged += (s, e) =>
        {
            socdService.Configure(socdService.Layout, chkSocdVertical.Checked);
            Log($"[SOCD] Axe vertical {(chkSocdVertical.Checked ? "activé" : "désactivé")}.");
        };

        void UpdateSocdLayoutButtons()
        {
            bool azerty = socdService.Layout == SocdLayout.Azerty;
            btnSocdAzerty.NormalColor = azerty ? Color.FromArgb(124, 58, 237) : Color.FromArgb(28, 28, 28);
            btnSocdQwerty.NormalColor = !azerty ? Color.FromArgb(124, 58, 237) : Color.FromArgb(28, 28, 28);
            chkSocdVertical.Title = azerty ? "Axe vertical (Z/S)" : "Axe vertical (W/S)";
        }

        btnSocdAzerty = CreateVioletActionButton("AZERTY (Q / D)", Color.FromArgb(124, 58, 237), Point.Empty, 130, (s, e) =>
        {
            socdService.Configure(SocdLayout.Azerty, chkSocdVertical.Checked);
            UpdateSocdLayoutButtons();
            if (socdService.IsEnabled) lblSocdStatus.Text = "ACTIF (Q / D)";
            Log("[SOCD] Profil AZERTY (Q / D) sélectionné.");
        });

        btnSocdQwerty = CreateVioletActionButton("QWERTY (A / D)", Color.FromArgb(28, 28, 28), Point.Empty, 130, (s, e) =>
        {
            socdService.Configure(SocdLayout.Qwerty, chkSocdVertical.Checked);
            UpdateSocdLayoutButtons();
            if (socdService.IsEnabled) lblSocdStatus.Text = "ACTIF (A / D)";
            Log("[SOCD] Profil QWERTY (A / D) sélectionné.");
        });

        btnSocdAzerty.Font = AppFonts.GetBoldFont(8f);
        btnSocdQwerty.Font = AppFonts.GetBoldFont(8f);

        void LayoutSocdControls()
        {
            int cw = pnlSocdCard.ClientSize.Width;
            btnSocdQwerty.Bounds = new Rectangle(cw - 14 - 135, 56, 135, 36);
            btnSocdAzerty.Bounds = new Rectangle(cw - 14 - 135 - 8 - 135, 56, 135, 36);
        }
        pnlSocdCard.Resize += (s, e) => LayoutSocdControls();
        LayoutSocdControls();

        Label lblSocdWarn = new Label
        {
            Text = "⚠ Anti-cheat : Les macros logicielles (SendInput) sont interdites sur CS2 et risquées sous Riot Vanguard. Utilisez en entraînement ou à vos risques.",
            Font = AppFonts.GetBodyFont(7.8f),
            ForeColor = Color.FromArgb(251, 191, 36),
            Location = new Point(14, 108),
            Size = new Size(pnlSocdCard.ClientSize.Width - 28, 48),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.Transparent
        };

        pnlSocdCard.Controls.Add(chkSocd);
        pnlSocdCard.Controls.Add(chkSocdVertical);
        pnlSocdCard.Controls.Add(btnSocdAzerty);
        pnlSocdCard.Controls.Add(btnSocdQwerty);
        pnlSocdCard.Controls.Add(lblSocdWarn);
        pnlMainContent.Controls.Add(pnlSocdCard);

        // ══════════════════════════════════════════════════════════
        // 8. SECTION VISEUR CROSSHAIR X STUDIO (238 PRESETS ESPORT)
        // ══════════════════════════════════════════════════════════
        pnlCrosshairCard = new ModernCard
        {
            Location = new Point(24, 2012),
            Size = new Size(contentWidth - 48, 330),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BorderRadius = 12,
            FillColor = Color.FromArgb(16, 16, 16),
            BorderColor = Color.FromArgb(40, 40, 40),
            Padding = new Padding(14)
        };

        Label lblCrosshairTitle = new Label
        {
            Text = "VISEUR CROSSHAIR X STUDIO",
            Font = AppFonts.GetBoldFont(8.8f),
            ForeColor = Color.White,
            Location = new Point(14, 10),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlCrosshairCard.Controls.Add(lblCrosshairTitle);

        Label lblCrosshairTag = new Label
        {
            Text = "[F9]",
            Font = AppFonts.GetBoldFont(8f),
            ForeColor = Color.FromArgb(74, 222, 128),
            Location = new Point(228, 10),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlCrosshairCard.Controls.Add(lblCrosshairTag);

        Label lblPresetBadge = new Label
        {
            Text = "238 PRESETS ESPORT",
            Font = AppFonts.GetBoldFont(7.2f),
            ForeColor = Color.FromArgb(168, 85, 247),
            BackColor = Color.FromArgb(30, 20, 45),
            Location = new Point(262, 11),
            AutoSize = true,
            Padding = new Padding(4, 2, 4, 2)
        };
        pnlCrosshairCard.Controls.Add(lblPresetBadge);

        Label lblCrosshairSubtitle = new Label
        {
            Text = "Overlay ultra-rapide au-dessus des jeux (0ms, clic traversant, anti-magenta). Réticules officiels des Pros.",
            Font = AppFonts.GetBodyFont(7.8f),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(14, 28),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlCrosshairCard.Controls.Add(lblCrosshairSubtitle);

        // Aperçu du viseur à droite avec grille tactique eSport
        crosshairPreview = new CrosshairPreviewControl
        {
            Location = new Point(pnlCrosshairCard.ClientSize.Width - 134, 14),
            Size = new Size(120, 120),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        crosshairPreview.Click += (s, e) => ToggleCrosshair();
        pnlCrosshairCard.Controls.Add(crosshairPreview);

        ModernButton btnRecenter = new ModernButton
        {
            Text = "Recentrer (0,0)",
            Size = new Size(120, 26),
            Location = new Point(pnlCrosshairCard.ClientSize.Width - 134, 138),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Font = AppFonts.GetBoldFont(7.5f),
            NormalColor = Color.FromArgb(24, 24, 24)
        };
        btnRecenter.Click += (s, e) =>
        {
            CrosshairService.Instance.UpdateSettings(s => { s.OffsetX = 0; s.OffsetY = 0; });
            crosshairPreview.Invalidate();
            Log("[CROSSHAIR] Viseur recentré sur (0, 0).");
        };
        pnlCrosshairCard.Controls.Add(btnRecenter);

        // Ligne 1 : Interrupteurs principaux
        chkCrosshair = new ModernToggleSwitch
        {
            Title = "Activer Viseur [F9]",
            Checked = CrosshairService.Instance.IsActive,
            Location = new Point(14, 52),
            Size = new Size(185, 32),
            Font = AppFonts.GetFont(9f)
        };
        chkCrosshair.CheckedChanged += (s, e) =>
        {
            if (chkCrosshair.Checked != CrosshairService.Instance.IsActive)
                ToggleCrosshair();
        };
        pnlCrosshairCard.Controls.Add(chkCrosshair);

        ModernToggleSwitch chkAutoGame = new ModernToggleSwitch
        {
            Title = "Auto-Affichage en jeu",
            Checked = CrosshairService.Instance.Settings.AutoShowOnGame,
            Location = new Point(208, 52),
            Size = new Size(205, 32),
            Font = AppFonts.GetFont(9f)
        };
        chkAutoGame.CheckedChanged += (s, e) =>
        {
            CrosshairService.Instance.UpdateSettings(cfg => cfg.AutoShowOnGame = chkAutoGame.Checked);
            Log(chkAutoGame.Checked ? "[CROSSHAIR] Mode Auto-Affichage en jeu activé." : "[CROSSHAIR] Mode Auto-Affichage désactivé.");
        };
        pnlCrosshairCard.Controls.Add(chkAutoGame);

        CheckBox chkCrisp = new CheckBox
        {
            Text = "Anti-bavure (Pixel-Crisp)",
            Font = AppFonts.GetBodyFont(7.5f),
            ForeColor = Color.FromArgb(200, 200, 200),
            Checked = CrosshairService.Instance.Settings.CrispLines,
            Location = new Point(425, 58),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        chkCrisp.CheckedChanged += (s, e) =>
        {
            CrosshairService.Instance.UpdateSettings(cfg => cfg.CrispLines = chkCrisp.Checked);
            crosshairPreview.Invalidate();
        };
        pnlCrosshairCard.Controls.Add(chkCrisp);

        // Ligne 2 : Bibliothèque CrosshairX - Pills de Catégories
        Label lblLib = new Label
        {
            Text = "Bibliothèque :",
            Font = AppFonts.GetBoldFont(7.5f),
            ForeColor = Color.FromArgb(160, 160, 160),
            Location = new Point(14, 94),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlCrosshairCard.Controls.Add(lblLib);

        Panel pnlCatPills = new Panel
        {
            Location = new Point(102, 90),
            Size = new Size(pnlCrosshairCard.ClientSize.Width - 250, 30),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.Transparent
        };
        pnlCrosshairCard.Controls.Add(pnlCatPills);

        // Controls de recherche et de sélection
        TextBox txtSearch = new TextBox
        {
            Location = new Point(14, 126),
            Size = new Size(160, 24),
            BackColor = Color.FromArgb(24, 24, 24),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = AppFonts.GetBodyFont(8f),
            PlaceholderText = "🔍 Rechercher..."
        };
        pnlCrosshairCard.Controls.Add(txtSearch);

        ComboBox cmbPresets = new ComboBox
        {
            Location = new Point(182, 126),
            Size = new Size(230, 26),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(24, 24, 24),
            ForeColor = Color.White,
            Font = AppFonts.GetBodyFont(8f),
            FlatStyle = FlatStyle.Flat
        };
        pnlCrosshairCard.Controls.Add(cmbPresets);

        string currentCat = "Tous";
        bool isInitializingPresets = true;

        void RefreshPresetCombo()
        {
            cmbPresets.Items.Clear();
            var filtered = CrosshairService.Instance.FilterPresets(currentCat, txtSearch.Text);
            foreach (var p in filtered)
            {
                cmbPresets.Items.Add($"{p.Name} - {p.Author}");
            }
            if (cmbPresets.Items.Count > 0)
            {
                cmbPresets.SelectedIndex = 0;
            }
        }

        string[] catList = new[] { "Tous", "Valorant Pros", "CS2 Pros", "Fortnite Pros", "Rust Creators", "Tactical", "Funny" };
        int pillX = 0;
        foreach (var cat in catList)
        {
            string catLabel = cat == "Tous" ? "Tous (238)" : (cat.Replace(" Pros", "★").Replace(" Creators", ""));
            var btnCat = new ModernButton
            {
                Text = catLabel,
                Location = new Point(pillX, 0),
                Size = new Size(cat == "Tous" ? 68 : 88, 26),
                BorderRadius = 5,
                Font = AppFonts.GetBoldFont(7.2f),
                NormalColor = cat == currentCat ? Color.White : Color.FromArgb(28, 28, 28),
                ForeColor = cat == currentCat ? Color.Black : Color.FromArgb(200, 200, 200)
            };
            btnCat.Click += (s, e) =>
            {
                currentCat = cat;
                foreach (Control c in pnlCatPills.Controls)
                {
                    if (c is ModernButton mb)
                    {
                        bool isSel = mb == btnCat;
                        mb.NormalColor = isSel ? Color.White : Color.FromArgb(28, 28, 28);
                        mb.ForeColor = isSel ? Color.Black : Color.FromArgb(200, 200, 200);
                    }
                }
                RefreshPresetCombo();
            };
            pnlCatPills.Controls.Add(btnCat);
            pillX += btnCat.Width + 5;
        }

        txtSearch.TextChanged += (s, e) => RefreshPresetCombo();

        // Déclaration des sliders en amont pour synchronisation
        ModernSlider sliderSize = new ModernSlider { Minimum = 1, Maximum = 30, Value = (int)CrosshairService.Instance.Settings.Size, Location = new Point(56, 206), Size = new Size(88, 24) };
        ModernSlider sliderThick = new ModernSlider { Minimum = 1, Maximum = 8, Value = (int)CrosshairService.Instance.Settings.Thickness, Location = new Point(234, 206), Size = new Size(80, 24) };
        ModernSlider sliderGap = new ModernSlider { Minimum = 0, Maximum = 25, Value = (int)CrosshairService.Instance.Settings.Gap, Location = new Point(376, 206), Size = new Size(80, 24) };
        ModernSlider sliderOpac = new ModernSlider { Minimum = 20, Maximum = 100, Value = (int)(CrosshairService.Instance.Settings.Opacity * 100), Location = new Point(526, 206), Size = new Size(80, 24) };

        CheckBox chkOutline = new CheckBox { Text = "Contour (Outline)", Font = AppFonts.GetBodyFont(7.5f), ForeColor = Color.FromArgb(200, 200, 200), Checked = CrosshairService.Instance.Settings.Outline, Location = new Point(530, 166), AutoSize = true, BackColor = Color.Transparent };
        CheckBox chkDot = new CheckBox { Text = "Point Central (Dot)", Font = AppFonts.GetBodyFont(7.5f), ForeColor = Color.FromArgb(200, 200, 200), Checked = CrosshairService.Instance.Settings.CenterDot, Location = new Point(325, 166), AutoSize = true, BackColor = Color.Transparent };
        CheckBox chkDotSquare = new CheckBox { Text = "Point Carré", Font = AppFonts.GetBodyFont(7.5f), ForeColor = Color.FromArgb(200, 200, 200), Checked = CrosshairService.Instance.Settings.DotShape == "Square", Location = new Point(445, 166), AutoSize = true, BackColor = Color.Transparent };
        CheckBox chkTop = new CheckBox { Text = "Haut", Font = AppFonts.GetBodyFont(7.5f), ForeColor = Color.FromArgb(200, 200, 200), Checked = CrosshairService.Instance.Settings.TopLine, Location = new Point(116, 166), AutoSize = true, BackColor = Color.Transparent };
        CheckBox chkBottom = new CheckBox { Text = "Bas", Font = AppFonts.GetBodyFont(7.5f), ForeColor = Color.FromArgb(200, 200, 200), Checked = CrosshairService.Instance.Settings.BottomLine, Location = new Point(168, 166), AutoSize = true, BackColor = Color.Transparent };
        CheckBox chkLeft = new CheckBox { Text = "Gauche", Font = AppFonts.GetBodyFont(7.5f), ForeColor = Color.FromArgb(200, 200, 200), Checked = CrosshairService.Instance.Settings.LeftLine, Location = new Point(214, 166), AutoSize = true, BackColor = Color.Transparent };
        CheckBox chkRight = new CheckBox { Text = "Droite", Font = AppFonts.GetBodyFont(7.5f), ForeColor = Color.FromArgb(200, 200, 200), Checked = CrosshairService.Instance.Settings.RightLine, Location = new Point(274, 166), AutoSize = true, BackColor = Color.Transparent };

        void SyncUiControls()
        {
            var st = CrosshairService.Instance.Settings;
            sliderSize.Value = Math.Clamp((int)st.Size, 1, 30);
            sliderThick.Value = Math.Clamp((int)st.Thickness, 1, 8);
            sliderGap.Value = Math.Clamp((int)st.Gap, 0, 25);
            sliderOpac.Value = Math.Clamp((int)(st.Opacity * 100), 20, 100);
            lblCrosshairSizeVal.Text = $"{sliderSize.Value}px";
            lblCrosshairThickVal.Text = $"{sliderThick.Value}px";
            lblCrosshairGapVal.Text = $"{sliderGap.Value}px";
            chkOutline.Checked = st.Outline;
            chkDot.Checked = st.CenterDot;
            chkDotSquare.Checked = st.DotShape == "Square";
            chkTop.Checked = st.TopLine;
            chkBottom.Checked = st.BottomLine;
            chkLeft.Checked = st.LeftLine;
            chkRight.Checked = st.RightLine;
            crosshairPreview.Invalidate();
        }

        cmbPresets.SelectedIndexChanged += (s, e) =>
        {
            if (isInitializingPresets) return;
            var filtered = CrosshairService.Instance.FilterPresets(currentCat, txtSearch.Text);
            if (cmbPresets.SelectedIndex >= 0 && cmbPresets.SelectedIndex < filtered.Count)
            {
                var sel = filtered[cmbPresets.SelectedIndex];
                CrosshairService.Instance.UpdateSettings(cfg => cfg.ApplyPreset(sel));
                SyncUiControls();
                Log($"[CROSSHAIR] Preset appliqué : {sel.Name} ({sel.Category} - {sel.Author})");
            }
        };

        // Custom dark input dialog helper
        string ShowValInputDialog(string promptText, string titleText)
        {
            using Form prompt = new Form
            {
                Width = 460,
                Height = 160,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = titleText,
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(16, 16, 16),
                ForeColor = Color.White,
                MaximizeBox = false,
                MinimizeBox = false
            };
            Label textLabel = new Label { Left = 16, Top = 16, Width = 410, Text = promptText, ForeColor = Color.FromArgb(200, 200, 200) };
            TextBox textBox = new TextBox { Left = 16, Top = 44, Width = 410, BackColor = Color.FromArgb(28, 28, 28), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            Button confirmation = new Button { Text = "Valider", Left = 246, Width = 85, Top = 80, DialogResult = DialogResult.OK, BackColor = Color.FromArgb(40, 40, 40), FlatStyle = FlatStyle.Flat };
            Button cancel = new Button { Text = "Annuler", Left = 341, Width = 85, Top = 80, DialogResult = DialogResult.Cancel, BackColor = Color.FromArgb(28, 28, 28), FlatStyle = FlatStyle.Flat };
            confirmation.Click += (sender, e) => { prompt.Close(); };
            cancel.Click += (sender, e) => { prompt.Close(); };
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(cancel);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;
            prompt.CancelButton = cancel;
            return prompt.ShowDialog(this) == DialogResult.OK ? textBox.Text : "";
        }

        // Boutons Import/Export Valorant
        ModernButton btnImportVal = new ModernButton
        {
            Text = "📥 Importer Code Valorant",
            Location = new Point(420, 126),
            Size = new Size(160, 26),
            BorderRadius = 6,
            Font = AppFonts.GetBoldFont(7.2f),
            NormalColor = Color.FromArgb(28, 28, 28),
            ForeColor = Color.FromArgb(220, 220, 220)
        };
        btnImportVal.Click += (s, e) =>
        {
            string prompt = ShowValInputDialog("Collez votre code de réticule Valorant (ex: 0;P;c;5;o;1;0t;1;0l;3;0o;2...) :", "Importer Réticule Valorant");
            if (!string.IsNullOrWhiteSpace(prompt))
            {
                bool ok = CrosshairService.Instance.ImportValorantCode(prompt);
                if (ok)
                {
                    SyncUiControls();
                    Log("[CROSSHAIR] Code Valorant importé et appliqué avec succès !");
                }
                else
                {
                    MessageBox.Show("Le code Valorant fourni n'a pas pu être analysé.", "Erreur format", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        };
        pnlCrosshairCard.Controls.Add(btnImportVal);

        ModernButton btnExportVal = new ModernButton
        {
            Text = "📋 Copier Code Valorant",
            Location = new Point(586, 126),
            Size = new Size(145, 26),
            BorderRadius = 6,
            Font = AppFonts.GetBoldFont(7.2f),
            NormalColor = Color.FromArgb(28, 28, 28),
            ForeColor = Color.FromArgb(220, 220, 220)
        };
        btnExportVal.Click += (s, e) =>
        {
            string code = CrosshairService.Instance.ExportValorantCode();
            Clipboard.SetText(code);
            Log($"[CROSSHAIR] Code Valorant copié dans le presse-papier : {code}");
            MessageBox.Show("Code Valorant copié dans le presse-papier : " + code, "Code exporté", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        pnlCrosshairCard.Controls.Add(btnExportVal);

        // Ligne 4 : Toggles Branches et Dots
        Label lblBranches = new Label { Text = "Branches :", Font = AppFonts.GetBoldFont(7.5f), ForeColor = Color.FromArgb(160, 160, 160), Location = new Point(14, 167), AutoSize = true, BackColor = Color.Transparent };
        pnlCrosshairCard.Controls.Add(lblBranches);

        chkTop.CheckedChanged += (s, e) => { CrosshairService.Instance.UpdateSettings(c => c.TopLine = chkTop.Checked); crosshairPreview.Invalidate(); };
        chkBottom.CheckedChanged += (s, e) => { CrosshairService.Instance.UpdateSettings(c => c.BottomLine = chkBottom.Checked); crosshairPreview.Invalidate(); };
        chkLeft.CheckedChanged += (s, e) => { CrosshairService.Instance.UpdateSettings(c => c.LeftLine = chkLeft.Checked); crosshairPreview.Invalidate(); };
        chkRight.CheckedChanged += (s, e) => { CrosshairService.Instance.UpdateSettings(c => c.RightLine = chkRight.Checked); crosshairPreview.Invalidate(); };
        chkDot.CheckedChanged += (s, e) => { CrosshairService.Instance.UpdateSettings(c => c.CenterDot = chkDot.Checked); crosshairPreview.Invalidate(); };
        chkDotSquare.CheckedChanged += (s, e) => { CrosshairService.Instance.UpdateSettings(c => c.DotShape = chkDotSquare.Checked ? "Square" : "Circle"); crosshairPreview.Invalidate(); };
        chkOutline.CheckedChanged += (s, e) => { CrosshairService.Instance.UpdateSettings(c => c.Outline = chkOutline.Checked); crosshairPreview.Invalidate(); };

        pnlCrosshairCard.Controls.Add(chkTop);
        pnlCrosshairCard.Controls.Add(chkBottom);
        pnlCrosshairCard.Controls.Add(chkLeft);
        pnlCrosshairCard.Controls.Add(chkRight);
        pnlCrosshairCard.Controls.Add(chkDot);
        pnlCrosshairCard.Controls.Add(chkDotSquare);
        pnlCrosshairCard.Controls.Add(chkOutline);

        // Ligne 5 : Sliders
        Label lblTaille = new Label { Text = "Taille :", Font = AppFonts.GetBodyFont(7.5f), ForeColor = Color.FromArgb(160, 160, 160), Location = new Point(14, 209), AutoSize = true, BackColor = Color.Transparent };
        lblCrosshairSizeVal = new Label { Text = $"{(int)CrosshairService.Instance.Settings.Size}px", Font = AppFonts.GetBoldFont(7.5f), ForeColor = Color.White, Location = new Point(48, 209), AutoSize = true, BackColor = Color.Transparent };
        sliderSize.ValueChanged += (s, e) => { CrosshairService.Instance.UpdateSettings(cfg => cfg.Size = sliderSize.Value); lblCrosshairSizeVal.Text = $"{sliderSize.Value}px"; crosshairPreview.Invalidate(); };

        Label lblEpais = new Label { Text = "Épaisseur :", Font = AppFonts.GetBodyFont(7.5f), ForeColor = Color.FromArgb(160, 160, 160), Location = new Point(154, 209), AutoSize = true, BackColor = Color.Transparent };
        lblCrosshairThickVal = new Label { Text = $"{(int)CrosshairService.Instance.Settings.Thickness}px", Font = AppFonts.GetBoldFont(7.5f), ForeColor = Color.White, Location = new Point(208, 209), AutoSize = true, BackColor = Color.Transparent };
        sliderThick.ValueChanged += (s, e) => { CrosshairService.Instance.UpdateSettings(cfg => cfg.Thickness = sliderThick.Value); lblCrosshairThickVal.Text = $"{sliderThick.Value}px"; crosshairPreview.Invalidate(); };

        Label lblTrou = new Label { Text = "Trou :", Font = AppFonts.GetBodyFont(7.5f), ForeColor = Color.FromArgb(160, 160, 160), Location = new Point(325, 209), AutoSize = true, BackColor = Color.Transparent };
        lblCrosshairGapVal = new Label { Text = $"{(int)CrosshairService.Instance.Settings.Gap}px", Font = AppFonts.GetBoldFont(7.5f), ForeColor = Color.White, Location = new Point(356, 209), AutoSize = true, BackColor = Color.Transparent };
        sliderGap.ValueChanged += (s, e) => { CrosshairService.Instance.UpdateSettings(cfg => cfg.Gap = sliderGap.Value); lblCrosshairGapVal.Text = $"{sliderGap.Value}px"; crosshairPreview.Invalidate(); };

        Label lblOpac = new Label { Text = "Opacité :", Font = AppFonts.GetBodyFont(7.5f), ForeColor = Color.FromArgb(160, 160, 160), Location = new Point(468, 209), AutoSize = true, BackColor = Color.Transparent };
        Label lblOpacVal = new Label { Text = $"{(int)(CrosshairService.Instance.Settings.Opacity * 100)}%", Font = AppFonts.GetBoldFont(7.5f), ForeColor = Color.White, Location = new Point(510, 209), AutoSize = true, BackColor = Color.Transparent };
        sliderOpac.ValueChanged += (s, e) => { float op = sliderOpac.Value / 100f; CrosshairService.Instance.UpdateSettings(cfg => cfg.Opacity = op); lblOpacVal.Text = $"{sliderOpac.Value}%"; crosshairPreview.Invalidate(); };

        pnlCrosshairCard.Controls.Add(lblTaille);
        pnlCrosshairCard.Controls.Add(lblCrosshairSizeVal);
        pnlCrosshairCard.Controls.Add(sliderSize);
        pnlCrosshairCard.Controls.Add(lblEpais);
        pnlCrosshairCard.Controls.Add(lblCrosshairThickVal);
        pnlCrosshairCard.Controls.Add(sliderThick);
        pnlCrosshairCard.Controls.Add(lblTrou);
        pnlCrosshairCard.Controls.Add(lblCrosshairGapVal);
        pnlCrosshairCard.Controls.Add(sliderGap);
        pnlCrosshairCard.Controls.Add(lblOpac);
        pnlCrosshairCard.Controls.Add(lblOpacVal);
        pnlCrosshairCard.Controls.Add(sliderOpac);

        // Ligne 6 : Couleurs eSport rapides
        Label lblCouleurs = new Label { Text = "Couleur :", Font = AppFonts.GetBodyFont(7.5f), ForeColor = Color.FromArgb(160, 160, 160), Location = new Point(14, 252), AutoSize = true, BackColor = Color.Transparent };
        pnlCrosshairCard.Controls.Add(lblCouleurs);

        (string name, Color col)[] colorPresets = new[]
        {
            ("Vert Fluo", Color.FromArgb(0, 255, 102)),
            ("Cyan Fluo", Color.FromArgb(0, 255, 255)),
            ("Rouge", Color.FromArgb(255, 51, 51)),
            ("Violet", Color.FromArgb(168, 85, 247)),
            ("Jaune", Color.FromArgb(255, 230, 0)),
            ("Blanc", Color.FromArgb(255, 255, 255))
        };

        int colBtnX = 72;
        int colBtnY = 247;
        int colW = 76;
        int colH = 26;
        for (int i = 0; i < colorPresets.Length; i++)
        {
            var (cName, cColor) = colorPresets[i];
            var btnCol = new ModernButton
            {
                Text = cName,
                Location = new Point(colBtnX + (colW + 5) * i, colBtnY),
                Size = new Size(colW, colH),
                BorderRadius = 6,
                Font = AppFonts.GetBoldFont(7.5f),
                NormalColor = Color.FromArgb(24, 24, 24),
                ForeColor = cColor,
                BorderColor = cColor
            };
            btnCol.Click += (s, e) =>
            {
                CrosshairService.Instance.UpdateSettings(cfg => cfg.Color = cColor);
                crosshairPreview.Invalidate();
            };
            pnlCrosshairCard.Controls.Add(btnCol);
        }

        ModernButton btnCustomCol = new ModernButton
        {
            Text = "Palette...",
            Location = new Point(colBtnX + (colW + 5) * colorPresets.Length, colBtnY),
            Size = new Size(68, colH),
            BorderRadius = 6,
            Font = AppFonts.GetBoldFont(7.5f),
            NormalColor = Color.FromArgb(24, 24, 24),
            ForeColor = Color.White
        };
        btnCustomCol.Click += (s, e) =>
        {
            using var cd = new ColorDialog { Color = CrosshairService.Instance.Settings.Color, FullOpen = true };
            if (cd.ShowDialog() == DialogResult.OK)
            {
                CrosshairService.Instance.UpdateSettings(cfg => cfg.Color = cd.Color);
                crosshairPreview.Invalidate();
            }
        };
        pnlCrosshairCard.Controls.Add(btnCustomCol);

        // Ligne 7 : Formes Rapides
        int pBtnX = 14;
        int pBtnY = 286;
        (string fName, CrosshairShape fShape)[] shapes = new[]
        {
            ("Croix", CrosshairShape.ClassicCross),
            ("Gap", CrosshairShape.GapCross),
            ("Point", CrosshairShape.Dot),
            ("Cercle", CrosshairShape.Circle),
            ("Rond+Pt", CrosshairShape.CircleDot),
            ("T-Cross", CrosshairShape.TCross),
            ("Chevron", CrosshairShape.Chevron)
        };
        for (int i = 0; i < shapes.Length; i++)
        {
            var (fName, fShape) = shapes[i];
            var bShape = new ModernButton
            {
                Text = fName,
                Location = new Point(pBtnX + 68 * i, pBtnY),
                Size = new Size(64, 26),
                BorderRadius = 5,
                Font = AppFonts.GetBoldFont(7.5f),
                NormalColor = Color.FromArgb(28, 28, 28),
                ForeColor = Color.FromArgb(200, 200, 200)
            };
            bShape.Click += (s, e) =>
            {
                CrosshairService.Instance.UpdateSettings(cfg => cfg.Shape = fShape);
                SyncUiControls();
            };
            pnlCrosshairCard.Controls.Add(bShape);
        }

        RefreshPresetCombo();
        SyncUiControls();
        isInitializingPresets = false;
        pnlMainContent.Controls.Add(pnlCrosshairCard);


        // ══════════════════════════════════════════════════════════
        // 8b. WALLPAPER ENGINE (VIDÉO & PHOTO LIVE FOND D'ÉCRAN)
        // ══════════════════════════════════════════════════════════
        ModernCard pnlWallpaperCard = new ModernCard
        {
            Location = new Point(24, 2362),
            Size = new Size(contentWidth - 48, 270),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BorderRadius = 10,
            FillColor = Color.FromArgb(16, 16, 16),
            BorderColor = Color.FromArgb(40, 40, 40),
            Padding = new Padding(14)
        };

        Label lblWallpaperTitle = new Label
        {
            Text = "WALLPAPER ENGINE - FOND D'ÉCRAN VIDÉO & PHOTO",
            Font = AppFonts.GetBoldFont(8.5f),
            ForeColor = Color.White,
            Location = new Point(14, 10),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        Label lblWallpaperSub = new Label
        {
            Text = "Fonds d'écran animés vidéo (MP4, WebM, GIF) et photos avec pause automatique en jeu",
            Font = AppFonts.GetFont(8f),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(14, 28),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlWallpaperCard.Controls.Add(lblWallpaperTitle);
        pnlWallpaperCard.Controls.Add(lblWallpaperSub);

        // Ligne 1 : Boutons d'import
        int wpBtnW = Math.Max(120, (pnlWallpaperCard.ClientSize.Width - 48) / 3);
        ModernButton btnPickVideo = CreateVioletActionButton("Choisir une Vidéo (MP4, WebM...)", Color.FromArgb(28, 28, 28), new Point(14, 52), wpBtnW, async (s, e) =>
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Sélectionner une vidéo pour fond d'écran",
                Filter = "Fichiers Vidéo & Animations (*.mp4;*.webm;*.mov;*.avi;*.gif)|*.mp4;*.webm;*.mov;*.avi;*.gif|Tous les fichiers (*.*)|*.*"
            };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                await WallpaperEngineService.Instance.SetVideoWallpaperAsync(ofd.FileName, WallpaperEngineService.Instance.Volume);
                Log($"[WALLPAPER] Fond d'écran vidéo activé : {Path.GetFileName(ofd.FileName)}");
            }
        });

        ModernButton btnPickPhoto = CreateVioletActionButton("Choisir une Photo (JPG, PNG...)", Color.FromArgb(28, 28, 28), new Point(14 + wpBtnW + 10, 52), wpBtnW, async (s, e) =>
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Sélectionner une image pour fond d'écran",
                Filter = "Images (*.jpg;*.jpeg;*.png;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.bmp|Tous les fichiers (*.*)|*.*"
            };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                await WallpaperEngineService.Instance.SetPhotoWallpaperAsync(ofd.FileName, true);
                Log($"[WALLPAPER] Fond d'écran photo défini : {Path.GetFileName(ofd.FileName)}");
            }
        });

        ModernButton btnStopWallpaper = CreateVioletActionButton("Rétablir Bureau Windows", Color.FromArgb(28, 28, 28), new Point(14 + (wpBtnW + 10) * 2, 52), wpBtnW, (s, e) =>
        {
            WallpaperEngineService.Instance.StopLiveWallpaper(true);
            Log("[WALLPAPER] Fond d'écran animé désactivé. Bureau Windows standard rétabli.");
        });

        pnlWallpaperCard.Controls.Add(btnPickVideo);
        pnlWallpaperCard.Controls.Add(btnPickPhoto);
        pnlWallpaperCard.Controls.Add(btnStopWallpaper);

        // Ligne 2 : Presets 60 FPS
        Label lblPresets = new Label
        {
            Text = "PRESETS INTÉGRÉS :",
            Font = AppFonts.GetBoldFont(7.5f),
            ForeColor = Color.FromArgb(160, 160, 160),
            Location = new Point(14, 104),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlWallpaperCard.Controls.Add(lblPresets);

        int prBtnW = Math.Max(120, (pnlWallpaperCard.ClientSize.Width - 48) / 3);
        ModernButton btnPresetWaves = CreateVioletActionButton("Ondes Monochromes (Fluide)", Color.FromArgb(28, 28, 28), new Point(14, 122), prBtnW, async (s, e) =>
        {
            await WallpaperEngineService.Instance.SetPresetWallpaperAsync("monochrome_waves");
            Log("[WALLPAPER] Preset Ondes Monochromes 60 FPS activé.");
        });

        ModernButton btnPresetMatrix = CreateVioletActionButton("Pluie Digitale Matrix", Color.FromArgb(28, 28, 28), new Point(14 + prBtnW + 10, 122), prBtnW, async (s, e) =>
        {
            await WallpaperEngineService.Instance.SetPresetWallpaperAsync("matrix_rain");
            Log("[WALLPAPER] Preset Pluie Digitale Matrix activé.");
        });

        ModernButton btnPresetSpace = CreateVioletActionButton("Deep Space 3D (Warp)", Color.FromArgb(28, 28, 28), new Point(14 + (prBtnW + 10) * 2, 122), prBtnW, async (s, e) =>
        {
            await WallpaperEngineService.Instance.SetPresetWallpaperAsync("deep_space");
            Log("[WALLPAPER] Preset Deep Space 3D activé.");
        });

        pnlWallpaperCard.Controls.Add(btnPresetWaves);
        pnlWallpaperCard.Controls.Add(btnPresetMatrix);
        pnlWallpaperCard.Controls.Add(btnPresetSpace);

        // Ligne 3 : Contrôles Audio & Pause
        Label lblAudio = new Label
        {
            Text = "SON VIDÉO :",
            Font = AppFonts.GetBoldFont(7.5f),
            ForeColor = Color.FromArgb(160, 160, 160),
            Location = new Point(14, 178),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlWallpaperCard.Controls.Add(lblAudio);

        ModernButton btnVolMute = CreateVioletActionButton("Muet", Color.FromArgb(28, 28, 28), new Point(88, 172), 65, async (s, e) =>
        {
            await WallpaperEngineService.Instance.SetVolumeAsync(0f);
        });
        ModernButton btnVol20 = CreateVioletActionButton("20%", Color.FromArgb(28, 28, 28), new Point(159, 172), 55, async (s, e) =>
        {
            await WallpaperEngineService.Instance.SetVolumeAsync(0.2f);
        });
        ModernButton btnVol50 = CreateVioletActionButton("50%", Color.FromArgb(28, 28, 28), new Point(220, 172), 55, async (s, e) =>
        {
            await WallpaperEngineService.Instance.SetVolumeAsync(0.5f);
        });
        ModernButton btnVol100 = CreateVioletActionButton("100%", Color.FromArgb(28, 28, 28), new Point(281, 172), 60, async (s, e) =>
        {
            await WallpaperEngineService.Instance.SetVolumeAsync(1.0f);
        });

        ModernButton btnTogglePause = CreateVioletActionButton("Pause / Reprendre", Color.FromArgb(28, 28, 28), new Point(350, 172), 140, async (s, e) =>
        {
            await WallpaperEngineService.Instance.SetPausedAsync(!WallpaperEngineService.Instance.IsPaused);
        });

        CheckBox chkAutoPauseGame = new CheckBox
        {
            Text = "Pause auto en jeu (Économie GPU)",
            Font = AppFonts.GetFont(8.5f),
            ForeColor = Color.FromArgb(220, 220, 220),
            Location = new Point(500, 178),
            AutoSize = true,
            Checked = true,
            BackColor = Color.Transparent
        };
        chkAutoPauseGame.CheckedChanged += (s, e) =>
        {
            WallpaperEngineService.Instance.AutoPauseOnGame = chkAutoPauseGame.Checked;
        };

        pnlWallpaperCard.Controls.Add(btnVolMute);
        pnlWallpaperCard.Controls.Add(btnVol20);
        pnlWallpaperCard.Controls.Add(btnVol50);
        pnlWallpaperCard.Controls.Add(btnVol100);
        pnlWallpaperCard.Controls.Add(btnTogglePause);
        pnlWallpaperCard.Controls.Add(chkAutoPauseGame);

        // Ligne 4 : Barre de statut
        Label lblWallpaperStatus = new Label
        {
            Text = "Statut : Prêt (Choisissez une vidéo, photo ou un preset)",
            Font = AppFonts.GetBoldFont(8f),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(14, 230),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        WallpaperEngineService.Instance.StatusChanged += msg =>
        {
            if (this.InvokeRequired) this.BeginInvoke(() => lblWallpaperStatus.Text = "Statut : " + msg);
            else lblWallpaperStatus.Text = "Statut : " + msg;
        };
        pnlWallpaperCard.Controls.Add(lblWallpaperStatus);

        pnlMainContent.Controls.Add(pnlWallpaperCard);

        // ══════════════════════════════════════════════════════════
        // 9. Activity Log Box (Console Terminal Sombre)
        // ══════════════════════════════════════════════════════════
        ModernCard pnlLogCard = new ModernCard
        {
            Location = new Point(24, 2652),
            Size = new Size(contentWidth - 48, 146),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BorderRadius = 10,
            FillColor = Color.FromArgb(12, 12, 12),
            BorderColor = Color.FromArgb(35, 35, 35),
            Padding = new Padding(10)
        };

        Label lblLogTitle = new Label
        {
            Text = "CONSOLE D'ACTIVITE",
            Font = AppFonts.GetBoldFont(8f),
            ForeColor = Color.FromArgb(140, 140, 140),
            Location = new Point(14, 6),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlLogCard.Controls.Add(lblLogTitle);

        rtbLogs = new RichTextBox
        {
            Location = new Point(14, 24),
            Size = new Size(pnlLogCard.ClientSize.Width - 28, pnlLogCard.ClientSize.Height - 34),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.FromArgb(12, 12, 12),
            ForeColor = Color.FromArgb(200, 200, 200),
            BorderStyle = BorderStyle.None,
            Font = AppFonts.GetFont(9f),
            ReadOnly = true,
            ScrollBars = RichTextBoxScrollBars.None
        };
        pnlLogCard.Controls.Add(rtbLogs);
        pnlMainContent.Controls.Add(pnlLogCard);

        Label lblBrandLabel = new Label
        {
            Text = "GameEcoBot",
            Location = new Point(20, 24),
            Size = new Size(180, 24),
            Font = AppFonts.GetTitleFont(13f),
            ForeColor = Color.FromArgb(245, 245, 245),
            BackColor = Color.Transparent
        };
        pnlSidebar.Controls.Add(lblBrandLabel);

        Label lblBrandSub = new Label
        {
            Text = "GAMING & AUDIO CONTROLLER",
            Location = new Point(20, 48),
            Size = new Size(180, 16),
            Font = AppFonts.GetBoldFont(7.5f),
            ForeColor = Color.FromArgb(140, 140, 140),
            BackColor = Color.Transparent
        };
        pnlSidebar.Controls.Add(lblBrandSub);

        Label lblNavigationTitle = new Label
        {
            Text = "NAVIGATION",
            Location = new Point(20, 74),
            Size = new Size(180, 14),
            Font = AppFonts.GetBoldFont(7.5f),
            ForeColor = Color.FromArgb(124, 110, 154),
            BackColor = Color.Transparent
        };
        pnlSidebar.Controls.Add(lblNavigationTitle);

        int navY = 98;
        int navH = 36;
        int navGap = 6;
        int navW = sidebarWidth - 32;
        List<ModernButton> sidebarNavButtons = new();

        void StyleSidebarNavButton(ModernButton button, bool active)
        {
            button.NormalColor = active ? Color.FromArgb(28, 21, 48) : Color.Transparent;
            button.HoverColor = active ? Color.FromArgb(36, 27, 58) : Color.FromArgb(28, 21, 48);
            button.PressedColor = Color.FromArgb(52, 42, 90);
            button.BorderColor = active ? Color.FromArgb(124, 58, 237) : Color.Transparent;
            button.HasBorder = active;
            button.ForeColor = active ? Color.FromArgb(245, 243, 255) : Color.FromArgb(181, 168, 211);
        }

        ModernButton CreateSidebarNavBtn(string text, Control? target, int idx)
        {
            var btn = new ModernButton
            {
                Text = text,
                Location = new Point(16, navY + (navH + navGap) * idx),
                Size = new Size(navW, navH),
                BorderRadius = 8,
                Font = AppFonts.GetBoldFont(8f)
            };
            sidebarNavButtons.Add(btn);
            StyleSidebarNavButton(btn, idx == 0);
            btn.Click += (s, e) =>
            {
                foreach (ModernButton navButton in sidebarNavButtons)
                    StyleSidebarNavButton(navButton, ReferenceEquals(navButton, btn));

                if (target != null)
                    pnlMainContent.ScrollControlIntoView(target);
            };
            return btn;
        }

        pnlSidebar.Controls.Add(CreateSidebarNavBtn("Surveillance", pnlStatusBanner, 0));
        pnlSidebar.Controls.Add(CreateSidebarNavBtn("Viseur [F9]", pnlCrosshairCard, 1));
        pnlSidebar.Controls.Add(CreateSidebarNavBtn("Graphiques", pnlGraphContainer, 2));
        pnlSidebar.Controls.Add(CreateSidebarNavBtn("Souris [F8]", pnlSnappingSettings, 3));
        pnlSidebar.Controls.Add(CreateSidebarNavBtn("Processus et services", pnlExtremeCard, 4));
        pnlSidebar.Controls.Add(CreateSidebarNavBtn("Audio et micro", pnlSonarAudioCard, 5));
        pnlSidebar.Controls.Add(CreateSidebarNavBtn("Resolution", pnlResolutionCard, 6));
        pnlSidebar.Controls.Add(CreateSidebarNavBtn("Clavier SOCD [F10]", pnlSocdCard, 7));
        pnlSidebar.Controls.Add(CreateSidebarNavBtn("Fond d'ecran", pnlWallpaperCard, 8));
        pnlSidebar.Controls.Add(CreateSidebarNavBtn("Console d'activité", pnlLogCard, 9));

        var btnSidebarAdvisor = new ModernButton
        {
            Text = "Optimisations PC",
            Location = new Point(16, navY + (navH + navGap) * 10 + 12),
            Size = new Size(navW, 38),
            BorderRadius = 8,
            Font = AppFonts.GetBoldFont(8f),
            NormalColor = Color.FromArgb(124, 58, 237),
            HoverColor = Color.FromArgb(168, 85, 247),
            BorderColor = Color.FromArgb(60, 48, 105),
            ForeColor = Color.White
        };
        btnSidebarAdvisor.Click += (s, e) => OpenOptimizationAdvisor();
        pnlSidebar.Controls.Add(btnSidebarAdvisor);

        Panel sidebarDivider = new Panel
        {
            Location = new Point(sidebarWidth - 1, 0),
            Size = new Size(1, this.ClientSize.Height),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right,
            BackColor = Color.FromArgb(40, 32, 64)
        };
        pnlSidebar.Controls.Add(sidebarDivider);

        Panel headerDivider = new Panel
        {
            Location = new Point(0, headerHeight - 1),
            Size = new Size(pnlHeader.ClientSize.Width, 1),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.FromArgb(40, 32, 64)
        };
        pnlHeader.Controls.Add(headerDivider);

        this.Controls.Add(pnlMainContent);
        this.Controls.Add(pnlSidebar);
        this.Controls.Add(pnlHeader);
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
        lblSoundArtist.Text = string.IsNullOrWhiteSpace(audioTracker.CurrentArtist) ? "Spotify" : audioTracker.CurrentArtist;
        lblSoundSource.Text = "Spotify";
        if (btnSpotifyPlayPause != null)
        {
            btnSpotifyPlayPause.IsPlaying = audioTracker.IsPlaying;
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
            using (Brush brush = new SolidBrush(Color.FromArgb(35, 35, 35)))
            {
                g.FillPath(brush, path);
            }
            using (Pen pen = new Pen(Color.FromArgb(55, 55, 55), 1f))
            {
                g.DrawPath(pen, path);
            }
        }

        using (Pen pen = new Pen(Color.White, 2.2f))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            g.DrawArc(pen, 12, 11, 20, 20, 180, 180);
            g.DrawLine(pen, 12, 21, 12, 28);
            g.DrawLine(pen, 32, 21, 32, 28);
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
            HoverColor = active ? Color.FromArgb(168, 85, 247) : Color.FromArgb(36, 27, 58),
            PressedColor = Color.FromArgb(109, 40, 217),
            ForeColor = active ? Color.FromArgb(245, 243, 255) : Color.FromArgb(181, 168, 211),
            Font = AppFonts.GetBoldFont(9f)
        };
        return btn;
    }

    private void SwitchGraphMode(string mode)
    {
        Color activeBg = Color.FromArgb(124, 58, 237);
        Color inactiveBg = Color.Transparent;
        Color activeFg = Color.FromArgb(245, 243, 255);
        Color inactiveFg = Color.FromArgb(181, 168, 211);

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
            NormalColor = Color.FromArgb(36, 27, 58),
            HoverColor = Color.FromArgb(52, 42, 90),
            PressedColor = Color.FromArgb(28, 21, 48),
            BorderColor = Color.FromArgb(60, 48, 105),
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
            BorderRadius = 12,
            FillColor = Color.FromArgb(22, 17, 44),
            BorderColor = Color.FromArgb(60, 48, 105),
            Padding = new Padding(14, 8, 14, 8)
        };

        Label lbl = new Label
        {
            Text = label,
            Font = AppFonts.GetBoldFont(8f),
            ForeColor = Color.FromArgb(140, 140, 140),
            Location = new Point(12, 10),
            AutoSize = true,
            BackColor = Color.Transparent
        };

        valLabel = new Label
        {
            Text = defaultValue,
            Font = AppFonts.GetBoldFont(13f),
            ForeColor = accentColor,
            Location = new Point(12, 32),
            AutoSize = true,
            BackColor = Color.Transparent
        };

        card.Controls.Add(lbl);
        card.Controls.Add(valLabel);
        return card;
    }

    private ModernCard CreateResourceMetricCard(string title, string initialValue, string status, Color accent, bool showProgress, out Label valueLabel, out Panel? progressTrack, out Panel? progressFill)
    {
        ModernCard card = new ModernCard
        {
            Location = Point.Empty,
            Size = new Size(320, 116),
            BorderRadius = 12,
            FillColor = Color.FromArgb(22, 17, 44),
            BorderColor = Color.FromArgb(60, 48, 105),
            Padding = new Padding(16, 10, 16, 10)
        };
        Label eyebrow = new Label
        {
            Text = "MESURE SYSTÈME",
            Font = AppFonts.GetBoldFont(7.5f),
            ForeColor = Color.FromArgb(140, 140, 140),
            Location = new Point(16, 10),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        Label titleLabel = new Label
        {
            Text = title,
            Font = AppFonts.GetBoldFont(9f),
            ForeColor = Color.White,
            Location = new Point(16, 29),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        valueLabel = new Label
        {
            Text = initialValue,
            Font = AppFonts.GetTitleFont(24f),
            ForeColor = accent,
            Location = new Point(16, 49),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        Label statusLabel = new Label
        {
            Text = status,
            Font = AppFonts.GetBoldFont(7.2f),
            ForeColor = accent,
            Location = new Point(card.Width - 100, 16),
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.Transparent
        };
        card.Controls.Add(eyebrow);
        card.Controls.Add(titleLabel);
        card.Controls.Add(valueLabel);
        card.Controls.Add(statusLabel);
        progressTrack = null;
        progressFill = null;
        if (showProgress)
        {
            Panel track = new Panel
            {
                Location = new Point(16, 96),
                Size = new Size(card.Width - 32, 5),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.FromArgb(35, 35, 35),
                Tag = 0f
            };
            Panel fill = new Panel
            {
                Location = Point.Empty,
                Size = new Size(0, 5),
                BackColor = accent
            };
            track.Controls.Add(fill);
            track.Resize += (s, e) =>
            {
                float percent = track.Tag is float current ? current : 0f;
                fill.Width = Math.Clamp((int)Math.Round(track.ClientSize.Width * percent / 100f), 0, track.ClientSize.Width);
            };
            card.Controls.Add(track);
            progressTrack = track;
            progressFill = fill;
        }
        return card;
    }

    private void UpdateHardwareMetrics()
    {
        if (hardwareGraph == null || lblCpuValue == null || lblRamValue == null || pnlCpuProgressTrack == null || pnlCpuProgressFill == null || pnlRamProgressTrack == null || pnlRamProgressFill == null)
            return;
        float? cpu = hardwareGraph.CpuUsage;
        float ram = Math.Clamp(hardwareGraph.RamUsage, 0f, 100f);
        lblCpuValue.Text = cpu.HasValue ? $"{cpu.Value:0}%" : "N/D";
        lblRamValue.Text = $"{ram:0}%";
        lblGpuValue.Text = "N/D";
        SetMetricProgress(pnlCpuProgressTrack!, pnlCpuProgressFill!, cpu ?? 0f);
        SetMetricProgress(pnlRamProgressTrack!, pnlRamProgressFill!, ram);
    }

    private static void SetMetricProgress(Panel track, Panel fill, float value)
    {
        float percent = Math.Clamp(value, 0f, 100f);
        track.Tag = percent;
        fill.Width = Math.Clamp((int)Math.Round(track.ClientSize.Width * percent / 100f), 0, track.ClientSize.Width);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Color.FromArgb(40, 40, 40), 1f);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    private void SetupTray()
    {
        trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Afficher GameEcoBot", null, (s, e) => ShowAndRestore());
        trayMenu.Items.Add("Assistant d'optimisation", null, (s, e) => OpenOptimizationAdvisor());
        trayMenu.Items.Add("Recherche de jeu", null, async (s, e) => await intelligenceService.BoostIntelligenceAsync());
        trayMenu.Items.Add("Basculer Angle Snapping (F8)", null, (s, e) => ToggleAngleSnapping());
        trayMenu.Items.Add("Basculer Viseur Crosshair (F9)", null, (s, e) => ToggleCrosshair());
        trayMenu.Items.Add("Activer/Désactiver Mode Éco", null, (s, e) => BtnToggleEco_Click(s, e));
        trayMenu.Items.Add("Purger la RAM", null, (s, e) => BtnCleanRam_Click(s, e));
        trayMenu.Items.Add("-");
        trayMenu.Items.Add("Quitter complètement", null, (s, e) => ExitApp());

        trayIcon = new NotifyIcon
        {
            Text = "GameEcoBot",
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
        NativeMethods.RegisterHotKey(this.Handle, HOTKEY_ID_F8, 0, 0x77); // F8
        NativeMethods.RegisterHotKey(this.Handle, HOTKEY_ID_F9, 0, 0x78); // F9
        NativeMethods.RegisterHotKey(this.Handle, HOTKEY_ID_F10, 0, 0x79); // F10
        // Le hook WH_MOUSE_LL est attaché UNIQUEMENT à la demande pour garantir 0ms d'overhead quand inactif
    }

    private void StartAngleSnappingThread()
    {
        if (angleHookThread != null && angleHookThread.IsAlive) return;

        hasLastMousePos = false;
        isSnappingInjected = false;

        using var readyEvent = new ManualResetEvent(false);

        angleHookThread = new Thread(() =>
        {
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            angleHookThreadId = NativeMethods.GetCurrentThreadId();

            mouseProc = MouseHookCallback;
            IntPtr hMod = NativeMethods.GetModuleHandle(null);
            mouseHookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, mouseProc, hMod, 0);

            readyEvent.Set();

            Application.Run();

            if (mouseHookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(mouseHookId);
                mouseHookId = IntPtr.Zero;
            }
        });

        angleHookThread.IsBackground = true;
        angleHookThread.SetApartmentState(ApartmentState.STA);
        angleHookThread.Start();
        readyEvent.WaitOne(1500);
    }

    private void StopAngleSnappingThread()
    {
        if (angleHookThread != null)
        {
            if (angleHookThreadId != 0)
            {
                NativeMethods.PostThreadMessage(angleHookThreadId, 0x0012 /* WM_QUIT */, IntPtr.Zero, IntPtr.Zero);
            }
            if (angleHookThread.IsAlive)
            {
                angleHookThread.Join(500);
            }
            angleHookThread = null;
            angleHookThreadId = 0;
        }

        if (mouseHookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(mouseHookId);
            mouseHookId = IntPtr.Zero;
        }
    }

    public void ToggleAngleSnapping()
    {
        isAngleSnappingActive = !isAngleSnappingActive;
        hasLastMousePos = false;
        isSnappingInjected = false;

        if (chkAngleSnapping != null && chkAngleSnapping.Checked != isAngleSnappingActive)
            chkAngleSnapping.Checked = isAngleSnappingActive;

        if (isAngleSnappingActive)
        {
            StartAngleSnappingThread();

            btnToggleAngleSnapping.NormalColor = Color.FromArgb(16, 185, 129);
            btnToggleAngleSnapping.ForeColor = Color.White;
            btnToggleAngleSnapping.Text = $"Snapping ({angleSnappingPercent}%)";
            lblAngleStatus.Text = $"{angleSnappingPercent}% ACTIF";
            lblAngleStatus.ForeColor = Color.FromArgb(74, 222, 128);
            double pct = angleSnappingPercent / 100.0;
            double threshold = 8.0 + pct * 17.0;
            Log($"[SNAPPING] Angle Snapping ULTRA activé à {angleSnappingPercent}% (zone {threshold:0.0}°, thread dédié, zéro lag).");
        }
        else
        {
            StopAngleSnappingThread();

            btnToggleAngleSnapping.NormalColor = Color.FromArgb(28, 28, 28);
            btnToggleAngleSnapping.ForeColor = Color.White;
            btnToggleAngleSnapping.Text = "Angle Snapping [F8]";
            lblAngleStatus.Text = "DESACTIVE";
            lblAngleStatus.ForeColor = Color.FromArgb(120, 120, 120);
            Log("[SNAPPING] Angle Snapping ULTRA désactivé (Hook libéré).");
        }
    }

    public void ToggleCrosshair()
    {
        CrosshairService.Instance.Toggle();
        bool active = CrosshairService.Instance.IsActive;

        if (chkCrosshair != null && chkCrosshair.Checked != active)
            chkCrosshair.Checked = active;

        if (btnToggleCrosshair != null)
        {
            btnToggleCrosshair.NormalColor = active ? Color.FromArgb(16, 185, 129) : Color.FromArgb(28, 28, 28);
            btnToggleCrosshair.ForeColor = Color.White;
            btnToggleCrosshair.Text = active ? "Viseur : ACTIF" : "Viseur [F9]";
        }

        if (lblCrosshairStatus != null)
        {
            lblCrosshairStatus.Text = active ? "ACTIF" : "DESACTIVE";
            lblCrosshairStatus.ForeColor = active ? Color.FromArgb(74, 222, 128) : Color.FromArgb(120, 120, 120);
        }

        crosshairPreview?.Invalidate();

        Log(active
            ? $"[CROSSHAIR] Viseur {CrosshairService.Instance.Settings.Shape} activé au centre de l'écran (0ms, clic traversant, [F9])."
            : "[CROSSHAIR] Viseur désactivé.");
    }

    public void ToggleSocd()
    {
        bool enable = !socdService.IsEnabled;
        socdService.SetEnabled(enable);

        if (chkSocd != null && chkSocd.Checked != enable)
            chkSocd.Checked = enable;

        string pair = socdService.Layout == SocdLayout.Azerty ? "Q / D" : "A / D";
        if (lblSocdStatus != null)
        {
            lblSocdStatus.Text = enable ? $"ACTIF ({pair})" : "DESACTIVE";
            lblSocdStatus.ForeColor = enable ? Color.FromArgb(74, 222, 128) : Color.FromArgb(120, 120, 120);
        }

        Log(enable
            ? $"[SOCD] Clavier Snap Tap activé ({pair}, thread dédié, [F10])."
            : "[SOCD] Clavier Snap Tap désactivé (Hook libéré).");
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || wParam != (IntPtr)NativeMethods.WM_MOUSEMOVE || !isAngleSnappingActive)
            return NativeMethods.CallNextHookEx(mouseHookId, nCode, wParam, lParam);

        int curX = Marshal.ReadInt32(lParam, 0);  // MSLLHOOKSTRUCT.pt.x = offset 0
        int curY = Marshal.ReadInt32(lParam, 4);  // MSLLHOOKSTRUCT.pt.y = offset 4
        uint flags = (uint)Marshal.ReadInt32(lParam, 12); // flags = offset 12

        // Skip les événements injectés par notre SetCursorPos
        if ((flags & 0x01) != 0 || isSnappingInjected)
        {
            isSnappingInjected = false;
            lastMouseX = curX;
            lastMouseY = curY;
            return NativeMethods.CallNextHookEx(mouseHookId, nCode, wParam, lParam);
        }

        if (!hasLastMousePos)
        {
            lastMouseX = curX;
            lastMouseY = curY;
            hasLastMousePos = true;
            return NativeMethods.CallNextHookEx(mouseHookId, nCode, wParam, lParam);
        }

        int dx = curX - lastMouseX;
        int dy = curY - lastMouseY;

        // Aucun déplacement
        if (dx == 0 && dy == 0)
            return NativeMethods.CallNextHookEx(mouseHookId, nCode, wParam, lParam);

        int absDx = Math.Abs(dx);
        int absDy = Math.Abs(dy);
        int speedSq = dx * dx + dy * dy;

        // Protection téléportation curseur (>350px)
        if (absDx > 350 || absDy > 350)
        {
            lastMouseX = curX;
            lastMouseY = curY;
            return NativeMethods.CallNextHookEx(mouseHookId, nCode, wParam, lParam);
        }

        // Flicks ultra-rapides (>150px) → laisser 100% libre pour les 180° et réflexes
        if (speedSq > 22500)
        {
            lastMouseX = curX;
            lastMouseY = curY;
            return NativeMethods.CallNextHookEx(mouseHookId, nCode, wParam, lParam);
        }

        double angle = Math.Atan2(absDy, absDx) * (180.0 / Math.PI);

        // Paramètres boostés ultra-agressifs basés sur angleSnappingPercent (1-100)
        double pct = angleSnappingPercent / 100.0;
        double axisThreshold = 8.0 + pct * 17.0;  // jusqu'à 25° de zone de capture !
        double diagThreshold = 4.0 + pct * 8.0;   // jusqu'à 12° sur diagonale 45°
        double strength = 0.80 + pct * 0.20;      // jusqu'à 1.0 (verrouillage absolu 100%)

        // Boost supplémentaire pour le micro-aim (mouvements précis < 10px)
        if (speedSq < 100)
        {
            axisThreshold = Math.Min(28.0, axisThreshold + 3.0);
            strength = 1.0;
        }

        int newX = curX;
        int newY = curY;
        bool shouldSnap = false;

        // 1. SNAP HORIZONTAL (0°) — Verrouillage à niveau de tête parfait
        if (angle <= axisThreshold && absDx >= 1)
        {
            newY = (strength >= 1.0) ? lastMouseY : lastMouseY + (int)Math.Round(dy * (1.0 - strength));
            newX = curX;
            shouldSnap = (newY != curY);
        }
        // 2. SNAP VERTICAL (90°) — Contrôle du recul parfait
        else if ((90.0 - angle) <= axisThreshold && absDy >= 1)
        {
            newX = (strength >= 1.0) ? lastMouseX : lastMouseX + (int)Math.Round(dx * (1.0 - strength));
            newY = curY;
            shouldSnap = (newX != curX);
        }
        // 3. SNAP DIAGONAL (45°) — Ratio 1:1 parfait
        else if (Math.Abs(angle - 45.0) <= diagThreshold && absDx >= 1 && absDy >= 1)
        {
            int mag = (absDx + absDy) / 2;
            if (mag < 1) mag = 1;
            int targetDx = mag * Math.Sign(dx);
            int targetDy = mag * Math.Sign(dy);
            newX = lastMouseX + targetDx;
            newY = lastMouseY + targetDy;
            shouldSnap = (newX != curX || newY != curY);
        }

        if (shouldSnap)
        {
            isSnappingInjected = true;
            lastMouseX = newX;
            lastMouseY = newY;
            NativeMethods.SetCursorPos(newX, newY);
            return (IntPtr)1; // Bloquer l'événement physique non-snappé
        }

        lastMouseX = curX;
        lastMouseY = curY;
        return NativeMethods.CallNextHookEx(mouseHookId, nCode, wParam, lParam);
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_HOTKEY = 0x0312;
        if (m.Msg == WM_HOTKEY)
        {
            int hotkeyId = m.WParam.ToInt32();
            if (hotkeyId == HOTKEY_ID_F8)
            {
                ToggleAngleSnapping();
            }
            else if (hotkeyId == HOTKEY_ID_F9)
            {
                ToggleCrosshair();
            }
            else if (hotkeyId == HOTKEY_ID_F10)
            {
                ToggleSocd();
            }
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
        NativeMethods.SetUltimateGamingPowerPlan(false);
        NativeMethods.RestoreNormalPowerPlan();
        NativeMethods.EnableHighPrecisionTimer(false);
        NativeMethods.EnableMouseNoDelay(false);
        StopAngleSnappingThread();
        socdService.Dispose();
        NativeMethods.UnregisterHotKey(this.Handle, HOTKEY_ID_F8);
        NativeMethods.UnregisterHotKey(this.Handle, HOTKEY_ID_F9);
        NativeMethods.UnregisterHotKey(this.Handle, HOTKEY_ID_F10);
        CrosshairService.Instance.Hide();
        // Restaurer Ultra Eco et Services avant de quitter
        if (isUltraEcoActive) ultraEcoManager.Deactivate();
        if (isServiceCleanActive && GamingServiceManager.IsGamingModeActive) GamingServiceManager.RestoreServices();
        ultraEcoManager.Dispose();
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
            return;
        }

        ExitApp();
    }

    private static string CleanGameDisplayName(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return "En attente...";
        if (processName.StartsWith("VALORANT", StringComparison.OrdinalIgnoreCase)) return "VALORANT";
        if (processName.StartsWith("FortniteClient", StringComparison.OrdinalIgnoreCase)) return "Fortnite";
        if (processName.StartsWith("RobloxPlayer", StringComparison.OrdinalIgnoreCase)) return "Roblox";
        if (processName.Equals("cs2", StringComparison.OrdinalIgnoreCase)) return "Counter-Strike 2";
        if (processName.Equals("csgo", StringComparison.OrdinalIgnoreCase)) return "CS:GO";
        if (processName.Equals("r5apex", StringComparison.OrdinalIgnoreCase)) return "Apex Legends";
        if (processName.Equals("javaw", StringComparison.OrdinalIgnoreCase)) return "Minecraft";
        if (processName.Equals("TslGame", StringComparison.OrdinalIgnoreCase)) return "PUBG";
        if (processName.Equals("Overwatch", StringComparison.OrdinalIgnoreCase)) return "Overwatch";
        if (processName.Equals("GTA5", StringComparison.OrdinalIgnoreCase)) return "GTA V";
        if (processName.Equals("FiveM_GTAProcess", StringComparison.OrdinalIgnoreCase)) return "FiveM";
        if (processName.EndsWith("-Win64-Shipping", StringComparison.OrdinalIgnoreCase))
            return processName[..^15];
        return processName;
    }

    private async void MonitorTimer_Tick(object? sender, EventArgs e)
    {
        await CheckActiveGameAndApplyEcoAsync();
    }

    private async Task CheckActiveGameAndApplyEcoAsync()
    {
        if (isDetectionRunning) return;
        isDetectionRunning = true;

        try
        {
            // Mettre à jour l'audio en direct (ce que j'entends)
            _ = audioTracker.UpdateAsync();

            var processes = Process.GetProcesses();

            int currentPid = Process.GetCurrentProcess().Id;
            Process? detectedGame = null;

            // 1. PRIORITÉ ABSOLUE : Fenêtre active au premier plan (Foreground Window)
            IntPtr fgHwnd = NativeMethods.GetForegroundWindow();
            if (fgHwnd != IntPtr.Zero)
            {
                NativeMethods.GetWindowThreadProcessId(fgHwnd, out int fgPid);
                if (fgPid > 0 && fgPid != currentPid)
                {
                    try
                    {
                        var fgProc = processes.FirstOrDefault(p => p.Id == fgPid);
                        if (fgProc != null && !GameIntelligenceService.IsExcluded(fgProc.ProcessName))
                        {
                            if (intelligenceService.KnownGames.Contains(fgProc.ProcessName))
                            {
                                detectedGame = fgProc;
                            }
                            else
                            {
                                if (await Task.Run(() => intelligenceService.IsGameAsync(fgProc)))
                                {
                                    detectedGame = fgProc;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            // 2. PERSISTANCE ALT-TAB : Si un jeu était déjà actif, vérifier s'il tourne toujours en arrière-plan
            if (detectedGame == null && isGameActive && !string.IsNullOrEmpty(activeGameName))
            {
                var prevProc = processes.FirstOrDefault(p => p.ProcessName.Equals(activeGameName, StringComparison.OrdinalIgnoreCase));
                if (prevProc != null && !prevProc.HasExited)
                {
                    detectedGame = prevProc;
                }
            }

            // 3. SCAN DES JEUX EN ARRIÈRE-PLAN : Vérifier si un jeu connu tourne parmi les processus
            if (detectedGame == null)
            {
                detectedGame = processes
                    .Where(p => p.Id != currentPid && !GameIntelligenceService.IsExcluded(p.ProcessName) && intelligenceService.KnownGames.Contains(p.ProcessName))
                    .OrderByDescending(p =>
                    {
                        try { return p.WorkingSet64; } catch { return 0L; }
                    })
                    .FirstOrDefault();
            }

            if (detectedGame != null)
            {
                string cleanName = CleanGameDisplayName(detectedGame.ProcessName);

                if (!isGameActive || activeGameName != detectedGame.ProcessName)
                {
                    isGameActive = true;
                    WallpaperEngineService.Instance.OnGameStatusChanged(true);
                    if (CrosshairService.Instance.Settings.AutoShowOnGame && !CrosshairService.Instance.IsActive) { this.BeginInvoke(() => ToggleCrosshair()); }
                    activeGameName = detectedGame.ProcessName;
                    lblActiveGame.Text = cleanName;

                    // 1. Boost Priorité Haute Processus Jeu
                    NativeMethods.BoostGameProcess(detectedGame.Id);
                    // 2. Timer Windows 1ms Haute Précision
                    NativeMethods.EnableHighPrecisionTimer(true);
                    // Le plan hautes performances ne s'active que si l'option Turbo CPU en jeu est cochée.
                    if (chkAutoTurbo != null && chkAutoTurbo.Checked)
                    {
                        bool activated = NativeMethods.SetUltimateGamingPowerPlan(true);
                        Log(activated ? $"Plan de jeu activé pour {cleanName}." : "[ATTENTION] Windows n'a pas pu activer le plan de jeu.");
                    }
                    else
                    {
                        NativeMethods.SetUltimateGamingPowerPlan(false);
                    }

                    Log($"[JEU] Jeu détecté : {cleanName} ({activeGameName}). Priorité haute et timer précis activés.");

                    pnlStatusBanner.FillColor = Color.FromArgb(22, 17, 44);
                    lblStatusHeader.Text = $"MODE JEU ACTIF - {cleanName.ToUpper()}";
                    lblStatusHeader.ForeColor = Color.White;

                    if ((chkAutoDetect != null && chkAutoDetect.Checked) || isManualEcoActive)
                        ApplyEcoToAllBackgroundApps(processes, detectedGame.Id);
                }
                else
                {
                    lblActiveGame.Text = cleanName;
                    if ((chkAutoDetect != null && chkAutoDetect.Checked) || isManualEcoActive)
                        ApplyEcoToAllBackgroundApps(processes, detectedGame.Id);
                }
            }
            else
            {
                if (isGameActive)
                {
                    isGameActive = false;
                    WallpaperEngineService.Instance.OnGameStatusChanged(false);
                    if (CrosshairService.Instance.Settings.AutoShowOnGame && CrosshairService.Instance.IsActive) { this.BeginInvoke(() => ToggleCrosshair()); }
                    NativeMethods.EnableHighPrecisionTimer(false);
                    bool planRestored = NativeMethods.SetUltimateGamingPowerPlan(false);
                    Log(planRestored
                        ? $"[INFO] {activeGameName} s'est arrêté. Plan d'alimentation précédent rétabli."
                        : $"[ATTENTION] {activeGameName} s'est arrêté, mais Windows n'a pas pu restaurer le plan précédent.");
                    activeGameName = "";
                    lblActiveGame.Text = "En attente...";

                    if (!isManualEcoActive)
                    {
                        RestoreAllProcessesToNormal();
                        pnlStatusBanner.FillColor = Color.FromArgb(22, 17, 44);
                        lblStatusHeader.Text = "EN VEILLE - PRET POUR LE JEU";
                        lblStatusHeader.ForeColor = Color.White;
                    }
                }
                else if (isManualEcoActive)
                {
                    ApplyEcoToAllBackgroundApps(processes, -1);
                }
                else
                {
                    lblActiveGame.Text = "En attente...";
                }
            }
        }
        catch { }
        finally
        {
            isDetectionRunning = false;
        }
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
        hardwareGraph.SyncThrottledPids(currentlyThrottledPids);
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
        hardwareGraph.SyncThrottledPids(currentlyThrottledPids);
        Log($"[ECO] {restored} applications restaurees en mode normal.");
    }

    private void BtnToggleEco_Click(object? sender, EventArgs e)
    {
        isManualEcoActive = !isManualEcoActive;
        if (isManualEcoActive)
        {
            btnToggleEco.Text = "Mode Eco : ACTIF";
            btnToggleEco.NormalColor = Color.FromArgb(16, 185, 129);
            pnlStatusBanner.FillColor = Color.FromArgb(22, 17, 44);
            lblStatusHeader.Text = "MODE ECO MANUEL ACTIF";
            lblStatusHeader.ForeColor = Color.FromArgb(74, 222, 128);
            Log("[ECO] Mode Eco force manuellement.");
            ApplyEcoToAllBackgroundApps(Process.GetProcesses(), -1);
            hardwareGraph.SyncThrottledPids(currentlyThrottledPids);
        }
        else
        {
            btnToggleEco.Text = "Activer Mode Eco";
            btnToggleEco.NormalColor = Color.FromArgb(28, 28, 28);
            pnlStatusBanner.FillColor = Color.FromArgb(22, 17, 44);
            lblStatusHeader.Text = "EN VEILLE - PRET POUR LE JEU";
            lblStatusHeader.ForeColor = Color.White;
            RestoreAllProcessesToNormal();
            hardwareGraph.SyncThrottledPids(currentlyThrottledPids);
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
        Log($"[RAM] Cache mémoire vidé pour {count} processus.");
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
                Log("[WINDOWS] Démarrage automatique avec Windows activé.");
            }
            else
            {
                key?.DeleteValue("GameEcoBot", false);
                Log("[WINDOWS] Démarrage automatique avec Windows désactivé.");
            }
        }
        catch (Exception ex)
        {
            Log($"Erreur démarrage: {ex.Message}");
        }
    }

    private void Log(string message)
    {
        if (rtbLogs == null || rtbLogs.IsDisposed) return;
        try
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            rtbLogs.AppendText($"[{timestamp}] {message}\r\n");
            rtbLogs.SelectionStart = rtbLogs.Text.Length;
            rtbLogs.ScrollToCaret();
        }
        catch { }
    }

    private Icon CreateVioletAppIcon()
    {
        using Bitmap bmp = new Bitmap(32, 32);
        using Graphics g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Cercle extérieur blanc
        using (Brush brush = new SolidBrush(Color.White))
        {
            g.FillEllipse(brush, 2, 2, 28, 28);
        }

        // Cercle intérieur noir
        using (Brush innerBrush = new SolidBrush(Color.Black))
        {
            g.FillEllipse(innerBrush, 9, 9, 14, 14);
        }

        IntPtr hIcon = bmp.GetHicon();
        Icon icon = (Icon)Icon.FromHandle(hIcon).Clone();
        NativeMethods.DestroyIcon(hIcon);
        return icon;
    }
}
