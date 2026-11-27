using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GameEcoBot;

public enum CrosshairShape
{
    ClassicCross,
    GapCross,
    Dot,
    Circle,
    CircleDot,
    TCross,
    Chevron,
    Custom
}

public sealed class CrosshairPreset
{
    public int Id { get; set; }
    public string Name { get; set; } = "Custom";
    public string Category { get; set; } = "Community";
    public string Author { get; set; } = "CrosshairX";
    public string ColorHex { get; set; } = "#00FF66";
    public float Size { get; set; } = 5f;
    public float Thickness { get; set; } = 2f;
    public float Gap { get; set; } = 4f;
    public float Opacity { get; set; } = 1.0f;
    public bool CenterDot { get; set; } = false;
    public float DotDiameter { get; set; } = 3f;
    public string DotShape { get; set; } = "Square"; // "Square" ou "Circle"
    public string DotColorHex { get; set; } = "#00FF66";
    public bool Outline { get; set; } = true;
    public float OutlineThickness { get; set; } = 1f;
    public string OutlineColorHex { get; set; } = "#000000";
    public bool TopLine { get; set; } = true;
    public bool BottomLine { get; set; } = true;
    public bool LeftLine { get; set; } = true;
    public bool RightLine { get; set; } = true;
    public int OffsetX { get; set; } = 0;
    public int OffsetY { get; set; } = 0;
}

public sealed class CrosshairSettings
{
    public CrosshairShape Shape { get; set; } = CrosshairShape.ClassicCross;
    public string ColorHex { get; set; } = "#00FF66"; // Vert Fluo eSport standard
    public float Size { get; set; } = 6f;
    public float Thickness { get; set; } = 2f;
    public float Gap { get; set; } = 4f;
    public bool Outline { get; set; } = true;
    public float OutlineThickness { get; set; } = 1f;
    public string OutlineColorHex { get; set; } = "#000000";

    public bool CenterDot { get; set; } = false;
    public float DotDiameter { get; set; } = 3f;
    public string DotShape { get; set; } = "Square"; // "Square" ou "Circle"
    public string DotColorHex { get; set; } = "#00FF66";

    public bool TopLine { get; set; } = true;
    public bool BottomLine { get; set; } = true;
    public bool LeftLine { get; set; } = true;
    public bool RightLine { get; set; } = true;

    public float Opacity { get; set; } = 1.0f;
    public int OffsetX { get; set; } = 0;
    public int OffsetY { get; set; } = 0;

    public bool CrispLines { get; set; } = true;
    public bool AutoShowOnGame { get; set; } = false;
    public string ActivePresetName { get; set; } = "Par défaut";

    [System.Text.Json.Serialization.JsonIgnore]
    public Color Color
    {
        get
        {
            try { return ColorTranslator.FromHtml(ColorHex); }
            catch { return Color.FromArgb(0, 255, 102); }
        }
        set => ColorHex = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public Color DotColor
    {
        get
        {
            try { return ColorTranslator.FromHtml(DotColorHex); }
            catch { return Color; }
        }
        set => DotColorHex = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public Color OutlineColor
    {
        get
        {
            try { return ColorTranslator.FromHtml(OutlineColorHex); }
            catch { return Color.Black; }
        }
        set => OutlineColorHex = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
    }

    public void ApplyPreset(CrosshairPreset p)
    {
        this.ActivePresetName = p.Name;
        this.ColorHex = p.ColorHex;
        this.Size = p.Size;
        this.Thickness = p.Thickness;
        this.Gap = p.Gap;
        this.Opacity = p.Opacity;
        this.CenterDot = p.CenterDot;
        this.DotDiameter = p.DotDiameter;
        this.DotShape = p.DotShape;
        this.DotColorHex = p.DotColorHex;
        this.Outline = p.Outline;
        this.OutlineThickness = p.OutlineThickness;
        this.OutlineColorHex = p.OutlineColorHex;
        this.TopLine = p.TopLine;
        this.BottomLine = p.BottomLine;
        this.LeftLine = p.LeftLine;
        this.RightLine = p.RightLine;
        this.OffsetX = p.OffsetX;
        this.OffsetY = p.OffsetY;
        this.Shape = CrosshairShape.Custom;
    }
}

public sealed class CrosshairConfigModel
{
    public bool IsActive { get; set; } = false;
    public CrosshairSettings Settings { get; set; } = new();
}

/// <summary>
/// Service de gestion du réticule de visée (Crosshair) compétitif par-dessus tous les jeux.
/// Intègre la bibliothèque complète de réticules de CrosshairX (Pros Valorant, CS2, Fortnite, Rust).
/// </summary>
public sealed class CrosshairService
{
    private static readonly Lazy<CrosshairService> _lazy = new(() => new CrosshairService());
    public static CrosshairService Instance => _lazy.Value;

    private CrosshairOverlayForm? _overlayForm;
    private readonly string _configFilePath;
    private readonly string _presetsFilePath;

    public CrosshairSettings Settings { get; private set; } = new();
    public bool IsActive { get; private set; } = false;
    public List<CrosshairPreset> Presets { get; private set; } = new();

    public event Action? CrosshairChanged;

    private CrosshairService()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GameEcoBot");
        Directory.CreateDirectory(dir);
        _configFilePath = Path.Combine(dir, "crosshair_config.json");
        _presetsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crosshair_presets.json");

        LoadPresets();
        LoadConfig();

        SystemEvents.DisplaySettingsChanged += (_, _) => Recenter();
    }

    public void LoadPresets()
    {
        try
        {
            // 1. Chercher dans BaseDirectory
            string json = "";
            if (File.Exists(_presetsFilePath))
            {
                json = File.ReadAllText(_presetsFilePath, Encoding.UTF8);
            }
            else
            {
                // 2. Chercher dans LocalAppData
                string fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameEcoBot", "crosshair_presets.json");
                if (File.Exists(fallback))
                {
                    json = File.ReadAllText(fallback, Encoding.UTF8);
                }
            }

            if (!string.IsNullOrWhiteSpace(json))
            {
                var list = JsonSerializer.Deserialize<List<CrosshairPreset>>(json);
                if (list != null && list.Count > 0)
                {
                    Presets = list;
                    return;
                }
            }
        }
        catch { }

        // Fallback essentiels si non trouvé
        if (Presets.Count == 0)
        {
            Presets = GetDefaultEsportPresets();
        }
    }

    private static List<CrosshairPreset> GetDefaultEsportPresets()
    {
        return new List<CrosshairPreset>
        {
            new CrosshairPreset { Id = 1, Name = "TenZ", Category = "Valorant Pros", Author = "Sentinels", ColorHex = "#00FFFF", Size = 4, Thickness = 2, Gap = 2, Opacity = 1f, Outline = true, OutlineThickness = 1, CenterDot = false },
            new CrosshairPreset { Id = 2, Name = "aspas", Category = "Valorant Pros", Author = "Leviatán", ColorHex = "#00FFFF", Size = 2, Thickness = 3, Gap = 0, Opacity = 1f, Outline = false, CenterDot = false },
            new CrosshairPreset { Id = 3, Name = "Alfajer", Category = "Valorant Pros", Author = "Fnatic", ColorHex = "#FFFFFF", Size = 5, Thickness = 2, Gap = 8, Opacity = 1f, Outline = false, CenterDot = false },
            new CrosshairPreset { Id = 4, Name = "Boaster", Category = "Valorant Pros", Author = "Fnatic", ColorHex = "#00FFFF", Size = 1, Thickness = 2, Gap = 0, Opacity = 1f, Outline = false, CenterDot = false },
            new CrosshairPreset { Id = 5, Name = "Demon1", Category = "Valorant Pros", Author = "NRG", ColorHex = "#FFFFFF", Size = 2, Thickness = 1, Gap = 0, Opacity = 1f, Outline = true, OutlineThickness = 1, CenterDot = false },
            new CrosshairPreset { Id = 6, Name = "s1mple", Category = "CS2 Pros", Author = "NAVI / Falcons", ColorHex = "#00FF66", Size = 2, Thickness = 1, Gap = -1, Opacity = 1f, Outline = false, CenterDot = true, DotDiameter = 2 },
            new CrosshairPreset { Id = 7, Name = "m0NESY", Category = "CS2 Pros", Author = "G2 Esports", ColorHex = "#00FFFF", Size = 2, Thickness = 1, Gap = -3, Opacity = 1f, Outline = false, CenterDot = false },
            new CrosshairPreset { Id = 8, Name = "NiKo", Category = "CS2 Pros", Author = "G2 Esports", ColorHex = "#00FFFF", Size = 2, Thickness = 1, Gap = -3, Opacity = 1f, Outline = false, CenterDot = false },
            new CrosshairPreset { Id = 9, Name = "donk", Category = "CS2 Pros", Author = "Team Spirit", ColorHex = "#00FF66", Size = 1, Thickness = 1, Gap = -4, Opacity = 1f, Outline = false, CenterDot = false },
            new CrosshairPreset { Id = 10, Name = "ZywOo", Category = "CS2 Pros", Author = "Team Vitality", ColorHex = "#00FFFF", Size = 2, Thickness = 1, Gap = -3, Opacity = 1f, Outline = false, CenterDot = false }
        };
    }

    public void LoadConfig()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                string json = File.ReadAllText(_configFilePath, Encoding.UTF8);
                var cfg = JsonSerializer.Deserialize<CrosshairConfigModel>(json);
                if (cfg != null)
                {
                    Settings = cfg.Settings ?? new CrosshairSettings();
                    if (cfg.IsActive)
                    {
                        Show();
                    }
                }
            }
        }
        catch { }
    }

    public void SaveConfig()
    {
        try
        {
            var cfg = new CrosshairConfigModel
            {
                IsActive = this.IsActive,
                Settings = this.Settings
            };
            string json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configFilePath, json, Encoding.UTF8);
        }
        catch { }
    }

    public void Show()
    {
        IsActive = true;
        if (_overlayForm == null || _overlayForm.IsDisposed)
        {
            _overlayForm = new CrosshairOverlayForm();
        }
        _overlayForm.ShowOverlay();
        SaveConfig();
        CrosshairChanged?.Invoke();
    }

    public void Hide()
    {
        IsActive = false;
        if (_overlayForm != null && !_overlayForm.IsDisposed)
        {
            _overlayForm.HideOverlay();
        }
        SaveConfig();
        CrosshairChanged?.Invoke();
    }

    public void Toggle()
    {
        if (IsActive) Hide();
        else Show();
    }

    public void UpdateSettings(Action<CrosshairSettings> update)
    {
        update(Settings);
        _overlayForm?.Invalidate();
        SaveConfig();
        CrosshairChanged?.Invoke();
    }

    public void Recenter()
    {
        if (_overlayForm != null && !_overlayForm.IsDisposed)
        {
            _overlayForm.UpdatePosition();
        }
    }

    public List<string> GetCategories()
    {
        var list = new List<string> { "Tous" };
        var dist = Presets.Select(p => p.Category).Where(c => !string.IsNullOrEmpty(c)).Distinct().OrderBy(c => c);
        list.AddRange(dist);
        return list;
    }

    public List<CrosshairPreset> FilterPresets(string category, string searchQuery)
    {
        IEnumerable<CrosshairPreset> q = Presets;
        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("Tous", StringComparison.OrdinalIgnoreCase))
        {
            q = q.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            string s = searchQuery.Trim().ToLowerInvariant();
            q = q.Where(p => p.Name.ToLowerInvariant().Contains(s) || p.Author.ToLowerInvariant().Contains(s));
        }
        return q.ToList();
    }

    /// <summary>
    /// Importe un profil ou code de viseur Valorant officiel (ex: 0;P;c;5;o;1;0t;1;0l;3;0o;2;0a;1...)
    /// </summary>
    public bool ImportValorantCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        try
        {
            string clean = code.Trim();
            string[] parts = clean.Split(';');
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < parts.Length - 1; i += 2)
            {
                dict[parts[i]] = parts[i + 1];
            }

            UpdateSettings(s =>
            {
                // Couleur Valorant (c)
                if (dict.TryGetValue("c", out string? cVal))
                {
                    s.ColorHex = cVal switch
                    {
                        "0" => "#FFFFFF", // Blanc
                        "1" => "#71FF74", // Vert Fluo
                        "2" => "#90EE90", // Vert Clair
                        "3" => "#ADFF2F", // Jaune-Vert
                        "4" => "#FFFF00", // Jaune
                        "5" => "#00FFFF", // Cyan
                        "6" => "#FF1493", // Rose
                        "7" => "#FF0000", // Rouge
                        _ => s.ColorHex
                    };
                }
                // Outline (o) et épaisseur (t)
                if (dict.TryGetValue("o", out string? oVal)) s.Outline = oVal == "1";
                if (dict.TryGetValue("t", out string? tVal) && float.TryParse(tVal, out float outThick)) s.OutlineThickness = Math.Max(1f, outThick);

                // Point central (d) et taille (z)
                if (dict.TryGetValue("d", out string? dVal)) s.CenterDot = dVal == "1";
                if (dict.TryGetValue("z", out string? zVal) && float.TryParse(zVal, out float dSize)) s.DotDiameter = Math.Max(1f, dSize);

                // Lignes intérieures : Épaisseur (0t), Longueur (0l), Décalage (0o/0g), Opacité (0a)
                if (dict.TryGetValue("0t", out string? itVal) && float.TryParse(itVal, out float lThick)) s.Thickness = Math.Max(1f, lThick);
                if (dict.TryGetValue("0l", out string? ilVal) && float.TryParse(ilVal, out float lLen)) s.Size = Math.Max(1f, lLen);
                if (dict.TryGetValue("0o", out string? ioVal) && float.TryParse(ioVal, out float lGap)) s.Gap = Math.Max(0f, lGap);
                if (dict.TryGetValue("0a", out string? iaVal) && float.TryParse(iaVal, out float lOp)) s.Opacity = Math.Clamp(lOp, 0.1f, 1f);

                // Visibilité lignes (0b)
                if (dict.TryGetValue("0b", out string? ibVal))
                {
                    bool linesVis = ibVal != "0";
                    s.TopLine = s.BottomLine = s.LeftLine = s.RightLine = linesVis;
                }

                s.Shape = CrosshairShape.Custom;
                s.ActivePresetName = "Code Valorant Importé";
            });

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Exporte la configuration actuelle au format de partage Valorant.
    /// </summary>
    public string ExportValorantCode()
    {
        var sb = new StringBuilder("0;P;");

        // Code couleur
        string c = Settings.ColorHex.ToUpperInvariant();
        string colIdx = c switch
        {
            "#FFFFFF" => "0",
            "#71FF74" or "#00FF00" or "#00FF66" => "1",
            "#FFFF00" or "#FFE600" => "4",
            "#00FFFF" => "5",
            "#FF0000" or "#FF3333" => "7",
            _ => "5"
        };
        sb.Append($"c;{colIdx};");

        if (Settings.Outline)
            sb.Append($"o;1;t;{(int)Math.Max(1f, Settings.OutlineThickness)};");
        else
            sb.Append("o;0;");

        if (Settings.CenterDot)
            sb.Append($"d;1;z;{(int)Math.Max(1f, Settings.DotDiameter)};a;1;");
        else
            sb.Append("d;0;");

        bool hasLines = Settings.TopLine || Settings.BottomLine || Settings.LeftLine || Settings.RightLine;
        if (hasLines)
        {
            sb.Append($"0t;{(int)Math.Max(1f, Settings.Thickness)};");
            sb.Append($"0l;{(int)Math.Max(1f, Settings.Size)};");
            sb.Append($"0o;{(int)Math.Max(0f, Settings.Gap)};");
            sb.Append($"0a;{Settings.Opacity:0.0};");
            sb.Append("0f;0;1b;0");
        }
        else
        {
            sb.Append("0b;0;1b;0");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Algorithme de rendu ultra-net (Anti-Magenta Blending).
    /// Dessine les pixels avec précision exacte pour éviter toute frange rose sur l'overlay transparent.
    /// </summary>
    public static void DrawCrosshair(Graphics g, float cx, float cy, CrosshairSettings s)
    {
        // En mode CrispLines, on désactive le lissage GDI+ pour les lignes droites afin d'éliminer
        // les pixels de bordure semi-transparents qui créent des bavures magenta avec TransparencyKey.
        g.SmoothingMode = s.CrispLines ? SmoothingMode.None : SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        Color c = s.Color;
        int a = (int)(Math.Clamp(s.Opacity, 0.1f, 1f) * 255);
        Color mainColor = Color.FromArgb(a, c.R, c.G, c.B);
        Color outColor = s.Outline ? Color.FromArgb(a, s.OutlineColor.R, s.OutlineColor.G, s.OutlineColor.B) : Color.Transparent;

        float size = Math.Max(1f, s.Size);
        float thick = Math.Max(1f, s.Thickness);
        float gap = Math.Max(0f, s.Gap);
        float outThick = Math.Max(1f, s.OutlineThickness);

        // Dessiner les branches de viseur personnalisées (CrosshairX Engine)
        if (s.Shape == CrosshairShape.ClassicCross || s.Shape == CrosshairShape.GapCross || s.Shape == CrosshairShape.TCross || s.Shape == CrosshairShape.Custom)
        {
            float halfThick = (float)Math.Floor(thick / 2f);

            void DrawRectArm(float rx, float ry, float rw, float rh)
            {
                if (s.Outline)
                {
                    using var outBrush = new SolidBrush(outColor);
                    g.FillRectangle(outBrush, rx - outThick, ry - outThick, rw + 2 * outThick, rh + 2 * outThick);
                }
                using var brush = new SolidBrush(mainColor);
                g.FillRectangle(brush, rx, ry, rw, rh);
            }

            // Bras Gauche
            if (s.LeftLine)
            {
                DrawRectArm(cx - gap - size, cy - halfThick, size, thick);
            }
            // Bras Droit
            if (s.RightLine)
            {
                DrawRectArm(cx + gap, cy - halfThick, size, thick);
            }
            // Bras Bas
            if (s.BottomLine)
            {
                DrawRectArm(cx - halfThick, cy + gap, thick, size);
            }
            // Bras Haut (sauf si T-Cross ou décoché)
            if (s.TopLine && s.Shape != CrosshairShape.TCross)
            {
                DrawRectArm(cx - halfThick, cy - gap - size, thick, size);
            }
        }
        else if (s.Shape == CrosshairShape.Circle || s.Shape == CrosshairShape.CircleDot)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float radius = Math.Max(4f, size);
            if (s.Outline)
            {
                using var outPen = new Pen(outColor, thick + 2f * outThick);
                g.DrawEllipse(outPen, cx - radius, cy - radius, radius * 2, radius * 2);
            }
            using (var pen = new Pen(mainColor, thick))
            {
                g.DrawEllipse(pen, cx - radius, cy - radius, radius * 2, radius * 2);
            }
        }
        else if (s.Shape == CrosshairShape.Chevron)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float chW = size * 0.9f;
            float chH = size * 0.9f;
            PointF pTop = new PointF(cx, cy - chH * 0.5f);
            PointF pLeft = new PointF(cx - chW, cy + chH * 0.5f);
            PointF pRight = new PointF(cx + chW, cy + chH * 0.5f);

            if (s.Outline)
            {
                using var outPen = new Pen(outColor, thick + 2f * outThick)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };
                g.DrawLine(outPen, pLeft, pTop);
                g.DrawLine(outPen, pTop, pRight);
            }
            using (var pen = new Pen(mainColor, thick)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            })
            {
                g.DrawLine(pen, pLeft, pTop);
                g.DrawLine(pen, pTop, pRight);
            }
        }

        // Dessiner le Point Central (Dot) indépendant
        bool drawDot = s.CenterDot || s.Shape == CrosshairShape.Dot || s.Shape == CrosshairShape.CircleDot;
        if (drawDot)
        {
            Color dotC = s.DotColor;
            Color dotFinal = Color.FromArgb(a, dotC.R, dotC.G, dotC.B);
            float dotR = Math.Max(1f, s.DotDiameter / 2f);

            if (s.DotShape.Equals("Square", StringComparison.OrdinalIgnoreCase))
            {
                g.SmoothingMode = SmoothingMode.None;
                float dSize = Math.Max(2f, dotR * 2f);
                float dx = cx - (float)Math.Floor(dSize / 2f);
                float dy = cy - (float)Math.Floor(dSize / 2f);

                if (s.Outline)
                {
                    using var outBrush = new SolidBrush(outColor);
                    g.FillRectangle(outBrush, dx - outThick, dy - outThick, dSize + 2 * outThick, dSize + 2 * outThick);
                }
                using var dotBrush = new SolidBrush(dotFinal);
                g.FillRectangle(dotBrush, dx, dy, dSize, dSize);
            }
            else // Circle Dot
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (s.Outline)
                {
                    using var outBrush = new SolidBrush(outColor);
                    g.FillEllipse(outBrush, cx - dotR - outThick, cy - dotR - outThick, (dotR + outThick) * 2, (dotR + outThick) * 2);
                }
                using var dotBrush = new SolidBrush(dotFinal);
                g.FillEllipse(dotBrush, cx - dotR, cy - dotR, dotR * 2, dotR * 2);
            }
        }
    }
}

/// <summary>
/// Boîte de prévisualisation en direct du viseur avec grille de ciblage tactique.
/// </summary>
public sealed class CrosshairPreviewControl : Control
{
    public CrosshairPreviewControl()
    {
        this.DoubleBuffered = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        this.Size = new Size(130, 130);
        this.BackColor = Color.FromArgb(12, 12, 12);
        this.Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Fond sombre
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var bgBrush = new SolidBrush(Color.FromArgb(12, 12, 12)))
        {
            g.FillRectangle(bgBrush, rect);
        }

        // Grille de ciblage subtile eSport
        using (var gridPen = new Pen(Color.FromArgb(28, 28, 28), 1f))
        {
            float cx = Width / 2f;
            float cy = Height / 2f;
            g.DrawLine(gridPen, cx, 0, cx, Height);
            g.DrawLine(gridPen, 0, cy, Width, cy);
            g.DrawEllipse(gridPen, cx - 22, cy - 22, 44, 44);
            g.DrawEllipse(gridPen, cx - 44, cy - 44, 88, 88);
        }

        // Dessiner le crosshair
        CrosshairService.DrawCrosshair(g, Width / 2f, Height / 2f, CrosshairService.Instance.Settings);

        // Bordure moderne
        using (var borderPen = new Pen(Color.FromArgb(45, 45, 45), 1f))
        {
            g.DrawRectangle(borderPen, rect);
        }
    }
}
