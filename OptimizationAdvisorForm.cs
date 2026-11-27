using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace GameEcoBot;

/// <summary>
/// Checklist guidée inspirée des deux vidéos d'optimisation.
/// Les modifications BIOS, pilotes, sécurité et overclocking ne sont jamais appliquées
/// automatiquement : les vidéos divergent sur plusieurs réglages et le matériel varie.
/// </summary>
public sealed class OptimizationAdvisorForm : Form
{
    private static readonly Color BackgroundColor = Color.FromArgb(10, 10, 10);
    private static readonly Color CardColor = Color.FromArgb(18, 18, 18);
    private static readonly Color BorderColor = Color.FromArgb(40, 40, 40);
    private static readonly Color AccentColor = Color.White;
    private static readonly Color MutedColor = Color.FromArgb(160, 160, 160);

    private readonly OptimizationProgress progress;
    private readonly string progressPath;
    private NumericUpDown? beforeScore;
    private NumericUpDown? afterScore;
    private Label? scoreComparison;

    public OptimizationAdvisorForm()
    {
        progressPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GameEcoBot",
            "optimization-progress.json");
        progress = LoadProgress();

        Text = "GameEcoBot — Assistant d'optimisation";
        StartPosition = FormStartPosition.CenterParent;
        BackColor = BackgroundColor;
        ForeColor = Color.White;
        Font = AppFonts.GetBodyFont(9f);
        ClientSize = new Size(1040, 760);
        MinimumSize = new Size(860, 650);
        ShowInTaskbar = false;

        BuildInterface();
    }

    private void BuildInterface()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 88,
            BackColor = Color.FromArgb(14, 14, 14),
            Padding = new Padding(20, 12, 20, 8)
        };

        header.Controls.Add(new Label
        {
            Text = "Assistant d'optimisation",
            Location = new Point(20, 12),
            AutoSize = true,
            Font = AppFonts.GetTitleFont(17f),
            ForeColor = AccentColor,
            BackColor = Color.Transparent
        });
        header.Controls.Add(new Label
        {
            Text = "Mesurez, vérifiez et choisissez les réglages un par un — rien n'est appliqué en silence.",
            Location = new Point(22, 48),
            Size = new Size(640, 22),
            AutoEllipsis = true,
            ForeColor = MutedColor,
            BackColor = Color.Transparent
        });

        var sourceOne = CreateSourceLink(
            "Vidéo 1",
            "https://youtu.be/U-gkZ2GJXlE?si=xC7_mYryu2GiEu0H",
            new Point(720, 24));
        var sourceTwo = CreateSourceLink(
            "Vidéo 2",
            "https://youtu.be/0EJektuVzC0?si=dBMU1Q5oZMZf6nC7",
            new Point(820, 24));
        header.Controls.Add(sourceOne);
        header.Controls.Add(sourceTwo);

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            DrawMode = TabDrawMode.OwnerDrawFixed,
            ItemSize = new Size(140, 34),
            SizeMode = TabSizeMode.Fixed,
            Padding = new Point(12, 5),
            BackColor = BackgroundColor,
            Font = AppFonts.GetBoldFont(9f)
        };
        tabs.DrawItem += (_, e) =>
        {
            var page = tabs.TabPages[e.Index];
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var bounds = e.Bounds;
            using var fill = new SolidBrush(selected
                ? Color.FromArgb(40, 40, 40)
                : Color.FromArgb(18, 18, 18));
            e.Graphics.FillRectangle(fill, bounds);
            TextRenderer.DrawText(
                e.Graphics,
                page.Text,
                tabs.Font,
                bounds,
                selected ? Color.White : MutedColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };

        var overview = CreatePage(tabs, "Bilan");
        AddCard(overview, "benchmark-intro", "1. Mesurer avant de modifier",
            "Lancez un benchmark dans les mêmes conditions avant et après les changements. Le score seul ne mesure pas la latence ni la stabilité.",
            "Ouvrir 3DMark", () => OpenTarget("https://store.steampowered.com/app/223850/3DMark/"));
        AddCard(overview, "health-diagnostic", "2. Vérifier la santé du PC",
            "Contrôlez températures, throttling, erreurs mémoire, saturation CPU/GPU/disque et pilotes. Corrigez un problème matériel avant de chercher des FPS.",
            "Ouvrir le Gestionnaire des tâches", OpenTaskManager);
        AddCard(overview, "idle-usage", "3. Regarder l'utilisation au repos",
            "Dans Gestionnaire des tâches > Performances, observez CPU, mémoire, GPU et disque après quelques minutes sans activité. Cherchez le composant réellement saturé.",
            "Ouvrir le Gestionnaire des tâches", OpenTaskManager);
        AddCard(overview, "restore-point-before", "4. Créer un point de restauration",
            "Faites-le avant une série de changements Windows. La protection du système doit être activée et disposer d'espace disque.",
            "Ouvrir la Protection du système", OpenRestorePoint);
        AddCard(overview, "fix-real-bottleneck", "5. Traiter le vrai goulot d'étranglement",
            "Des températures trop hautes, un disque saturé ou une RAM pleine ne se règlent pas avec un script d'optimisation. Vérifiez le refroidissement, l'état du stockage et les besoins de mise à niveau.");

        var windows = CreatePage(tabs, "Windows");
        AddCard(windows, "startup-apps", "Démarrage : désactiver seulement les applis inutiles",
            "Désactivez les launchers et utilitaires que vous n'utilisez pas au démarrage. Gardez les outils nécessaires et les composants de sécurité.",
            "Ouvrir les applis de démarrage", () => OpenTarget("ms-settings:startupapps"));
        AddCard(windows, "remove-apps", "Désinstaller les applications connues et inutilisées",
            "Vérifiez le nom d'une application avant de la supprimer. N'utilisez pas « Tout sélectionner » et ne retirez pas les composants Windows inconnus.",
            "Ouvrir les applications installées", () => OpenTarget("ms-settings:appsfeatures"));
        AddCard(windows, "game-mode", "Activer le Mode Jeu",
            "Vérifiez le Mode Jeu Windows. Il peut limiter certaines interruptions en arrière-plan ; mesurez le résultat sur vos jeux.",
            "Ouvrir le Mode Jeu", () => OpenTarget("ms-settings:gaming-gamemode"));
        AddCard(windows, "game-capture", "Vérifier les captures et enregistrements en arrière-plan",
            "Si vous ne les utilisez pas, désactivez les captures automatiques pour éviter une activité et des écritures disque dont vous n'avez pas besoin.",
            "Ouvrir les paramètres de capture", () => OpenTarget("ms-settings:gaming-gamedvr"));
        AddCard(windows, "storage-cleanup", "Nettoyer les fichiers temporaires avec vérification",
            "Utilisez les paramètres de stockage et examinez les éléments sélectionnés avant suppression. Évitez un nettoyage pendant une partie si le disque est déjà sollicité.",
            "Ouvrir le Stockage", () => OpenTarget("ms-settings:storagepolicies"));
        AddCard(windows, "privacy-ads", "Choisir vos réglages de confidentialité et de publicité",
            "Les deux vidéos proposent de réduire suggestions, publicités personnalisées et données de diagnostic. Ce sont des choix de confidentialité, pas une promesse de FPS.",
            "Ouvrir les paramètres de confidentialité", () => OpenTarget("ms-settings:privacy-general"));
        AddCard(windows, "privacy-location", "Décider si les applications peuvent utiliser la localisation",
            "Désactivez-la uniquement si vous n'en avez pas l'usage. Cela n'améliore pas nécessairement les performances en jeu.",
            "Ouvrir la Localisation", () => OpenTarget("ms-settings:privacy-location"));
        AddCard(windows, "power-mode", "Choisir le mode d'alimentation adapté",
            "Sur portable, un mode plus performant peut réduire l'autonomie et augmenter la chaleur. Les vidéos déconseillent le plan « Performances ultimes » comme optimisation universelle.",
            "Ouvrir Alimentation", () => OpenTarget("ms-settings:powersleep"));
        AddCard(windows, "sysmain-index", "SysMain et indexation : les vidéos se contredisent",
            "Une vidéo recommande de les laisser actifs pour un usage quotidien ; l'autre conseille de les désactiver. GameEcoBot ne les modifie pas : ne testez qu'un réglage à la fois et revenez en arrière si la recherche ou le disque se dégrade.");
        AddCard(windows, "visual-effects", "Effets visuels : optionnels selon l'âge du PC",
            "La vidéo récente indique que leur désactivation apporte peu sur un PC moderne ; l'autre suggère de la tester sur du matériel ancien. Gardez l'interface que vous préférez et comparez.");
        AddCard(windows, "defender-vbs", "Ne pas désactiver la sécurité pour gagner quelques FPS",
            "Gardez Microsoft Defender actif. La désactivation de VBS/Isolation du noyau peut réduire la sécurité : ne la faites pas automatiquement sur la seule base d'un tutoriel.");
        AddCard(windows, "power-plan-warning", "Éviter les scripts de « debloat » et les réglages en lot",
            "Les vidéos avertissent que des scripts globaux peuvent retirer Defender, des services Windows ou des fonctions utiles. Préférez un changement documenté, individuel et réversible.");

        var gaming = CreatePage(tabs, "Jeux & matériel");
        AddCard(gaming, "drivers", "Mettre à jour les pilotes depuis la bonne source",
            "Pour la carte mère, utilisez la page de son fabricant ; pour le GPU, le logiciel ou site officiel NVIDIA, AMD ou Intel. Windows Update apporte aussi les mises à jour de sécurité.",
            "Ouvrir Windows Update", () => OpenTarget("ms-settings:windowsupdate"));
        AddCard(gaming, "bios", "BIOS : mise à jour seulement si elle répond à un vrai besoin",
            "Les vidéos signalent le risque d'une coupure pendant le flashage. Suivez uniquement la procédure du fabricant pour corriger un problème précis ; ce n'est pas une étape de routine.");
        AddCard(gaming, "xmp-expo", "Activer XMP/EXPO uniquement si le kit est compatible",
            "Le profil se règle dans le BIOS/UEFI et peut améliorer les performances mémoire. Vérifiez la compatibilité carte mère/RAM, puis testez la stabilité ; désactivez le profil si des plantages apparaissent.");
        AddCard(gaming, "hags-graphics", "Tester les options graphiques de Windows",
            "Mode Jeu, planification GPU accélérée et optimisations fenêtrées peuvent varier selon le GPU et les jeux. Testez une option à la fois et gardez le réglage qui fonctionne chez vous.",
            "Ouvrir les paramètres graphiques", () => OpenTarget("ms-settings:display-graphics"));
        AddCard(gaming, "refresh-rate", "Vérifier la résolution et la fréquence réelle de l'écran",
            "Choisissez la résolution native et la fréquence maximale prise en charge par l'écran. Un câble ou un port inadapté peut limiter les Hz disponibles.",
            "Ouvrir l'affichage avancé", () => OpenTarget("ms-settings:display-advanced"));
        AddCard(gaming, "vrr-hdr", "VRR et HDR : seulement si l'écran et le jeu les prennent en charge",
            "Activez ces options au cas par cas. Si un jeu s'affiche mal ou saccade, comparez avec le réglage désactivé.",
            "Ouvrir l'affichage", () => OpenTarget("ms-settings:display"));
        AddCard(gaming, "gpu-panel", "Réglages NVIDIA/AMD : comparer jeu par jeu",
            "Une vidéo recommande faible latence Ultra et certains réglages de performance ; l'autre dit que la plupart des réglages 3D globaux apportent peu aujourd'hui. Évitez un profil global aveugle.",
            "Ouvrir les paramètres GPU", OpenGpuControlPanel);
        AddCard(gaming, "gpu-overclock", "Overclocking GPU : avancé, jamais appliqué par GameEcoBot",
            "Les vidéos divergent : l'une préfère un scan automatique sans toucher à la tension, l'autre propose des changements manuels de fréquence/tension. N'augmentez pas la tension à l'aveugle ; testez la stabilité et gardez un retour aux réglages d'origine.");
        AddCard(gaming, "display-driver-reset", "En cas d'affichage bloqué",
            "Windows peut redémarrer le pilote graphique avec Win + Ctrl + Maj + B. L'écran peut clignoter brièvement ; ce raccourci ne remplace pas un diagnostic matériel.");
        AddCard(gaming, "steam-integrity", "Si un jeu Steam plante, vérifier ses fichiers",
            "Dans Steam : Bibliothèque > clic droit sur le jeu > Propriétés > Fichiers installés > Vérifier l'intégrité des fichiers.");
        AddCard(gaming, "linux-lite", "Système « Lite » ou Linux : changement de système, pas un bouton d'optimisation",
            "Les vidéos citent Linux/Windows allégé comme piste pour certains vieux PC. Vérifiez d'abord la compatibilité de vos jeux, anti-cheats, logiciels et périphériques.");

        var benchmarks = CreatePage(tabs, "Avant / après");
        AddBenchmarkCard(benchmarks);
        AddCard(benchmarks, "benchmark-method", "Comparer proprement",
            "Utilisez le même benchmark, la même résolution et les mêmes réglages. Fermez les tâches inutiles, notez les changements effectués, puis vérifiez aussi la stabilité et la latence.");
        AddCard(benchmarks, "restore-point-after", "Créer un second point après une configuration stable",
            "Une fois les réglages vérifiés, un point de restauration distinct aide à retrouver cet état. Il ne remplace pas une sauvegarde personnelle des fichiers.",
            "Ouvrir la Protection du système", OpenRestorePoint);

        Controls.Add(tabs);
        Controls.Add(header);
        FormClosing += (_, _) => SaveProgress();
    }

    private static LinkLabel CreateSourceLink(string text, string url, Point location)
    {
        var link = new LinkLabel
        {
            Text = text,
            Location = location,
            Size = new Size(85, 24),
            LinkColor = AccentColor,
            ActiveLinkColor = Color.White,
            VisitedLinkColor = AccentColor,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        link.LinkClicked += (_, _) => OpenTarget(url);
        return link;
    }

    private static FlowLayoutPanel CreatePage(TabControl tabs, string title)
    {
        var page = new TabPage(title)
        {
            BackColor = BackgroundColor,
            UseVisualStyleBackColor = false
        };
        var content = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(14),
            BackColor = BackgroundColor
        };
        content.ClientSizeChanged += (_, _) =>
        {
            int width = Math.Max(400, content.ClientSize.Width - content.Padding.Horizontal - 8);
            foreach (Control child in content.Controls)
                child.Width = width;
        };
        page.Controls.Add(content);
        tabs.TabPages.Add(page);
        return content;
    }

    private void AddCard(
        FlowLayoutPanel page,
        string id,
        string title,
        string description,
        string? actionText = null,
        Action? action = null)
    {
        var card = new Panel
        {
            Width = Math.Max(400, page.ClientSize.Width - page.Padding.Horizontal - 8),
            Height = 126,
            BackColor = CardColor,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(14)
        };

        var heading = new Label
        {
            Text = title,
            Location = new Point(15, 10),
            Size = new Size(card.Width - 30, 22),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            AutoEllipsis = true,
            Font = AppFonts.GetBoldFont(9.5f),
            ForeColor = AccentColor,
            BackColor = Color.Transparent
        };
        var detail = new Label
        {
            Text = description,
            Location = new Point(16, 35),
            Size = new Size(card.Width - 32, 48),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            AutoEllipsis = true,
            Font = AppFonts.GetBodyFont(8.7f),
            ForeColor = MutedColor,
            BackColor = Color.Transparent
        };
        var verified = new CheckBox
        {
            Text = "Vérifié / fait",
            Location = new Point(15, 92),
            AutoSize = true,
            ForeColor = Color.FromArgb(220, 220, 220),
            BackColor = Color.Transparent,
            Checked = progress.Checks.TryGetValue(id, out bool isChecked) && isChecked
        };
        verified.CheckedChanged += (_, _) =>
        {
            progress.Checks[id] = verified.Checked;
            SaveProgress();
        };

        card.Controls.Add(heading);
        card.Controls.Add(detail);
        card.Controls.Add(verified);

        if (actionText is not null && action is not null)
        {
            var link = new LinkLabel
            {
                Text = actionText,
                AutoSize = true,
                LinkColor = AccentColor,
                ActiveLinkColor = Color.White,
                VisitedLinkColor = AccentColor,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent
            };
            link.LinkClicked += (_, _) => action();
            card.Controls.Add(link);

            void PositionLink()
            {
                link.Location = new Point(
                    Math.Max(180, card.ClientSize.Width - link.PreferredWidth - 18),
                    95);
            }
            card.Resize += (_, _) =>
            {
                heading.Width = Math.Max(250, card.ClientSize.Width - 30);
                detail.Width = Math.Max(250, card.ClientSize.Width - 32);
                PositionLink();
            };
            PositionLink();
        }

        page.Controls.Add(card);
    }

    private void AddBenchmarkCard(FlowLayoutPanel page)
    {
        var card = new Panel
        {
            Width = Math.Max(400, page.ClientSize.Width - page.Padding.Horizontal - 8),
            Height = 174,
            BackColor = CardColor,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(14)
        };

        card.Controls.Add(new Label
        {
            Text = "Saisir les scores de benchmark",
            Location = new Point(15, 10),
            Size = new Size(card.Width - 30, 22),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Font = AppFonts.GetBoldFont(9.5f),
            ForeColor = AccentColor,
            BackColor = Color.Transparent
        });
        card.Controls.Add(new Label
        {
            Text = "Exécutez le même test avant et après les réglages, puis entrez les scores ci-dessous.",
            Location = new Point(16, 36),
            Size = new Size(card.Width - 32, 24),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = MutedColor,
            BackColor = Color.Transparent
        });

        card.Controls.Add(new Label
        {
            Text = "Avant",
            Location = new Point(16, 71),
            AutoSize = true,
            ForeColor = Color.White,
            BackColor = Color.Transparent
        });
        beforeScore = CreateScoreInput(progress.Baseline);
        beforeScore.Location = new Point(72, 67);
        card.Controls.Add(beforeScore);

        card.Controls.Add(new Label
        {
            Text = "Après",
            Location = new Point(245, 71),
            AutoSize = true,
            ForeColor = Color.White,
            BackColor = Color.Transparent
        });
        afterScore = CreateScoreInput(progress.Optimized);
        afterScore.Location = new Point(300, 67);
        card.Controls.Add(afterScore);

        scoreComparison = new Label
        {
            Location = new Point(16, 112),
            Size = new Size(card.Width - 32, 25),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Font = AppFonts.GetBoldFont(10f),
            ForeColor = AccentColor,
            BackColor = Color.Transparent
        };
        card.Controls.Add(scoreComparison);

        beforeScore.ValueChanged += (_, _) =>
        {
            progress.Baseline = beforeScore.Value;
            UpdateComparison();
            SaveProgress();
        };
        afterScore.ValueChanged += (_, _) =>
        {
            progress.Optimized = afterScore.Value;
            UpdateComparison();
            SaveProgress();
        };
        card.Resize += (_, _) =>
        {
            foreach (Control child in card.Controls)
            {
                if (child is Label label && label.Anchor.HasFlag(AnchorStyles.Right))
                    label.Width = Math.Max(250, card.ClientSize.Width - 32);
            }
        };
        page.Controls.Add(card);
        UpdateComparison();
    }

    private static NumericUpDown CreateScoreInput(decimal? initialValue)
    {
        return new NumericUpDown
        {
            Minimum = 0,
            Maximum = 10_000_000,
            DecimalPlaces = 0,
            ThousandsSeparator = true,
            Value = initialValue.HasValue && initialValue.Value > 0
                ? Math.Min(initialValue.Value, 10_000_000)
                : 0,
            Width = 145,
            BackColor = Color.FromArgb(24, 24, 24),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    private void UpdateComparison()
    {
        if (beforeScore is null || afterScore is null || scoreComparison is null)
            return;

        if (beforeScore.Value <= 0 || afterScore.Value <= 0)
        {
            scoreComparison.Text = "Entrez les deux scores pour afficher l'évolution.";
            return;
        }

        decimal difference = afterScore.Value - beforeScore.Value;
        decimal percentage = difference / beforeScore.Value * 100m;
        string sign = difference > 0 ? "+" : string.Empty;
        scoreComparison.Text =
            $"{sign}{difference:N0} points  •  {sign}{percentage:0.00} %";
        scoreComparison.ForeColor = difference >= 0
            ? Color.FromArgb(134, 239, 172)
            : Color.FromArgb(252, 165, 165);
    }

    private OptimizationProgress LoadProgress()
    {
        try
        {
            if (File.Exists(progressPath))
                return JsonSerializer.Deserialize<OptimizationProgress>(
                    File.ReadAllText(progressPath)) ?? new OptimizationProgress();
        }
        catch
        {
            // Un fichier de progression illisible ne doit pas empêcher l'ouverture de l'assistant.
        }

        return new OptimizationProgress();
    }

    private void SaveProgress()
    {
        try
        {
            string? directory = Path.GetDirectoryName(progressPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(progressPath, JsonSerializer.Serialize(progress,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // La checklist reste utilisable même si l'écriture locale échoue.
        }
    }

    private static void OpenTaskManager()
    {
        try
        {
            Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowLaunchError("le Gestionnaire des tâches", ex);
        }
    }

    private static void OpenRestorePoint()
    {
        try
        {
            string protectionTool = Path.Combine(
                Environment.SystemDirectory,
                "SystemPropertiesProtection.exe");
            Process.Start(new ProcessStartInfo(protectionTool) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowLaunchError("la Protection du système", ex);
        }
    }

    private static void OpenGpuControlPanel()
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string[] candidates =
        {
            Path.Combine(programFiles, "NVIDIA Corporation", "Control Panel Client", "nvcplui.exe"),
            Path.Combine(programFiles, "AMD", "CNext", "CNext", "RadeonSoftware.exe")
        };

        try
        {
            string? panel = candidates.FirstOrDefault(File.Exists);
            if (panel is not null)
            {
                Process.Start(new ProcessStartInfo(panel) { UseShellExecute = true });
                return;
            }

            OpenTarget("ms-settings:display-graphics");
            MessageBox.Show(
                "Le panneau NVIDIA/AMD n'a pas été trouvé. Les paramètres graphiques Windows sont ouverts.",
                "GameEcoBot",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowLaunchError("les paramètres du GPU", ex);
        }
    }

    private static void OpenTarget(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowLaunchError(target, ex);
        }
    }

    private static void ShowLaunchError(string target, Exception ex)
    {
        MessageBox.Show(
            $"Impossible d'ouvrir {target}.\n{ex.Message}",
            "GameEcoBot",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private sealed class OptimizationProgress
    {
        public Dictionary<string, bool> Checks { get; set; } = new();
        public decimal? Baseline { get; set; }
        public decimal? Optimized { get; set; }
    }
}
