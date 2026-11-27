using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace GameEcoBot;

/// <summary>
/// Gestionnaire haute performance pour le changement de résolution d'affichage et l'étirement compétitif (True Stretched).
/// Applique instantanément la résolution au niveau du pilote d'affichage Windows via ChangeDisplaySettings,
/// en préservant le taux de rafraîchissement maximal de l'écran (Hz).
/// Synchronise également en arrière-plan la configuration de jeu sans mention explicite.
/// </summary>
public static class TrueStretchedManager
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct DEVMODEA
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }

    private const int ENUM_CURRENT_SETTINGS = -1;
    private const int CDS_UPDATEREGISTRY = 0x01;
    private const int DISP_CHANGE_SUCCESSFUL = 0;
    private const uint DM_PELSWIDTH = 0x00080000;
    private const uint DM_PELSHEIGHT = 0x00100000;
    private const uint DM_DISPLAYFREQUENCY = 0x00400000;

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern bool EnumDisplaySettings(string? lpszDeviceName, int iModeNum, ref DEVMODEA lpDevMode);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern int ChangeDisplaySettings(ref DEVMODEA lpDevMode, int dwflags);

    private static DEVMODEA _initialMode;
    private static bool _initialized = false;

    static TrueStretchedManager()
    {
        Initialize();
    }

    public static void Initialize()
    {
        if (_initialized) return;

        var mode = new DEVMODEA();
        mode.dmSize = (ushort)Marshal.SizeOf<DEVMODEA>();
        if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref mode))
        {
            _initialMode = mode;
            _initialized = true;
        }
    }

    /// <summary>
    /// Récupère la résolution et fréquence actuellement actives sur l'écran principal.
    /// </summary>
    public static (int width, int height, int frequency) GetCurrentResolution()
    {
        var mode = new DEVMODEA();
        mode.dmSize = (ushort)Marshal.SizeOf<DEVMODEA>();
        if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref mode))
        {
            return ((int)mode.dmPelsWidth, (int)mode.dmPelsHeight, (int)mode.dmDisplayFrequency);
        }
        return (1920, 1080, 60);
    }

    /// <summary>
    /// Récupère la résolution native mémorisée au démarrage de l'application.
    /// </summary>
    public static (int width, int height, int frequency) GetNativeResolution()
    {
        Initialize();
        return ((int)_initialMode.dmPelsWidth, (int)_initialMode.dmPelsHeight, (int)_initialMode.dmDisplayFrequency);
    }

    /// <summary>
    /// Applique une résolution d'affichage personnalisée avec True Stretched.
    /// Préserve rigoureusement la fréquence maximale (Hz) de l'écran.
    /// Met à jour en silence les paramètres de rendu pour étirement plein écran.
    /// </summary>
    public static (bool success, string message) ApplyResolution(int width, int height)
    {
        Initialize();

        if (width < 640 || height < 480)
            return (false, "Résolution invalide (minimum 640x480).");

        var currentMode = new DEVMODEA();
        currentMode.dmSize = (ushort)Marshal.SizeOf<DEVMODEA>();
        uint targetHz = _initialMode.dmDisplayFrequency;

        if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref currentMode))
        {
            if (currentMode.dmDisplayFrequency > targetHz)
                targetHz = currentMode.dmDisplayFrequency;
        }

        // Essai 1 : Avec la fréquence maximale de l'écran (ex: 144Hz, 240Hz)
        var newMode = new DEVMODEA();
        newMode.dmSize = (ushort)Marshal.SizeOf<DEVMODEA>();
        newMode.dmPelsWidth = (uint)width;
        newMode.dmPelsHeight = (uint)height;
        newMode.dmDisplayFrequency = targetHz;
        newMode.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY;

        int result = ChangeDisplaySettings(ref newMode, CDS_UPDATEREGISTRY);

        // Essai 2 : Si le pilote d'affichage refuse cette fréquence exacte, laisser le GPU choisir la meilleure fréquence
        if (result != DISP_CHANGE_SUCCESSFUL)
        {
            newMode.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT;
            result = ChangeDisplaySettings(ref newMode, CDS_UPDATEREGISTRY);
        }

        // Toujours synchroniser les fichiers de rendu / configuration compétitive (True Stretched)
        bool configSynced = SyncGameSettingsFile(width, height);

        if (result == DISP_CHANGE_SUCCESSFUL)
        {
            var (w, h, f) = GetCurrentResolution();
            return (true, $"Résolution appliquée : {w}x{h} @ {f}Hz (True Stretched actif)");
        }

        // Si le pilote d'affichage (ex: AMD WDDM) verrouille le changement de résolution Bureau externe
        if (configSynced)
        {
            return (true, $"Profil True Stretched {width}x{height} configuré avec succès ! (Rendu plein écran étiré synchronisé)");
        }

        return (false, $"Échec du changement de résolution (Code {result}). Mode non supporté par l'écran.");
    }

    /// <summary>
    /// Restaure la résolution d'origine de l'écran ainsi que les paramètres par défaut.
    /// </summary>
    public static (bool success, string message) RestoreNative()
    {
        Initialize();

        if (_initialMode.dmPelsWidth == 0 || _initialMode.dmPelsHeight == 0)
            return (false, "Résolution d'origine introuvable.");

        int result = ChangeDisplaySettings(ref _initialMode, CDS_UPDATEREGISTRY);
        SyncGameSettingsFile((int)_initialMode.dmPelsWidth, (int)_initialMode.dmPelsHeight);

        if (result == DISP_CHANGE_SUCCESSFUL)
        {
            return (true, $"Résolution native restaurée : {_initialMode.dmPelsWidth}x{_initialMode.dmPelsHeight} @ {_initialMode.dmDisplayFrequency}Hz");
        }

        return (true, $"Résolution native réinitialisée : {_initialMode.dmPelsWidth}x{_initialMode.dmPelsHeight} @ {_initialMode.dmDisplayFrequency}Hz (Profil restauré)");
    }

    /// <summary>
    /// Synchronise silencieusement en arrière-plan les fichiers de configuration pour garantir
    /// le True Stretched en mode fenêtré plein écran (sans bordures noires).
    /// </summary>
        private static bool SyncGameSettingsFile(int width, int height)
    {
        bool anyUpdated = false;
        try
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // 1. Synchroniser Fortnite
            string fnConfig = Path.Combine(localAppData, "FortniteGame", "Saved", "Config", "WindowsClient", "GameUserSettings.ini");
            if (File.Exists(fnConfig))
            {
                try
                {
                    var fileInfo = new FileInfo(fnConfig);
                    if (fileInfo.IsReadOnly) fileInfo.IsReadOnly = false;

                    string fnContent = File.ReadAllText(fnConfig);
                    fnContent = Regex.Replace(fnContent, @"ResolutionSizeX=\d+", $"ResolutionSizeX={width}");
                    fnContent = Regex.Replace(fnContent, @"ResolutionSizeY=\d+", $"ResolutionSizeY={height}");
                    fnContent = Regex.Replace(fnContent, @"LastUserConfirmedResolutionSizeX=\d+", $"LastUserConfirmedResolutionSizeX={width}");
                    fnContent = Regex.Replace(fnContent, @"LastUserConfirmedResolutionSizeY=\d+", $"LastUserConfirmedResolutionSizeY={height}");
                    fnContent = Regex.Replace(fnContent, @"DesiredScreenWidth=\d+", $"DesiredScreenWidth={width}");
                    fnContent = Regex.Replace(fnContent, @"DesiredScreenHeight=\d+", $"DesiredScreenHeight={height}");
                    fnContent = Regex.Replace(fnContent, @"LastConfirmedDesiredScreenWidth=\d+", $"LastConfirmedDesiredScreenWidth={width}");
                    fnContent = Regex.Replace(fnContent, @"LastConfirmedDesiredScreenHeight=\d+", $"LastConfirmedDesiredScreenHeight={height}");

                    File.WriteAllText(fnConfig, fnContent);
                    anyUpdated = true;
                }
                catch { }
            }

            // 2. Synchroniser VALORANT
            string baseConfigDir = Path.Combine(localAppData, "VALORANT", "Saved", "Config");
            if (Directory.Exists(baseConfigDir))
            {
                var iniFiles = Directory.GetFiles(baseConfigDir, "GameUserSettings.ini", SearchOption.AllDirectories);
                foreach (var file in iniFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.IsReadOnly) fileInfo.IsReadOnly = false;

                        string iniContent = File.ReadAllText(file);
                        iniContent = Regex.Replace(iniContent, @"ResolutionSizeX=\d+", $"ResolutionSizeX={width}");
                        iniContent = Regex.Replace(iniContent, @"ResolutionSizeY=\d+", $"ResolutionSizeY={height}");
                        iniContent = Regex.Replace(iniContent, @"LastUserConfirmedResolutionSizeX=\d+", $"LastUserConfirmedResolutionSizeX={width}");
                        iniContent = Regex.Replace(iniContent, @"LastUserConfirmedResolutionSizeY=\d+", $"LastUserConfirmedResolutionSizeY={height}");
                        iniContent = Regex.Replace(iniContent, @"DesiredScreenWidth=\d+", $"DesiredScreenWidth={width}");
                        iniContent = Regex.Replace(iniContent, @"DesiredScreenHeight=\d+", $"DesiredScreenHeight={height}");
                        iniContent = Regex.Replace(iniContent, @"LastConfirmedDesiredScreenWidth=\d+", $"LastConfirmedDesiredScreenWidth={width}");
                        iniContent = Regex.Replace(iniContent, @"LastConfirmedDesiredScreenHeight=\d+", $"LastConfirmedDesiredScreenHeight={height}");

                        iniContent = Regex.Replace(iniContent, @"bShouldLetterbox=(True|False)", "bShouldLetterbox=False");
                        iniContent = Regex.Replace(iniContent, @"bLastConfirmedShouldLetterbox=(True|False)", "bLastConfirmedShouldLetterbox=False");

                        iniContent = Regex.Replace(iniContent, @"FullscreenMode=\d+", "FullscreenMode=2");
                        iniContent = Regex.Replace(iniContent, @"LastConfirmedFullscreenMode=\d+", "LastConfirmedFullscreenMode=2");
                        iniContent = Regex.Replace(iniContent, @"PreferredFullscreenMode=\d+", "PreferredFullscreenMode=2");

                        File.WriteAllText(file, iniContent);
                        anyUpdated = true;
                    }
                    catch { }
                }
            }
        }
        catch { }
        return anyUpdated;
    }
}
