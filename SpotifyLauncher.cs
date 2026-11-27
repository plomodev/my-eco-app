using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace GameEcoBot;

/// <summary>
/// Gère la détection et le lancement automatique de Spotify en mode minimisé dès l'ouverture de l'application.
/// </summary>
public static class SpotifyLauncher
{
    /// <summary>
    /// Recherche l'exécutable Spotify sur le système de l'utilisateur.
    /// </summary>
    public static string? FindSpotifyExecutablePath()
    {
        // 1. Emplacement standard Roaming AppData
        string appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Spotify\Spotify.exe");
        if (File.Exists(appDataPath)) return appDataPath;

        // 2. Emplacement Microsoft Store (WindowsApps)
        string localStorePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Microsoft\WindowsApps\Spotify.exe");
        if (File.Exists(localStorePath)) return localStorePath;

        // 3. Registre Windows HKCU App Paths
        try
        {
            using var hkcuKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\Spotify.exe");
            string? hkcuPath = hkcuKey?.GetValue("") as string;
            if (!string.IsNullOrWhiteSpace(hkcuPath) && File.Exists(hkcuPath)) return hkcuPath;
        }
        catch { }

        // 4. Registre Windows HKLM App Paths
        try
        {
            using var hklmKey = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\Spotify.exe");
            string? hklmPath = hklmKey?.GetValue("") as string;
            if (!string.IsNullOrWhiteSpace(hklmPath) && File.Exists(hklmPath)) return hklmPath;
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Vérifie si Spotify est déjà en cours d'exécution.
    /// </summary>
    public static bool IsSpotifyRunning()
    {
        try
        {
            return Process.GetProcessesByName("Spotify").Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Démarre Spotify en mode minimisé dès le démarrage de GameEcoBot s'il n'est pas déjà actif.
    /// </summary>
    public static void StartSpotifyMinimized()
    {
        if (IsSpotifyRunning()) return;

        string? spotifyPath = FindSpotifyExecutablePath();
        if (string.IsNullOrWhiteSpace(spotifyPath) || !File.Exists(spotifyPath)) return;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = spotifyPath,
                Arguments = "--minimized",
                WindowStyle = ProcessWindowStyle.Minimized,
                UseShellExecute = true
            };

            Process.Start(startInfo);

            // Tâche de maintien en arrière-plan pendant les premières secondes pour s'assurer
            // que la fenêtre ne s'affiche pas au premier plan
            _ = Task.Run(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(500);
                    var procs = Process.GetProcessesByName("Spotify");
                    foreach (var p in procs)
                    {
                        try
                        {
                            if (p.MainWindowHandle != IntPtr.Zero)
                            {
                                NativeMethods.ShowWindow(p.MainWindowHandle, NativeMethods.SW_SHOWMINNOACTIVE);
                            }
                        }
                        catch { }
                    }
                }
            });
        }
        catch { }
    }
}
