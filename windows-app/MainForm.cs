using System.Diagnostics;
using System.Reflection;

namespace GameEcoBot;

internal sealed class MainForm : Form
{
    private readonly HardwareMonitor _hardware = new();
    private readonly EcoModeController _ecoMode = new();
    private readonly System.Windows.Forms.Timer _hardwareTimer = new() { Interval = 1000 };
    private readonly Panel _pageHost = new() { Dock = DockStyle.Fill, BackColor = UiTheme.Background };
    private readonly Label _pageTitle = MakeLabel("", 17f, FontStyle.Bold, UiTheme.Text);
    private readonly Label _pageDescription = MakeLabel("", 9f, FontStyle.Regular, UiTheme.Muted);
    private readonly Button _ecoHeaderButton = MakeButton("MODE ÉCO", primary: false);
    private readonly Label _ecoHeaderStatus = MakeLabel("", 8f, FontStyle.Regular, UiTheme.Muted);
    private readonly Button _dashboardEcoButton = MakeButton("ACTIVER LE MODE ÉCO", primary: true);
    private readonly Button _dashboardScanButton = MakeButton("SCANNER LES JEUX", primary: false);
    private readonly Button _gamesScanButton = MakeButton("SCANNER LES JEUX", primary: true);
    private readonly Label _scanStatus = MakeLabel(
        "Aucun scan lancé. Seules les installations trouvées sur ce PC seront affichées.",
        8f,
        FontStyle.Regular,
        UiTheme.Muted);
    private readonly Label _gamesPageScanStatus = MakeLabel(
        "Aucun scan lancé. Seules les installations trouvées sur ce PC seront affichées.",
        8f,
        FontStyle.Regular,
        UiTheme.Muted);
    private readonly Label _dashboardGameCount = MakeLabel("Aucun scan lancé", 9f, FontStyle.Bold, UiTheme.Text);
    private readonly Label _gamesPageCount = MakeLabel("Aucun scan lancé", 9f, FontStyle.Bold, UiTheme.Text);
    private readonly FlowLayoutPanel _dashboardGames = new()
    {
        AutoSize = true,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        BackColor = Color.Transparent,
        Margin = Padding.Empty,
    };
    private readonly ListView _gamesList = new();
    private readonly TextBox _gameSearch = new();
    private readonly ComboBox _refreshRate = new();
    private readonly Label _settingsRefreshInfo = MakeLabel("", 8f, FontStyle.Regular, UiTheme.Muted);
    private readonly PerformanceChart _chart = new() { Dock = DockStyle.Fill };
    private readonly Label _cpuValue = MakeLabel("—", 30f, FontStyle.Regular, UiTheme.Text);
    private readonly Label _cpuDetail = MakeLabel("Lecture des mesures…", 8f, FontStyle.Regular, UiTheme.Muted);
    private readonly Label _cpuName = MakeLabel("Processeur Windows", 8f, FontStyle.Regular, UiTheme.Muted);
    private readonly Label _ramValue = MakeLabel("—", 30f, FontStyle.Regular, UiTheme.Text);
    private readonly Label _ramDetail = MakeLabel("Lecture des mesures…", 8f, FontStyle.Regular, UiTheme.Muted);
    private readonly Label _gpuDetail = MakeLabel(
        "Aucune source de télémétrie GPU configurée.",
        8f,
        FontStyle.Regular,
        UiTheme.Muted);
    private readonly UsageBar _cpuBar = new(UiTheme.Violet);
    private readonly UsageBar _ramBar = new(UiTheme.Cyan);
    private readonly List<Button> _navigationButtons = [];
    private readonly Panel _sidebar = new()
    {
        Dock = DockStyle.Left,
        Width = 238,
        BackColor = UiTheme.Sidebar,
        Padding = new Padding(16, 18, 14, 18),
    };

    private readonly List<GameInstallation> _games = [];
    private readonly Panel _dashboardPage = new() { Dock = DockStyle.Fill, BackColor = UiTheme.Background };
    private readonly Panel _gamesPage = new() { Dock = DockStyle.Fill, BackColor = UiTheme.Background, Visible = false };
    private readonly Panel _settingsPage = new() { Dock = DockStyle.Fill, BackColor = UiTheme.Background, Visible = false };
    private bool _isScanning;

    public MainForm()
    {
        Text = "GameEcoBot";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1420, 940);
        MinimumSize = new Size(1080, 740);
        BackColor = UiTheme.Background;
        ForeColor = UiTheme.Text;
        Font = UiTheme.CreateFont(9f);
        DoubleBuffered = true;

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // The application icon is optional; the window still opens with the default icon.
        }

        BuildShell();
        BuildDashboardPage();
        BuildGamesPage();
        BuildSettingsPage();
        _pageHost.Controls.Add(_dashboardPage);
        _pageHost.Controls.Add(_gamesPage);
        _pageHost.Controls.Add(_settingsPage);
        ShowPage("overview");

        _ecoHeaderButton.Click += async (_, _) => await ToggleEcoModeAsync();
        _dashboardEcoButton.Click += async (_, _) => await ToggleEcoModeAsync();
        _dashboardScanButton.Click += async (_, _) => await ScanGamesAsync();
        _gamesScanButton.Click += async (_, _) => await ScanGamesAsync();
        _hardwareTimer.Tick += (_, _) => RefreshHardware();
        _hardwareTimer.Start();
        RefreshHardware();
        UpdateEcoState();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _hardwareTimer.Stop();
        _hardwareTimer.Dispose();
        base.OnFormClosed(e);
    }

    private void BuildShell()
    {
        var main = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Background };
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 94,
            BackColor = UiTheme.Background,
            Padding = new Padding(30, 17, 30, 15),
        };

        _pageTitle.Location = new Point(30, 15);
        _pageTitle.AutoSize = true;
        _pageDescription.Location = new Point(31, 49);
        _pageDescription.AutoSize = true;

        _ecoHeaderButton.Size = new Size(156, 39);
        _ecoHeaderButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _ecoHeaderButton.Location = new Point(
            Math.Max(250, header.ClientSize.Width - _ecoHeaderButton.Width - 30),
            16);
        _ecoHeaderStatus.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _ecoHeaderStatus.AutoSize = true;
        _ecoHeaderStatus.Location = new Point(
            _ecoHeaderButton.Left,
            _ecoHeaderButton.Bottom + 2);
        header.Resize += (_, _) =>
        {
            _ecoHeaderButton.Left = Math.Max(250, header.ClientSize.Width - _ecoHeaderButton.Width - 30);
            _ecoHeaderStatus.Left = _ecoHeaderButton.Left;
        };

        header.Controls.Add(_pageTitle);
        header.Controls.Add(_pageDescription);
        header.Controls.Add(_ecoHeaderButton);
        header.Controls.Add(_ecoHeaderStatus);

        _pageHost.Padding = new Padding(0);
        main.Controls.Add(_pageHost);
        main.Controls.Add(header);
        Controls.Add(main);
        Controls.Add(_sidebar);
        BuildSidebar();
    }

    private void BuildSidebar()
    {
        var brand = new Panel { Dock = DockStyle.Top, Height = 92 };
        var orb = new BrandOrb { Location = new Point(2, 4), Size = new Size(48, 48) };
        var brandName = MakeLabel("GAMEECOBOT", 11f, FontStyle.Bold, UiTheme.Text);
        brandName.Location = new Point(62, 10);
        brandName.AutoSize = true;
        var brandSub = MakeLabel("SURVEILLANCE PC", 7f, FontStyle.Regular, UiTheme.Muted);
        brandSub.Location = new Point(63, 36);
        brandSub.AutoSize = true;
        brand.Controls.Add(orb);
        brand.Controls.Add(brandName);
        brand.Controls.Add(brandSub);

        var navigation = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
        };
        navigation.Controls.Add(MakeNavButton("VUE D’ENSEMBLE", "overview"));
        navigation.Controls.Add(MakeNavButton("JEUX INSTALLÉS", "games"));
        navigation.Controls.Add(MakeNavButton("RÉGLAGES", "settings"));

        var appVersion = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "1.0.8";
        var footer = MakeLabel(
            $"VERSION {appVersion}\nLes mesures et scans restent sur cet ordinateur.",
            7.5f,
            FontStyle.Regular,
            UiTheme.Muted);
        footer.Dock = DockStyle.Bottom;
        footer.Height = 58;
        footer.TextAlign = ContentAlignment.BottomLeft;

        _sidebar.Controls.Add(footer);
        _sidebar.Controls.Add(navigation);
        _sidebar.Controls.Add(brand);
    }

    private Button MakeNavButton(string text, string page)
    {
        var button = MakeButton(text, primary: false);
        button.Width = 204;
        button.Height = 42;
        button.Margin = new Padding(0, 0, 0, 7);
        button.TextAlign = ContentAlignment.MiddleLeft;
        button.Padding = new Padding(13, 0, 0, 0);
        button.Tag = page;
        button.Click += (_, _) => ShowPage(page);
        _navigationButtons.Add(button);
        return button;
    }

    private void BuildDashboardPage()
    {
        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = UiTheme.Background,
            Padding = new Padding(30, 14, 30, 28),
        };
        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
        };

        var metrics = new TableLayoutPanel
        {
            Height = 160,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 16),
            BackColor = Color.Transparent,
        };
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334f));
        metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        metrics.Controls.Add(BuildMetricCard(
            "MESURE SYSTÈME",
            "PROCESSEUR · CPU",
            _cpuValue,
            _cpuDetail,
            _cpuName,
            _cpuBar), 0, 0);
        metrics.Controls.Add(BuildMetricCard(
            "MESURE SYSTÈME",
            "MÉMOIRE · RAM",
            _ramValue,
            _ramDetail,
            null,
            _ramBar), 1, 0);
        metrics.Controls.Add(BuildMetricCard(
            "TÉLÉMÉTRIE",
            "CARTE GRAPHIQUE · GPU",
            MakeLabel("N/D", 30f, FontStyle.Regular, UiTheme.Muted),
            _gpuDetail,
            null,
            null), 2, 0);

        var graphCard = new GlassPanel
        {
            Height = 306,
            Padding = new Padding(19, 16, 19, 15),
            Margin = new Padding(0, 0, 0, 16),
        };
        var graphTitle = MakeLabel("ÉVOLUTION DES PERFORMANCES", 10f, FontStyle.Bold, UiTheme.Text);
        graphTitle.Location = new Point(19, 16);
        graphTitle.AutoSize = true;
        var graphDescription = MakeLabel("Historique local des mesures CPU et RAM.", 8f, FontStyle.Regular, UiTheme.Muted);
        graphDescription.Location = new Point(20, 39);
        graphDescription.AutoSize = true;
        var legendCpu = MakeLegend("CPU", UiTheme.VioletLight);
        var legendRam = MakeLegend("RAM", UiTheme.Cyan);
        _chart.Dock = DockStyle.None;
        graphCard.Controls.Add(graphTitle);
        graphCard.Controls.Add(graphDescription);
        graphCard.Controls.Add(legendCpu);
        graphCard.Controls.Add(legendRam);
        graphCard.Controls.Add(_chart);
        graphCard.Resize += (_, _) =>
        {
            legendRam.Location = new Point(graphCard.Width - 92, 18);
            legendCpu.Location = new Point(graphCard.Width - 168, 18);
            _chart.Location = new Point(18, 62);
            _chart.Size = new Size(Math.Max(100, graphCard.Width - 36), Math.Max(100, graphCard.Height - 76));
        };
        graphCard.PerformLayout();

        var lower = new TableLayoutPanel
        {
            Height = 252,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            BackColor = Color.Transparent,
        };
        lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        lower.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        lower.Controls.Add(BuildGamesPreview(), 0, 0);
        lower.Controls.Add(BuildEcoPreview(), 1, 0);

        stack.Controls.Add(metrics);
        stack.Controls.Add(graphCard);
        stack.Controls.Add(lower);
        scroll.Controls.Add(stack);
        scroll.Resize += (_, _) =>
        {
            var width = Math.Max(750, scroll.ClientSize.Width - 60);
            stack.Width = width;
            metrics.Width = width;
            graphCard.Width = width;
            lower.Width = width;
            metrics.PerformLayout();
            lower.PerformLayout();
        };
        _dashboardPage.Controls.Add(scroll);
    }

    private GlassPanel BuildMetricCard(
        string eyebrow,
        string title,
        Label value,
        Label detail,
        Label? secondary,
        UsageBar? bar)
    {
        var card = new GlassPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 10, 0),
            Padding = new Padding(16),
        };
        var eyebrowLabel = MakeLabel(eyebrow, 7f, FontStyle.Regular, UiTheme.VioletLight);
        eyebrowLabel.Location = new Point(17, 15);
        eyebrowLabel.AutoSize = true;
        var titleLabel = MakeLabel(title, 8f, FontStyle.Bold, UiTheme.Text);
        titleLabel.Location = new Point(17, 35);
        titleLabel.AutoSize = true;
        value.Location = new Point(16, 62);
        value.AutoSize = true;
        detail.Location = new Point(17, 108);
        detail.AutoSize = false;
        detail.Size = new Size(Math.Max(90, card.Width - 34), 20);
        detail.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        if (secondary is not null)
        {
            secondary.Location = new Point(17, 128);
            secondary.AutoSize = false;
            secondary.Size = new Size(Math.Max(90, card.Width - 34), 18);
            secondary.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        }

        if (bar is not null)
        {
            bar.Location = new Point(17, 151);
            bar.Size = new Size(Math.Max(80, card.Width - 34), 6);
            bar.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        }

        card.Resize += (_, _) =>
        {
            detail.Width = Math.Max(90, card.Width - 34);
            if (secondary is not null)
            {
                secondary.Width = Math.Max(90, card.Width - 34);
            }

            if (bar is not null)
            {
                bar.Width = Math.Max(80, card.Width - 34);
            }
        };
        card.Controls.Add(eyebrowLabel);
        card.Controls.Add(titleLabel);
        card.Controls.Add(value);
        card.Controls.Add(detail);
        if (secondary is not null)
        {
            card.Controls.Add(secondary);
        }

        if (bar is not null)
        {
            card.Controls.Add(bar);
        }

        return card;
    }

    private Control BuildGamesPreview()
    {
        var card = new GlassPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(18),
        };
        var title = MakeLabel("JEUX INSTALLÉS SUR CE PC", 9f, FontStyle.Bold, UiTheme.Text);
        title.Location = new Point(18, 16);
        title.AutoSize = true;
        _dashboardGameCount.Location = new Point(19, 43);
        _dashboardGameCount.AutoSize = true;
        _scanStatus.Location = new Point(19, 63);
        _scanStatus.Size = new Size(340, 18);
        _scanStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _dashboardScanButton.Size = new Size(156, 34);
        _dashboardScanButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        card.Resize += (_, _) =>
        {
            _dashboardScanButton.Location = new Point(card.Width - _dashboardScanButton.Width - 17, 14);
            _dashboardGames.Location = new Point(18, 88);
            _dashboardGames.Size = new Size(Math.Max(80, card.Width - 36), 112);
            _scanStatus.Width = Math.Max(100, card.Width - 36);
        };
        _dashboardGames.MaximumSize = new Size(600, 112);
        card.Controls.Add(title);
        card.Controls.Add(_dashboardGameCount);
        card.Controls.Add(_scanStatus);
        card.Controls.Add(_dashboardScanButton);
        card.Controls.Add(_dashboardGames);
        UpdateDashboardGamePreview();
        return card;
    }

    private Control BuildEcoPreview()
    {
        var card = new GlassPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 0, 0, 0),
            Padding = new Padding(18),
        };
        var eyebrow = MakeLabel("RÉGLAGE WINDOWS", 7f, FontStyle.Regular, UiTheme.VioletLight);
        eyebrow.Location = new Point(18, 18);
        eyebrow.AutoSize = true;
        var title = MakeLabel("Mode Éco", 16f, FontStyle.Bold, UiTheme.Text);
        title.Location = new Point(18, 43);
        title.AutoSize = true;
        var description = MakeLabel(
            "Active le plan Économie d’énergie de Windows. Le plan précédent est mémorisé pour pouvoir le restaurer.",
            8f,
            FontStyle.Regular,
            UiTheme.Muted);
        description.Location = new Point(19, 78);
        description.Size = new Size(270, 58);
        description.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        description.AutoEllipsis = true;
        _dashboardEcoButton.Location = new Point(18, 155);
        _dashboardEcoButton.Size = new Size(190, 37);
        _dashboardEcoButton.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        card.Controls.Add(eyebrow);
        card.Controls.Add(title);
        card.Controls.Add(description);
        card.Controls.Add(_dashboardEcoButton);
        return card;
    }

    private void BuildGamesPage()
    {
        var content = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(30, 16, 30, 28),
            BackColor = UiTheme.Background,
        };
        var card = new GlassPanel { Dock = DockStyle.Top, Height = 630, Padding = new Padding(20) };
        var intro = MakeLabel(
            "Le scan lit uniquement les manifestes présents sur ce PC. Aucun catalogue en ligne n’est utilisé.",
            8f,
            FontStyle.Regular,
            UiTheme.Muted);
        intro.Location = new Point(20, 20);
        intro.AutoSize = true;
        _gameSearch.PlaceholderText = "Filtrer les jeux détectés";
        _gameSearch.Location = new Point(20, 58);
        _gameSearch.Size = new Size(340, 34);
        _gameSearch.BackColor = UiTheme.Background;
        _gameSearch.ForeColor = UiTheme.Text;
        _gameSearch.BorderStyle = BorderStyle.FixedSingle;
        _gameSearch.Font = UiTheme.CreateFont(9f);
        _gameSearch.TextChanged += (_, _) => UpdateGamesList();
        _gamesScanButton.Location = new Point(375, 57);
        _gamesScanButton.Size = new Size(170, 36);
        _gamesScanButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _gamesPageCount.Location = new Point(20, 110);
        _gamesPageCount.AutoSize = true;
        _gamesPageScanStatus.Location = new Point(20, 132);
        _gamesPageScanStatus.Size = new Size(600, 18);
        _gamesPageScanStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _gamesList.View = View.Details;
        _gamesList.FullRowSelect = true;
        _gamesList.GridLines = true;
        _gamesList.HideSelection = false;
        _gamesList.BackColor = UiTheme.Background;
        _gamesList.ForeColor = UiTheme.Text;
        _gamesList.Font = UiTheme.CreateFont(8.5f);
        _gamesList.BorderStyle = BorderStyle.FixedSingle;
        _gamesList.Location = new Point(20, 164);
        _gamesList.Size = new Size(600, 420);
        _gamesList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _gamesList.Columns.Add("Jeu", 220);
        _gamesList.Columns.Add("Source", 110);
        _gamesList.Columns.Add("Dossier d’installation", 420);
        _gamesList.DoubleClick += (_, _) => OpenSelectedGameFolder();
        card.Resize += (_, _) =>
        {
            _gamesScanButton.Left = card.Width - _gamesScanButton.Width - 20;
            _gamesList.Width = Math.Max(300, card.Width - 40);
            _gamesList.Height = Math.Max(200, card.Height - 212);
            _gamesList.Columns[2].Width = Math.Max(160, _gamesList.Width - 350);
            _gamesPageScanStatus.Width = Math.Max(250, card.Width - 40);
        };
        card.Controls.Add(intro);
        card.Controls.Add(_gameSearch);
        card.Controls.Add(_gamesScanButton);
        card.Controls.Add(_gamesPageCount);
        card.Controls.Add(_gamesPageScanStatus);
        card.Controls.Add(_gamesList);
        content.Controls.Add(card);
        _gamesPage.Controls.Add(content);
    }

    private void BuildSettingsPage()
    {
        var content = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(30, 16, 30, 26),
            BackColor = UiTheme.Background,
        };
        var refreshCard = new GlassPanel
        {
            Width = 900,
            Height = 205,
            Margin = new Padding(0, 0, 0, 16),
            Padding = new Padding(20),
        };
        var refreshTitle = MakeLabel("FRÉQUENCE DES MESURES", 9f, FontStyle.Bold, UiTheme.Text);
        refreshTitle.Location = new Point(20, 18);
        refreshTitle.AutoSize = true;
        var refreshDescription = MakeLabel(
            "Choisis le rythme d’actualisation CPU/RAM. Le mode Éco ralentit automatiquement la mise à jour à 5 secondes.",
            8f,
            FontStyle.Regular,
            UiTheme.Muted);
        refreshDescription.Location = new Point(21, 48);
        refreshDescription.Size = new Size(780, 36);
        refreshDescription.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _refreshRate.DropDownStyle = ComboBoxStyle.DropDownList;
        _refreshRate.Font = UiTheme.CreateFont(9f);
        _refreshRate.Items.AddRange(["1 seconde", "2 secondes", "5 secondes"]);
        _refreshRate.SelectedIndex = 0;
        _refreshRate.Location = new Point(20, 105);
        _refreshRate.Size = new Size(220, 34);
        _refreshRate.SelectedIndexChanged += (_, _) =>
        {
            UpdateTimerInterval();
            UpdateRefreshInfo();
        };
        _settingsRefreshInfo.Location = new Point(21, 153);
        _settingsRefreshInfo.AutoSize = true;
        refreshCard.Controls.Add(refreshTitle);
        refreshCard.Controls.Add(refreshDescription);
        refreshCard.Controls.Add(_refreshRate);
        refreshCard.Controls.Add(_settingsRefreshInfo);

        var privacyCard = new GlassPanel
        {
            Width = 900,
            Height = 180,
            Margin = Padding.Empty,
            Padding = new Padding(20),
        };
        var privacyTitle = MakeLabel("CONFIDENTIALITÉ ET DONNÉES", 9f, FontStyle.Bold, UiTheme.Text);
        privacyTitle.Location = new Point(20, 18);
        privacyTitle.AutoSize = true;
        var privacyText = MakeLabel(
            "Les mesures système sont lues localement par Windows. La détection des jeux examine les manifestes Steam et Epic locaux. Aucune donnée n’est envoyée à un serveur.",
            8.5f,
            FontStyle.Regular,
            UiTheme.Muted);
        privacyText.Location = new Point(21, 50);
        privacyText.Size = new Size(800, 52);
        privacyText.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        privacyText.AutoEllipsis = true;
        var limitation = MakeLabel(
            "La télémétrie GPU n’est pas disponible tant qu’un fournisseur compatible n’est pas configuré.",
            8f,
            FontStyle.Regular,
            UiTheme.VioletLight);
        limitation.Location = new Point(21, 120);
        limitation.AutoSize = true;
        privacyCard.Controls.Add(privacyTitle);
        privacyCard.Controls.Add(privacyText);
        privacyCard.Controls.Add(limitation);
        content.Controls.Add(refreshCard);
        content.Controls.Add(privacyCard);
        _settingsPage.Controls.Add(content);
        UpdateRefreshInfo();
    }

    private static Control MakeLegend(string text, Color color)
    {
        var legend = new Panel { Size = new Size(70, 22), BackColor = Color.Transparent };
        var dot = new Panel { BackColor = color, Size = new Size(7, 7), Location = new Point(0, 7) };
        var label = MakeLabel(text, 7.5f, FontStyle.Regular, UiTheme.Muted);
        label.Location = new Point(12, 3);
        label.AutoSize = true;
        legend.Controls.Add(dot);
        legend.Controls.Add(label);
        return legend;
    }

    private void ShowPage(string page)
    {
        _dashboardPage.Visible = page == "overview";
        _gamesPage.Visible = page == "games";
        _settingsPage.Visible = page == "settings";
        _dashboardPage.BringToFront();
        if (page == "games")
        {
            _gamesPage.Visible = true;
            _gamesPage.BringToFront();
        }
        else if (page == "settings")
        {
            _settingsPage.Visible = true;
            _settingsPage.BringToFront();
        }

        var (title, description) = page switch
        {
            "games" => ("Jeux installés", "Installations locales confirmées par les lanceurs."),
            "settings" => ("Réglages", "Fréquence des mesures, mode Éco et confidentialité."),
            _ => ("Surveillance du PC", "Processeur, mémoire et jeux installés localement."),
        };
        _pageTitle.Text = title;
        _pageDescription.Text = description;
        foreach (var button in _navigationButtons)
        {
            var selected = string.Equals(button.Tag?.ToString(), page, StringComparison.Ordinal);
            button.BackColor = selected ? UiTheme.Violet : Color.Transparent;
            button.ForeColor = selected ? Color.White : UiTheme.Muted;
        }
    }

    private void RefreshHardware()
    {
        try
        {
            var snapshot = _hardware.Sample();
            _cpuValue.Text = snapshot.CpuUsage is { } cpu ? $"{cpu:0}%" : "…";
            _cpuDetail.Text = snapshot.CpuUsage is { } currentCpu
                ? $"Utilisation actuelle · {currentCpu:0.0}%"
                : "Collecte de la première mesure…";
            _cpuName.Text = snapshot.CpuName;
            _cpuName.AutoEllipsis = true;
            _ramValue.Text = $"{snapshot.RamUsage:0}%";
            _ramDetail.Text = $"{FormatBytes(snapshot.UsedMemory)} utilisés sur {FormatBytes(snapshot.TotalMemory)}";
            _cpuBar.SetValue(snapshot.CpuUsage ?? 0);
            _ramBar.SetValue(snapshot.RamUsage);
            _cpuBar.Invalidate();
            _ramBar.Invalidate();
            _chart.AddSample(snapshot.CpuUsage, snapshot.RamUsage);
        }
        catch (Exception exception)
        {
            _cpuValue.Text = "N/D";
            _ramValue.Text = "N/D";
            _cpuDetail.Text = "Mesures Windows indisponibles.";
            _ramDetail.Text = exception.Message;
        }
    }

    private async Task ScanGamesAsync()
    {
        if (_isScanning)
        {
            return;
        }

        _isScanning = true;
        _dashboardScanButton.Enabled = false;
        _gamesScanButton.Enabled = false;
        _dashboardScanButton.Text = "SCAN EN COURS…";
        _gamesScanButton.Text = "SCAN EN COURS…";
        _scanStatus.Text = "Recherche des manifestes locaux Steam et Epic…";
        _gamesPageScanStatus.Text = _scanStatus.Text;
        _dashboardGameCount.Text = "Scan en cours…";
        _gamesPageCount.Text = "Scan en cours…";
        try
        {
            var found = await Task.Run(GameScanner.ScanLocal);
            _games.Clear();
            _games.AddRange(found);
            _scanStatus.Text = found.Count == 0
                ? "Aucun jeu confirmé. Vérifie que Steam ou Epic est installé et que ses dossiers sont accessibles."
                : $"{found.Count} jeu{(found.Count == 1 ? "" : "x")} trouvé{(found.Count == 1 ? "" : "s")} localement.";
            _gamesPageScanStatus.Text = _scanStatus.Text;
            _dashboardGameCount.Text = $"{found.Count} jeu{(found.Count == 1 ? "" : "x")} détecté{(found.Count == 1 ? "" : "s")}";
            _gamesPageCount.Text = _dashboardGameCount.Text;
            UpdateGamesList();
            UpdateDashboardGamePreview();
        }
        catch (Exception exception)
        {
            _scanStatus.Text = "Le scan n’a pas abouti.";
            _gamesPageScanStatus.Text = _scanStatus.Text;
            _dashboardGameCount.Text = "Scan interrompu";
            _gamesPageCount.Text = "Scan interrompu";
            MessageBox.Show(
                this,
                $"Impossible de terminer le scan local.\n\n{exception.Message}",
                "GameEcoBot",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            _isScanning = false;
            _dashboardScanButton.Enabled = true;
            _gamesScanButton.Enabled = true;
            _dashboardScanButton.Text = "SCANNER LES JEUX";
            _gamesScanButton.Text = "SCANNER LES JEUX";
        }
    }

    private void UpdateGamesList()
    {
        _gamesList.BeginUpdate();
        _gamesList.Items.Clear();
        var filter = _gameSearch.Text.Trim();
        foreach (var game in _games.Where(game =>
                     string.IsNullOrEmpty(filter)
                     || game.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase)))
        {
            var item = new ListViewItem(game.Name);
            item.SubItems.Add(game.Source);
            item.SubItems.Add(game.InstallPath);
            item.Tag = game.InstallPath;
            _gamesList.Items.Add(item);
        }

        _gamesList.EndUpdate();
    }

    private void UpdateDashboardGamePreview()
    {
        _dashboardGames.Controls.Clear();
        if (_games.Count == 0)
        {
            var empty = MakeLabel(
                "Les jeux apparaîtront ici après un scan local.",
                8f,
                FontStyle.Regular,
                UiTheme.Muted);
            empty.AutoSize = true;
            _dashboardGames.Controls.Add(empty);
            return;
        }

        foreach (var game in _games.Take(3))
        {
            var row = new Panel
            {
                Width = Math.Max(100, _dashboardGames.ClientSize.Width),
                Height = 28,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 5),
            };
            var name = MakeLabel(game.Name, 8f, FontStyle.Bold, UiTheme.Text);
            name.Location = new Point(0, 1);
            name.AutoSize = true;
            name.MaximumSize = new Size(220, 22);
            name.AutoEllipsis = true;
            var source = MakeLabel(game.Source.ToUpperInvariant(), 7f, FontStyle.Regular, UiTheme.VioletLight);
            source.Location = new Point(Math.Max(160, _dashboardGames.ClientSize.Width - 80), 3);
            source.AutoSize = true;
            row.Controls.Add(name);
            row.Controls.Add(source);
            _dashboardGames.Controls.Add(row);
        }

        if (_games.Count > 3)
        {
            var more = MakeLabel(
                $"+ {_games.Count - 3} autre{(_games.Count == 4 ? "" : "s")} — ouvrir « Jeux installés » pour tout voir.",
                7.5f,
                FontStyle.Regular,
                UiTheme.Muted);
            more.AutoSize = true;
            _dashboardGames.Controls.Add(more);
        }
    }

    private void OpenSelectedGameFolder()
    {
        if (_gamesList.SelectedItems.Count == 0
            || _gamesList.SelectedItems[0].Tag is not string path
            || !Directory.Exists(path))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"Impossible d’ouvrir le dossier.\n\n{exception.Message}",
                "GameEcoBot",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private async Task ToggleEcoModeAsync()
    {
        try
        {
            string message;
            if (_ecoMode.IsEcoActive)
            {
                var confirmRestore = MessageBox.Show(
                    this,
                    "GameEcoBot va restaurer le plan d’alimentation Windows actif avant l’activation du mode Éco.",
                    "Restaurer le plan précédent",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (confirmRestore != DialogResult.Yes)
                {
                    return;
                }

                message = await Task.Run(_ecoMode.RestorePreviousPlan);
            }
            else
            {
                var confirmEnable = MessageBox.Show(
                    this,
                    "Cette action modifie le plan d’alimentation Windows pour tout l’ordinateur. GameEcoBot mémorisera le plan actuel afin de pouvoir le restaurer.",
                    "Activer le mode Éco",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (confirmEnable != DialogResult.Yes)
                {
                    return;
                }

                message = await Task.Run(_ecoMode.Enable);
            }

            UpdateEcoState();
            MessageBox.Show(this, message, "GameEcoBot", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            UpdateEcoState();
            MessageBox.Show(
                this,
                $"Windows n’a pas pu modifier le plan d’alimentation.\n\n{exception.Message}",
                "Mode Éco indisponible",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void UpdateEcoState()
    {
        var active = _ecoMode.IsEcoActive;
        _ecoHeaderButton.Text = active ? "RESTAURER MON PLAN" : "MODE ÉCO";
        _ecoHeaderButton.BackColor = active ? UiTheme.SurfaceRaised : UiTheme.Violet;
        _ecoHeaderStatus.Text = active ? "PLAN ÉCO GÉRÉ PAR GAMEECOBOT" : "PLAN ACTUEL INCHANGÉ";
        _dashboardEcoButton.Text = active ? "RESTAURER MON PLAN" : "ACTIVER LE MODE ÉCO";
        UpdateTimerInterval();
        UpdateRefreshInfo();
    }

    private void UpdateTimerInterval()
    {
        var selected = _refreshRate.SelectedIndex switch
        {
            1 => 2000,
            2 => 5000,
            _ => 1000,
        };
        _hardwareTimer.Interval = _ecoMode.IsEcoActive ? Math.Max(5000, selected) : selected;
    }

    private void UpdateRefreshInfo()
    {
        if (_settingsRefreshInfo is null)
        {
            return;
        }

        _settingsRefreshInfo.Text = _ecoMode.IsEcoActive
            ? "Mode Éco actif · mesures actualisées toutes les 5 secondes."
            : $"Mesures actualisées toutes les {(_refreshRate.SelectedIndex switch { 1 => "2 secondes", 2 => "5 secondes", _ => "1 seconde" })}.";
    }

    private static string FormatBytes(ulong bytes)
    {
        string[] units = ["o", "Ko", "Mo", "Go", "To"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024d && unit < units.Length - 1)
        {
            value /= 1024d;
            unit++;
        }

        return $"{value:0.#} {units[unit]}";
    }

    private static Label MakeLabel(string text, float size, FontStyle style, Color color)
    {
        return new Label
        {
            Text = text,
            Font = UiTheme.CreateFont(size, style),
            ForeColor = color,
            BackColor = Color.Transparent,
            UseCompatibleTextRendering = true,
            AutoEllipsis = true,
        };
    }

    private static Button MakeButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            Font = UiTheme.CreateFont(8f, FontStyle.Bold),
            ForeColor = primary ? Color.White : UiTheme.Text,
            BackColor = primary ? UiTheme.Violet : UiTheme.SurfaceRaised,
            FlatStyle = FlatStyle.Flat,
            UseCompatibleTextRendering = true,
            Cursor = Cursors.Hand,
            Height = 38,
            AutoEllipsis = true,
        };
        button.FlatAppearance.BorderColor = UiTheme.Border;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(78, 46, 130);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(91, 42, 178);
        return button;
    }

    private sealed class BrandOrb : Control
    {
        public BrandOrb()
        {
            DoubleBuffered = true;
            SetStyle(
                ControlStyles.UserPaint
                    | ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.OptimizedDoubleBuffer,
                true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var bounds = new Rectangle(4, 4, Width - 8, Height - 8);
            using var glow = new SolidBrush(Color.FromArgb(35, UiTheme.Violet));
            using var outer = new SolidBrush(UiTheme.Violet);
            using var inner = new SolidBrush(UiTheme.VioletLight);
            e.Graphics.FillEllipse(glow, new Rectangle(0, 0, Width, Height));
            e.Graphics.FillEllipse(outer, bounds);
            e.Graphics.FillEllipse(inner, new Rectangle(bounds.X + 10, bounds.Y + 10, bounds.Width - 20, bounds.Height - 20));
        }
    }
}
