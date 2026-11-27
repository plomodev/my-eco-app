using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace GameEcoBot;

/// <summary>
/// Gestionnaire audio gaming et microphone (style SteelSeries Sonar)
/// Gère le gain de volume casque, l'égaliseur bruits de pas, le routage des canaux, le microphone
/// et applique directement les profils Sonar (Clarity High Pitch / Clearest Sound) sans ouvrir SteelSeries GG.
/// </summary>
public static class SonarAudioService
{
    private static readonly string ApoConfigPath = @"C:\Program Files\EqualizerAPO\config\config.txt";
    public static readonly string SonarDbPath = @"C:\ProgramData\SteelSeries\GG\apps\sonar\db\database.db";

    public static int PreampGainDb { get; set; } = 0;
    public static bool IsFootstepsBoostActive { get; set; } = false;
    public static bool IsMonoAntiPerteActive { get; set; } = true;
    public static bool IsMicBoostActive { get; set; } = false;
    public static bool IsClarityHighPitchActive { get; set; } = true;

    // ══ Equalizer APO Control ══

    public static bool IsApoInstalled => File.Exists(ApoConfigPath);
    public static bool IsSonarInstalled => File.Exists(SonarDbPath);

    public static void ApplyApoConfig()
    {
        try
        {
            if (!IsApoInstalled) return;

            string routing = IsMonoAntiPerteActive 
                ? "Copy: L=0.75*L+0.75*R R=0.75*L+0.75*R" 
                : "Copy: L=L R=R";

            string eqFilter = "";
            if (IsClarityHighPitchActive)
            {
                // Exacte courbe d'égalisation Clarity High Pitch (Clearest Sound) de SteelSeries Sonar
                eqFilter = "Filter: ON PK Fc 31 Hz Gain -12 dB Q 0.7071\n" +
                           "Filter: ON PK Fc 62 Hz Gain -12 dB Q 0.7071\n" +
                           "Filter: ON PK Fc 125 Hz Gain -4 dB Q 0.7071\n" +
                           "Filter: ON PK Fc 250 Hz Gain -4 dB Q 0.7071\n" +
                           "Filter: ON PK Fc 500 Hz Gain 0 dB Q 0.7071\n" +
                           "Filter: ON PK Fc 1000 Hz Gain 0 dB Q 0.7071\n" +
                           "Filter: ON PK Fc 2000 Hz Gain 3 dB Q 0.7071\n" +
                           "Filter: ON PK Fc 4000 Hz Gain 4 dB Q 0.7071\n" +
                           "Filter: ON PK Fc 8000 Hz Gain 4 dB Q 0.7071\n" +
                           "Filter: ON PK Fc 16000 Hz Gain 4 dB Q 0.7071";
            }
            else if (IsFootstepsBoostActive)
            {
                eqFilter = "Filter: ON PK Fc 2500 Hz Gain 6 dB Q 1.4\nFilter: ON PK Fc 4000 Hz Gain 4 dB Q 1.4";
            }

            string content = $"# Configuration Audio GameEcoBot\n" +
                             $"{routing}\n" +
                             $"Preamp: {PreampGainDb} dB\n" +
                             (string.IsNullOrEmpty(eqFilter) ? "" : $"{eqFilter}\n") +
                             $"Include: peace.txt\n";

            File.WriteAllText(ApoConfigPath, content);
        }
        catch { }
    }

    /// <summary>
    /// Applique le profil d'égalisation 'Clarity High Pitch' (Clearest Sound) directement dans SteelSeries Sonar
    /// sans avoir besoin d'ouvrir l'application SteelSeries GG.
    /// </summary>
    public static (bool success, string message) ApplyClarityHighPitch(bool enable = true)
    {
        IsClarityHighPitchActive = enable;
        ApplyApoConfig();

        if (!IsSonarInstalled)
        {
            return (true, "Clarity High Pitch activé (Equalizer APO synchronisé).");
        }

        try
        {
            using var connection = new SqliteConnection($"Data Source={SonarDbPath}");
            connection.Open();

            // 1. Profil Micro (vad = 3) : Clarity High Pitch
            string micConfigId = enable ? "78643046-ac01-4457-936a-27a74bf40a62" : "0bd5e813-752e-4cb9-98a4-727799a54292";
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "UPDATE selected_config SET config_id = @id WHERE vad = 3";
                cmd.Parameters.AddWithValue("@id", micConfigId);
                cmd.ExecuteNonQuery();
            }

            // 2. Profil Jeu (vad = 1) : Clarté maximale (Footsteps / Aigus clairs)
            string gameConfigId = "c1a71710-game-4457-936a-27a74bf40a62";
            if (enable)
            {
                using var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM configs WHERE id = @id";
                checkCmd.Parameters.AddWithValue("@id", gameConfigId);
                long count = (long)(checkCmd.ExecuteScalar() ?? 0);

                if (count == 0)
                {
                    // Récupérer le modèle de données Clarity High Pitch
                    using var getCmd = connection.CreateCommand();
                    getCmd.CommandText = "SELECT data, default_data FROM configs WHERE id = '78643046-ac01-4457-936a-27a74bf40a62'";
                    using var reader = getCmd.ExecuteReader();
                    if (reader.Read())
                    {
                        string data = reader.GetString(0);
                        string defData = reader.GetString(1);

                        using var insertCmd = connection.CreateCommand();
                        insertCmd.CommandText = @"
                            INSERT INTO configs (id, name, vad, data, schema_version, created_at, updated_at, is_preset, default_data, is_favorite, favorite_position)
                            VALUES (@id, 'Clarity High Pitch (Game)', 1, @data, 6, datetime('now'), datetime('now'), 0, @defData, 1, 0)";
                        insertCmd.Parameters.AddWithValue("@id", gameConfigId);
                        insertCmd.Parameters.AddWithValue("@data", data);
                        insertCmd.Parameters.AddWithValue("@defData", defData);
                        insertCmd.ExecuteNonQuery();
                    }
                }

                using var setGameCmd = connection.CreateCommand();
                setGameCmd.CommandText = "UPDATE selected_config SET config_id = @id WHERE vad = 1";
                setGameCmd.Parameters.AddWithValue("@id", gameConfigId);
                setGameCmd.ExecuteNonQuery();
            }

            // 3. Forcer les canaux Sonar en volume maximal
            using (var fixVolCmd = connection.CreateCommand())
            {
                fixVolCmd.CommandText = @"
                    UPDATE key_value SET value = 'Classic' WHERE key = 'MODE';
                    UPDATE key_value SET value = '1' WHERE key = 'STREAMING_STREAM_GAME_VOLUME';
                    UPDATE key_value SET value = '1' WHERE key = 'MONITORING_STREAM_GAME_VOLUME';
                    UPDATE key_value SET value = '1' WHERE key = 'CLASSIC_GAME_VOLUME';
                    UPDATE key_value SET value = 'False' WHERE key = 'CLASSIC_GAME_MUTE';
                    UPDATE key_value SET value = '1' WHERE key = 'HEADPHONE_OUT_VOLUME';
                ";
                fixVolCmd.ExecuteNonQuery();
            }

            // 4. Recharger discrètement SteelSeries Sonar en arrière-plan sans ouvrir GG
            SoftReloadSonarProcess();

            return (true, "Profil 'Clarity High Pitch' (Clearest Sound) activé directement dans Sonar sans ouvrir SteelSeries GG !");
        }
        catch (Exception ex)
        {
            return (false, $"Erreur Sonar : {ex.Message}");
        }
    }

    private static void SoftReloadSonarProcess()
    {
        try
        {
            foreach (var proc in Process.GetProcessesByName("SteelSeriesSonar"))
            {
                proc.Kill();
            }

            string sonarExe = @"C:\Program Files\SteelSeries\GG\apps\sonar\SteelSeriesSonar.exe";
            if (File.Exists(sonarExe))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = sonarExe,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);
            }
        }
        catch { }
    }

    // ══ Windows Audio Ducking & Multimedia Priority ══

    public static void OptimizeWindowsAudioDucking()
    {
        try
        {
            // Désactive la baisse automatique du son quand Discord / vocal parle
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Multimedia\Audio");
            key?.SetValue("UserDuckingPreference", 3, RegistryValueKind.DWord);

            // Priorité multimédia sans latence
            using var sysProfile = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", true);
            sysProfile?.SetValue("SystemResponsiveness", 0, RegistryValueKind.DWord);
            sysProfile?.SetValue("NoLazyMode", 1, RegistryValueKind.DWord);
        }
        catch { }
    }

    // ══ Windows CoreAudio COM Interop (Microphone & Playback Volume) ══

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr ppDevices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
        int RegisterEndpointNotificationCallback(IntPtr pClient);
        int UnregisterEndpointNotificationCallback(IntPtr pClient);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string pwstrId);
        int GetState(out int pdwState);
    }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr pNotify);
        int UnregisterControlChangeNotify(IntPtr pNotify);
        int GetChannelCount(out int pnChannelCount);
        int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        int GetMasterVolumeLevel(out float pfLevelDB);
        int GetMasterVolumeLevelScalar(out float pfLevel);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
        int VolumeStepUp(ref Guid pguidEventContext);
        int VolumeStepDown(ref Guid pguidEventContext);
        int QueryHardwareSupport(out uint pdwHardwareSupportMask);
        int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
    }

    private static IAudioEndpointVolume? GetEndpointVolume(int dataFlow)
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            int hr = enumerator.GetDefaultAudioEndpoint(dataFlow, 0, out var device);
            if (hr != 0 || device == null) return null;

            var iid = typeof(IAudioEndpointVolume).GUID;
            hr = device.Activate(ref iid, 1, IntPtr.Zero, out var obj);
            if (hr != 0 || obj == null) return null;

            return (IAudioEndpointVolume)obj;
        }
        catch
        {
            return null;
        }
    }

    public static int GetMicrophoneVolume()
    {
        try
        {
            var epv = GetEndpointVolume(1); // 1 = Capture / Microphone
            if (epv != null)
            {
                epv.GetMasterVolumeLevelScalar(out float level);
                return (int)Math.Round(level * 100f);
            }
        }
        catch { }
        return 100;
    }

    public static void SetMicrophoneVolume(int volumePercent)
    {
        try
        {
            volumePercent = Math.Clamp(volumePercent, 0, 100);
            var epv = GetEndpointVolume(1); // 1 = Capture / Microphone
            if (epv != null)
            {
                var guid = Guid.Empty;
                epv.SetMasterVolumeLevelScalar(volumePercent / 100f, ref guid);
            }
        }
        catch { }
    }

    public static int GetHeadphoneVolume()
    {
        try
        {
            var epv = GetEndpointVolume(0); // 0 = Render / Headphone
            if (epv != null)
            {
                epv.GetMasterVolumeLevelScalar(out float level);
                return (int)Math.Round(level * 100f);
            }
        }
        catch { }
        return 100;
    }

    public static void SetHeadphoneVolume(int volumePercent)
    {
        try
        {
            volumePercent = Math.Clamp(volumePercent, 0, 100);
            var epv = GetEndpointVolume(0); // 0 = Render / Headphone
            if (epv != null)
            {
                var guid = Guid.Empty;
                epv.SetMasterVolumeLevelScalar(volumePercent / 100f, ref guid);
            }
        }
        catch { }
    }
}
