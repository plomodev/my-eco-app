using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GameEcoBot;

/// <summary>
/// Service central de gestion du Wallpaper Engine (fond d'écran animé, vidéo, photo et shaders).
/// Intègre la mise en pause automatique pendant les jeux pour garantir un framerate (FPS) maximal.
/// </summary>
public sealed class WallpaperEngineService
{
    private static readonly Lazy<WallpaperEngineService> _lazy = new(() => new WallpaperEngineService());
    public static WallpaperEngineService Instance => _lazy.Value;

    private WallpaperHostForm? _hostForm;
    private readonly string _configFilePath;

    public bool IsActive { get; private set; } = false;
    public bool IsPaused { get; private set; } = false;
    public bool AutoPauseOnGame { get; set; } = true;
    public float Volume { get; set; } = 0f;
    public string CurrentMode { get; private set; } = "None"; // "Video", "Photo", "Preset", "None"
    public string CurrentTarget { get; private set; } = "";
    public string CurrentPresetId { get; private set; } = "monochrome_waves";

    public event Action<string>? StatusChanged;

    private WallpaperEngineService()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GameEcoBot");
        Directory.CreateDirectory(dir);
        _configFilePath = Path.Combine(dir, "wallpaper_config.json");
    }

    public async Task InitializeOnStartupAsync()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                string json = await File.ReadAllTextAsync(_configFilePath);
                var cfg = JsonSerializer.Deserialize<WallpaperConfigModel>(json);
                if (cfg != null && cfg.IsActive)
                {
                    AutoPauseOnGame = cfg.AutoPauseOnGame;
                    Volume = cfg.Volume;

                    if (cfg.Mode == "Video" && File.Exists(cfg.Target))
                    {
                        await SetVideoWallpaperAsync(cfg.Target, Volume);
                    }
                    else if (cfg.Mode == "Photo" && File.Exists(cfg.Target))
                    {
                        await SetPhotoWallpaperAsync(cfg.Target, cfg.AnimatePhoto);
                    }
                    else if (cfg.Mode == "Preset" && !string.IsNullOrWhiteSpace(cfg.PresetId))
                    {
                        await SetPresetWallpaperAsync(cfg.PresetId);
                    }
                }
            }
        }
        catch { }
    }

    public async Task SetVideoWallpaperAsync(string videoPath, float volume)
    {
        if (!File.Exists(videoPath)) return;

        EnsureHostForm();
        Volume = volume;
        CurrentMode = "Video";
        CurrentTarget = videoPath;
        IsActive = true;
        IsPaused = false;

        if (_hostForm != null)
        {
            await _hostForm.PlayVideoAsync(videoPath, volume, volume <= 0.001f);
        }

        SaveConfig();
        NotifyStatus($"Vidéo active : {Path.GetFileName(videoPath)}");
    }

    public async Task SetPhotoWallpaperAsync(string photoPath, bool animateLiveWallpaper)
    {
        if (!File.Exists(photoPath)) return;

        // 1. Toujours appliquer comme fond d'écran officiel Windows (persiste même après fermeture)
        SetWindowsNativeWallpaper(photoPath);

        if (animateLiveWallpaper)
        {
            EnsureHostForm();
            CurrentMode = "Photo";
            CurrentTarget = photoPath;
            IsActive = true;
            IsPaused = false;

            if (_hostForm != null)
            {
                await _hostForm.ShowImageAsync(photoPath, true);
            }
            NotifyStatus($"Photo animée active : {Path.GetFileName(photoPath)}");
        }
        else
        {
            // Mode statique pur : fermer le live wallpaper s'il tournait pour économiser 100% des ressources
            StopLiveWallpaper(false);
            CurrentMode = "PhotoStatic";
            CurrentTarget = photoPath;
            NotifyStatus($"Fond d'écran Windows défini : {Path.GetFileName(photoPath)}");
        }

        SaveConfig();
    }

    public async Task SetPresetWallpaperAsync(string presetId)
    {
        EnsureHostForm();
        CurrentMode = "Preset";
        CurrentPresetId = presetId;
        CurrentTarget = presetId;
        IsActive = true;
        IsPaused = false;

        if (_hostForm != null)
        {
            await _hostForm.PlayPresetAsync(presetId);
        }

        SaveConfig();
        string name = presetId switch
        {
            "matrix_rain" => "Pluie Digitale Matrix",
            "deep_space" => "Deep Space Starfield 3D",
            _ => "Ondes Minimalistes Monochromes"
        };
        NotifyStatus($"Preset actif : {name}");
    }

    public async Task SetPausedAsync(bool paused)
    {
        if (!IsActive || _hostForm == null) return;
        IsPaused = paused;
        await _hostForm.SetPausedAsync(paused);
        NotifyStatus(paused ? "Fond d'écran en pause" : "Lecture reprise");
    }

    public async Task SetVolumeAsync(float volume)
    {
        Volume = Math.Clamp(volume, 0f, 1f);
        if (_hostForm != null)
        {
            await _hostForm.SetVolumeAsync(Volume);
        }
        SaveConfig();
        NotifyStatus(Volume <= 0.001f ? "Audio : Muet" : $"Volume : {(int)(Volume * 100)}%");
    }

    public void StopLiveWallpaper(bool restoreWindowsBackground = true)
    {
        if (_hostForm != null)
        {
            try
            {
                _hostForm.Hide();
                _hostForm.Close();
                _hostForm.Dispose();
            }
            catch { }
            _hostForm = null;
        }

        IsActive = false;
        IsPaused = false;
        CurrentMode = "None";
        CurrentTarget = "";

        if (restoreWindowsBackground)
        {
            // Forcer le rafraîchissement du fond d'écran standard Windows
            NativeMethods.SystemParametersInfoSetWallpaper(
                NativeMethods.SPI_SETDESKWALLPAPER,
                0,
                null,
                NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE);
        }

        SaveConfig();
        NotifyStatus("Fond d'écran animé désactivé (Bureau Windows standard)");
    }

    public void OnGameStatusChanged(bool isGameRunning)
    {
        if (!IsActive || !AutoPauseOnGame || _hostForm == null) return;

        if (isGameRunning && !IsPaused)
        {
            _ = SetPausedAsync(true);
            NotifyStatus("En pause automatique (Jeu détecté - Économie GPU active)");
        }
        else if (!isGameRunning && IsPaused)
        {
            _ = SetPausedAsync(false);
            NotifyStatus("Reprise automatique (Jeu terminé)");
        }
    }

    public static bool SetWindowsNativeWallpaper(string imagePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
            if (key != null)
            {
                // Style 10 = Remplir (Fill), Tile = 0
                key.SetValue("WallpaperStyle", "10");
                key.SetValue("TileWallpaper", "0");
            }

            return NativeMethods.SystemParametersInfoSetWallpaper(
                NativeMethods.SPI_SETDESKWALLPAPER,
                0,
                imagePath,
                NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE);
        }
        catch
        {
            return false;
        }
    }

    private void EnsureHostForm()
    {
        if (_hostForm == null || _hostForm.IsDisposed)
        {
            _hostForm = new WallpaperHostForm();
            _hostForm.Show();
        }
    }

    private void NotifyStatus(string message)
    {
        StatusChanged?.Invoke(message);
    }

    private void SaveConfig()
    {
        try
        {
            var model = new WallpaperConfigModel
            {
                IsActive = IsActive,
                Mode = CurrentMode,
                Target = CurrentTarget,
                PresetId = CurrentPresetId,
                Volume = Volume,
                AutoPauseOnGame = AutoPauseOnGame,
                AnimatePhoto = true
            };
            string json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configFilePath, json);
        }
        catch { }
    }

    private sealed class WallpaperConfigModel
    {
        public bool IsActive { get; set; }
        public string Mode { get; set; } = "None";
        public string Target { get; set; } = "";
        public string PresetId { get; set; } = "";
        public float Volume { get; set; }
        public bool AutoPauseOnGame { get; set; } = true;
        public bool AnimatePhoto { get; set; } = true;
    }
}
