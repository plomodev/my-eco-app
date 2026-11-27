using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace GameEcoBot;

public sealed class ConcurrentSet<T> : IEnumerable<T> where T : notnull
{
    private readonly ConcurrentDictionary<T, byte> items;

    public ConcurrentSet(IEqualityComparer<T>? comparer = null)
    {
        items = new ConcurrentDictionary<T, byte>(comparer ?? EqualityComparer<T>.Default);
    }

    public int Count => items.Count;
    public bool Add(T item) => items.TryAdd(item, 0);
    public bool Contains(T item) => items.ContainsKey(item);

    public IEnumerator<T> GetEnumerator() => items.Keys.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class GameIntelligenceService
{
    private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2.0) };
    private readonly string cacheFilePath;
    private readonly ConcurrentDictionary<string, bool> detectedCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> negativeCheckCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan NegativeCheckCooldown = TimeSpan.FromMinutes(5);

    // Jeux réellement installés et reconnus sur le PC
    public ConcurrentSet<string> KnownGames { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public event Action<string>? OnLog;
    public event Action<int>? OnGamesUpdated;

    // Service d'intelligence internet (SteamSpy, Steam Store, RAWG)
    public InternetIntelligenceService InternetIntel { get; private set; }

    // Applications système, de développement et bureautiques à NE JAMAIS considérer comme des jeux
    private static readonly HashSet<string> ExcludedNonGameProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        // Système Windows
        "explorer", "taskmgr", "dwm", "csrss", "smss", "winlogon", "services", "lsass", "svchost", "conhost",
        "sihost", "taskhostw", "runtimebroker", "startmenuexperiencehost", "searchhost", "shellhost",
        "textinputhost", "securityhealthservice", "securityhealthsystray", "smartscreen", "msmpeng", "nissrv",
        "mpdefendercorereservice", "spoolsv", "fontdrvhost", "ctfmon", "backgroundtaskhost", "dllhost", "wmiprvse",

        // Navigateurs Web
        "chrome", "msedge", "msedgewebview2", "opera", "operagx", "firefox", "brave", "vivaldi", "safari", "arc", "torbrowser",
        "Navigateur Opera GX",

        // Outils Dev & Runtimes (CRITIQUE : Node, Git, Python, Compilateurs NE SONT PAS DES JEUX !)
        "node", "nodejs", "npm", "npx", "yarn", "pnpm", "bun", "deno", "ts-node",
        "cmd", "powershell", "pwsh", "wt", "windowsterminal", "bash", "wsl", "wslhost", "OpenConsole",
        "git", "git-bash", "git-cmd", "python", "pythonw", "python3", "py", "pyrefly", "pylance",
        "ruby", "perl", "dotnet", "msbuild", "vstest",
        "code", "cursor", "antigravity", "devenv", "rider64", "idea64", "pycharm64", "webstorm64", "clion64",

        // Communication & Réseaux Sociaux
        "discord", "discordcanary", "discorddevelopment", "discordptb", "Discord Canary",
        "spotify", "slack", "teams", "telegram", "whatsapp", "signal", "zoom", "skype",

        // Utilitaires de Streaming, Audio & Périphériques
        "GameEcoBot", "Voicemeeter", "voicemeeter8", "voicemeeterpro", "audiodg", "SteelSeriesGG", "SteelSeriesSonar",
        "SteelSeriesEngine", "SteelSeriesPrism", "ExitLag", "AudioRelay", "spacedeskService", "spacedeskServiceTray",
        "Medal", "OBS64", "obs32", "streamlabs", "LogitechGHub", "lghub", "RazerSynapse", "iCUE",

        // Launchers & Clients de boutique (ce sont des launchers, pas les jeux eux-mêmes !)
        "Steam", "steam", "SteamService", "steamwebhelper",
        "EpicGamesLauncher", "Epic Games Launcher", "UnrealEngineLauncher", "UnrealVersionSelector",
        "EpicOnlineServicesInstaller", "EOSBootStrapper", "EOSOverlayRenderer-Win64-Shipping", "Epic Online Services",
        "Riot Client", "RiotClientServices", "RiotClientCrashHandler", "Riot Vanguard", "Client Riot", "vgc", "vgtray", "vgm",
        "Battle.net", "Agent", "EADesktop", "Origin", "UbisoftConnect", "upc", "GalaxyClient",
        "Roblox Studio", "RobloxStudio", "RobloxPlayerInstaller",

        // Pilotes GPU & Optimiseurs
        "amdsoftware", "AMDRyzenMaster", "AMD Ryzen Master", "radeonsettings", "nvcontainer", "nvcplui", "geforceexperience",
        "NVIDIA GeForce Experience", "AMDRSServ", "AMDRSSrcExt", "atieclxx", "atiesrxx", "amdow", "amdfendrsr",

        // Utilitaires divers & Décompresseurs
        "qBittorrent", "utorrent", "WinRAR", "7zFM", "DirectXRedist", "InstallChainer", "QuickSFV", "GameInputRedist",
        "log-uploader", "handler", "AimLabLinksHandler", "Launcher", "Hydra", "EXM", "exmtweaksservice"
    };

    public static bool IsExcluded(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        string clean = name.Trim();
        if (ExcludedNonGameProcesses.Contains(clean)) return true;
        string lower = clean.ToLowerInvariant();
        if (ExcludedNonGameProcesses.Contains(lower)) return true;

        // Runtimes et scripts de dev
        if (lower == "node" || lower.StartsWith("node.") || lower.EndsWith(".js") || lower.EndsWith(".ts") ||
            lower.StartsWith("npm") || lower.StartsWith("yarn") || lower.StartsWith("pnpm") ||
            lower == "cmd" || lower == "powershell" || lower == "pwsh" || lower == "git" ||
            lower == "python" || lower == "pythonw")
        {
            return true;
        }

        // Mots-clés utilitaires
        if (lower.Contains("setup") || lower.Contains("installer") || lower.Contains("unins") ||
            lower.Contains("crash") || lower.Contains("helper") || lower.Contains("updater") ||
            lower.Contains("redist") || lower.Contains("overlay") || lower.Contains("report") ||
            lower.Contains("service") || lower.Contains("handler") || lower.Contains("chainer") ||
            lower.Contains("uploader") || lower.Contains("browser") || lower.Contains("navigateur") ||
            lower.Contains("client riot") || lower.Contains("epic online") ||
            (lower.Contains("studio") && !lower.Contains("visual")))
        {
            return true;
        }

        return false;
    }

    // ── BASE DE CONNAISSANCES IA : +300 JEUX MAJEURS PRÉ-INDEXÉS ──────────
    private static readonly HashSet<string> MasterGameDatabase = new(StringComparer.OrdinalIgnoreCase)
    {
        // FPS & Compétitifs
        "VALORANT", "VALORANT-Win64-Shipping",
        "FortniteClient-Win64-Shipping", "FortniteClient-Win64-Shipping_EAC_EOS", "FortniteClient-Win64-Shipping_EAC", "FortniteBootstrapper", "FortniteLauncher",
        "cs2", "csgo",
        "Overwatch", "Overwatch_Beta",
        "r5apex", "Apex", "ApexLegends",
        "RainbowSix", "RainbowSix_Vulkan", "RainbowSix_BE",
        "cod", "bootstrapper", "t6mp", "t6zm", "t6sp", "ModernWarfare", "Warzone", "CallOfDuty", "BlackOpsColdWar", "Vanguard", "sp24", "mp24", "cod24", "cod23", "cod22", "Warzone2",
        "TslGame", "TslGame_BE", "PUBG",
        "EscapeFromTarkov", "EscapeFromTarkov_BE",
        "Discovery", "Discovery-Win64-Shipping", // The Finals
        "Marvel-Win64-Shipping", "MarvelRivals", // Marvel Rivals
        "Spectre-Win64-Shipping", "Deadlock", "project8", "hl2", "tf_win64", "tf",
        "DeltaForce", "DeltaForce-Win64-Shipping", // Delta Force Hawk Ops
        "ABInfinite", "ArenaBreakout", // Arena Breakout Infinite
        "XDefiant", "XDefiant_BE", "FragPunk", "FragPunk-Win64-Shipping", "ArcRaiders",
        "DayZ", "DayZ_x64", "DayZ_BE", "SCUM", "SquadGame", "arma3", "arma3_x64", "ArmaReforgerSteam",
        "aces", "WarThunder", "WorldOfTanks", "WorldOfWarships",

        // Aim Trainers & Entraînement
        "AimLab", "AimLab_tb", "3D Aim Trainer", "3DAimTrainer", "KovaaK", "FPSAimTrainer", "AimHero",

        // Sandbox, Mondes Ouverts & Jeux Sociaux
        "RobloxPlayerBeta", "RobloxPlayer",
        "javaw", "Minecraft.Windows", "Minecraft",
        "GTA5", "PlayGTAV", "FiveM", "FiveM_GTAProcess", "FiveM_b2699_GTAProcess", "FiveM_b2802_GTAProcess", "FiveM_b3095_GTAProcess", "FiveM_b3258_GTAProcess",
        "RedM", "RDR2", "PlayRDR2",
        "gmod", "Terraria", "RustClient", "Rust", "Unturned", "SeaOfThieves",
        "ArkAscended", "ShooterGame", "Palworld-Win64-Shipping", "Palworld", "helldivers2", "Helldivers2", "Valheim",
        "ONCE_HUMAN", "OnceHuman", "TL", "TL-Win64-Shipping", // Throne and Liberty

        // MOBAs, MMORPG & Stratégie
        "League of Legends", "LeagueClient", "LeagueClientUx",
        "dota2", "Smite", "Smite2", "SC2_x64", "HeroesOfTheStorm_x64", "Brawlhalla",
        "Wow", "WowClassic", "WowT", "Diablo IV", "DiabloIV", "DiabloIIIRef",
        "PathOfExile", "PathOfExile_x64", "PathOfExile2", "PathOfExile2_x64",
        "LostArk", "Destiny2", "Warframe.x64", "Warframe", "TheFirstDescendant", "M1-Win64-Shipping",

        // RPG, Aventure, Gacha & Solo
        "eldenring", "Cyberpunk2077",
        "GenshinImpact", "YuanShen", "HonkaiStarRail", "StarRail", "ZenlessZoneZero", "Client-Win64-Shipping", "Wuthering Waves",
        "bg3", "bg3_dx11", "witcher3", "HogwartsLegacy",
        "DarkSoulsIII", "DarkSoulsRemastered", "DarkSoulsII", "Sekiro", "ArmoredCore6",
        "MonsterHunterWorld", "MonsterHunterRise", "MHRise", "MonsterHunterWilds",
        "b1-Win64-Shipping", // Black Myth Wukong
        "GodOfWar", "Spider-Man", "MilesMorales", "Starfield", "Fallout4", "SkyrimSE", "Skyrim",
        "DeadByDaylight-Win64-Shipping", "Lethal Company", "Phasmophobia", "Content Warning", "Among Us",
        "Hades", "Hades2", "HollowKnight", "SonsOfTheForest", "TheForest", "PacificDrive", "Enshrouded",
        "Stalker2-Win64-Shipping", "Stalker2", "KingdomCome2",

        // Sports & Course
        "RocketLeague", "RocketLeague_DX11",
        "FC24", "FC25", "FIFA23", "FIFA22",
        "ForzaHorizon5", "ForzaHorizon4", "ForzaMotorsport",
        "AssettoCorsa", "acs", "AC2-Win64-Shipping", "BeamNG.drive.x64", "Skate", "F1_24", "F1_23", "F1_22", "NBA2K24", "NBA2K25",
        "NeedForSpeedUnbound", "NFS22", "Trackmania", "Motorfest", "TheCrewMotorfest",

        // Rythme, Simulation & Émulateurs
        "osu!", "Clone Hero",
        "TS4_x64", "TheSims4", "Sims4",
        "EuroTruckSimulator2", "eurotrucks2", "amtrucks", "FlightSimulator", "Cities2", "Cities",
        "GooglePlayGames", "crosvm", "yuzu", "Ryujinx", "rpcs3", "Dolphin", "PCSX2", "shadPS4"
    };

    public GameIntelligenceService()
    {
        string localFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameEcoBot");
        Directory.CreateDirectory(localFolder);
        cacheFilePath = Path.Combine(localFolder, "games_intelligence_cache.json");

        // Pré-charger la base maîtresse
        foreach (var game in MasterGameDatabase)
        {
            RegisterGame(game);
        }

        LoadCache();

        // Initialiser le service internet et lui relayer les logs
        InternetIntel = new InternetIntelligenceService();
        InternetIntel.OnLog += msg => OnLog?.Invoke(msg);
    }

    private void LoadCache()
    {
        try
        {
            if (File.Exists(cacheFilePath))
            {
                string json = File.ReadAllText(cacheFilePath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
                if (dict != null)
                {
                    bool needResave = false;
                    foreach (var kvp in dict)
                    {
                        if (IsExcluded(kvp.Key) || !kvp.Value)
                        {
                            needResave = true;
                            continue;
                        }

                        detectedCache[kvp.Key] = true;
                        KnownGames.Add(kvp.Key);
                    }

                    if (needResave)
                    {
                        SaveCache();
                    }
                }
            }
        }
        catch { }
    }

    private void SaveCache()
    {
        try
        {
            var dict = new Dictionary<string, bool>(detectedCache);
            string json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(cacheFilePath, json);
        }
        catch { }
    }

    /// <summary>
    /// Scanne TOUS les disques, launchers (Steam multi-bibliothèques, Epic, Riot, Ubisoft, EA, Roblox, Xbox) et le Registre Windows.
    /// </summary>
    public async Task<int> BoostIntelligenceAsync()
    {
        // ── Phase Locale (synchrone dans un thread pool) ──────────────
        HashSet<string> foundGameNames = new(StringComparer.OrdinalIgnoreCase);

        await Task.Run(() =>
        {
            OnLog?.Invoke("[IA] Lancement de l'Analyse Approfondie (Tous disques, Steam, Epic, Riot, Registre, Roblox)...");

            var allDrives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable))
                .Select(d => d.RootDirectory.FullName)
                .ToList();

            // 1. Scan Steam multi-bibliothèques (parcours de libraryfolders.vdf et de tous les disques)
            ScanSteamLibraries(allDrives, foundGameNames);

            // 2. Scan Epic Games Manifests et dossiers de jeux
            ScanEpicGames(allDrives, foundGameNames);

            // 3. Scan Riot Games (VALORANT, LoL, etc.) sur tous les disques
            ScanRiotGames(allDrives, foundGameNames);

            // 4. Scan Roblox & LocalAppData
            ScanRobloxAndAppData(foundGameNames);

            // 5. Scan Ubisoft, EA, Battle.net, GOG, Xbox Games sur tous les disques
            ScanOtherLaunchers(allDrives, foundGameNames);

            // 6. Scan Approfondi du Registre Windows (Uninstall keys HKLM & HKCU)
            ScanWindowsRegistry(foundGameNames);

            // 7. Scan des Raccourcis Bureau et Menu Démarrer (.lnk / .url)
            ScanDesktopAndStartMenu(foundGameNames);

            SaveCache();

            int countLocal = foundGameNames.Count;
            string previewLocal = string.Join(", ", foundGameNames.Take(6));
            if (countLocal > 6) previewLocal += $"... (+{countLocal - 6} autres)";
            OnLog?.Invoke($"[SCAN] Scan local terminé : {countLocal} jeux trouvés ({previewLocal})");
        });

        // ── Phase Internet (async – SteamSpy + Steam Store + RAWG) ────
        OnLog?.Invoke("[INTERNET] Enrichissement IA via SteamSpy, Steam Store & RAWG...");
        int internetAdded = 0;
        try
        {
            internetAdded = await InternetIntel.FullEnrichmentAsync(this, foundGameNames).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"[ATTENTION] [Internet] Enrichissement internet échoué : {ex.Message}");
        }

        SaveCache();

        int total = KnownGames.Count;
        OnLog?.Invoke($"[IA] Intelligence IA Boostée : {total} jeux connus au total (dont {internetAdded} via internet) !");
        OnGamesUpdated?.Invoke(total);

        return total;
    }

    private void ScanSteamLibraries(List<string> drives, HashSet<string> foundGames)
    {
        try
        {
            HashSet<string> steamLibraryPaths = new(StringComparer.OrdinalIgnoreCase);

            // Découverte du chemin principal Steam
            string defaultSteam = @"C:\Program Files (x86)\Steam";
            if (Directory.Exists(defaultSteam)) steamLibraryPaths.Add(defaultSteam);

            foreach (var drive in drives)
            {
                string candidate = Path.Combine(drive, "Steam");
                if (Directory.Exists(candidate)) steamLibraryPaths.Add(candidate);
                string candidateLib = Path.Combine(drive, "SteamLibrary");
                if (Directory.Exists(candidateLib)) steamLibraryPaths.Add(candidateLib);
            }

            // Lecture de libraryfolders.vdf pour découvrir TOUTES les bibliothèques sur les SSDs
            string vdfPath = Path.Combine(defaultSteam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdfPath))
            {
                try
                {
                    string vdfText = File.ReadAllText(vdfPath);
                    var matches = Regex.Matches(vdfText, "\"path\"\\s+\"([^\"]+)\"");
                    foreach (Match m in matches)
                    {
                        if (m.Groups.Count > 1)
                        {
                            string rawPath = m.Groups[1].Value.Replace(@"\\", @"\");
                            if (Directory.Exists(rawPath)) steamLibraryPaths.Add(rawPath);
                        }
                    }
                }
                catch { }
            }

            // Pour chaque bibliothèque Steam trouvée
            foreach (var libPath in steamLibraryPaths)
            {
                string commonDir = Path.Combine(libPath, "steamapps", "common");
                if (!Directory.Exists(commonDir)) continue;

                foreach (var gameDir in Directory.EnumerateDirectories(commonDir))
                {
                    string folderName = Path.GetFileName(gameDir);
                    if (folderName.Equals("Steamworks Shared", StringComparison.OrdinalIgnoreCase)) continue;

                    RegisterGame(folderName);
                    foundGames.Add(folderName);

                    // Chercher les exécutables du jeu
                    try
                    {
                        foreach (var exe in Directory.EnumerateFiles(gameDir, "*.exe", SearchOption.TopDirectoryOnly))
                        {
                            string exeName = Path.GetFileNameWithoutExtension(exe);
                            if (IsLikelyGameExecutable(exeName))
                            {
                                RegisterGame(exeName);
                            }
                        }

                        // Rechercher aussi dans Binaries ou Bin64
                        string[] subDirs = { "bin", "bin64", "Binaries", "Game" };
                        foreach (var sub in subDirs)
                        {
                            string p = Path.Combine(gameDir, sub);
                            if (Directory.Exists(p))
                            {
                                foreach (var subExe in Directory.EnumerateFiles(p, "*.exe", SearchOption.AllDirectories))
                                {
                                    string exeName = Path.GetFileNameWithoutExtension(subExe);
                                    if (IsLikelyGameExecutable(exeName))
                                    {
                                        RegisterGame(exeName);
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    private void ScanEpicGames(List<string> drives, HashSet<string> foundGames)
    {
        try
        {
            string epicManifestsPath = @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests";
            if (Directory.Exists(epicManifestsPath))
            {
                foreach (var file in Directory.EnumerateFiles(epicManifestsPath, "*.item"))
                {
                    try
                    {
                        string content = File.ReadAllText(file);
                        using var doc = JsonDocument.Parse(content);
                        if (doc.RootElement.TryGetProperty("DisplayName", out var nameProp))
                        {
                            string disp = nameProp.GetString() ?? "";
                            if (!string.IsNullOrWhiteSpace(disp))
                            {
                                RegisterGame(disp);
                                foundGames.Add(disp);
                            }
                        }
                        if (doc.RootElement.TryGetProperty("LaunchExecutable", out var execProp))
                        {
                            string execName = Path.GetFileNameWithoutExtension(execProp.GetString() ?? "");
                            if (!string.IsNullOrWhiteSpace(execName))
                            {
                                RegisterGame(execName);
                                RegisterGame($"{execName}-Shipping");
                            }
                        }
                        if (doc.RootElement.TryGetProperty("InstallLocation", out var installProp))
                        {
                            string installDir = installProp.GetString() ?? "";
                            if (Directory.Exists(installDir))
                            {
                                ScanDirectoryForGameExes(installDir);
                            }
                        }
                    }
                    catch { }
                }
            }

            // Dossiers Epic Games sur tous les disques
            foreach (var drive in drives)
            {
                string epicDir = Path.Combine(drive, "Program Files", "Epic Games");
                if (Directory.Exists(epicDir))
                {
                    foreach (var dir in Directory.EnumerateDirectories(epicDir))
                    {
                        string gName = Path.GetFileName(dir);
                        RegisterGame(gName);
                        foundGames.Add(gName);
                        ScanDirectoryForGameExes(dir);
                    }
                }
            }
        }
        catch { }
    }

    private void ScanRiotGames(List<string> drives, HashSet<string> foundGames)
    {
        try
        {
            foreach (var drive in drives)
            {
                string riotPath = Path.Combine(drive, "Riot Games");
                if (Directory.Exists(riotPath))
                {
                    foreach (var dir in Directory.EnumerateDirectories(riotPath))
                    {
                        string dirName = Path.GetFileName(dir);
                        if (!dirName.Contains("Riot Client", StringComparison.OrdinalIgnoreCase) &&
                            !dirName.Contains("Install", StringComparison.OrdinalIgnoreCase))
                        {
                            RegisterGame(dirName);
                            foundGames.Add(dirName);
                            ScanDirectoryForGameExes(dir);
                        }
                    }
                }
            }
        }
        catch { }
    }

    private void ScanRobloxAndAppData(HashSet<string> foundGames)
    {
        try
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            // Roblox
            string robloxDir = Path.Combine(localAppData, "Roblox", "Versions");
            if (Directory.Exists(robloxDir))
            {
                foreach (var exe in Directory.EnumerateFiles(robloxDir, "RobloxPlayerBeta.exe", SearchOption.AllDirectories))
                {
                    RegisterGame("RobloxPlayerBeta");
                    RegisterGame("RobloxPlayer");
                    foundGames.Add("Roblox");
                    break;
                }
            }

            // Minecraft (.minecraft)
            string mcDir = Path.Combine(appData, ".minecraft");
            if (Directory.Exists(mcDir))
            {
                RegisterGame("javaw");
                RegisterGame("Minecraft.Windows");
                foundGames.Add("Minecraft");
            }
        }
        catch { }
    }

    private void ScanOtherLaunchers(List<string> drives, HashSet<string> foundGames)
    {
        try
        {
            foreach (var drive in drives)
            {
                // Xbox Games (GamePass)
                string xboxDir = Path.Combine(drive, "XboxGames");
                if (Directory.Exists(xboxDir))
                {
                    foreach (var dir in Directory.EnumerateDirectories(xboxDir))
                    {
                        string name = Path.GetFileName(dir);
                        RegisterGame(name);
                        foundGames.Add(name);
                        ScanDirectoryForGameExes(dir);
                    }
                }

                // Ubisoft
                string ubiDir = Path.Combine(drive, "Program Files (x86)", "Ubisoft", "Ubisoft Game Launcher", "games");
                if (Directory.Exists(ubiDir))
                {
                    foreach (var dir in Directory.EnumerateDirectories(ubiDir))
                    {
                        string name = Path.GetFileName(dir);
                        RegisterGame(name);
                        foundGames.Add(name);
                        ScanDirectoryForGameExes(dir);
                    }
                }

                // EA Games
                string eaDir = Path.Combine(drive, "Program Files", "EA Games");
                if (Directory.Exists(eaDir))
                {
                    foreach (var dir in Directory.EnumerateDirectories(eaDir))
                    {
                        string name = Path.GetFileName(dir);
                        RegisterGame(name);
                        foundGames.Add(name);
                        ScanDirectoryForGameExes(dir);
                    }
                }

                // GOG Games
                string gogDir = Path.Combine(drive, "GOG Games");
                if (Directory.Exists(gogDir))
                {
                    foreach (var dir in Directory.EnumerateDirectories(gogDir))
                    {
                        string name = Path.GetFileName(dir);
                        RegisterGame(name);
                        foundGames.Add(name);
                        ScanDirectoryForGameExes(dir);
                    }
                }
            }
        }
        catch { }
    }

    private void ScanWindowsRegistry(HashSet<string> foundGames)
    {
        string[] regRoots =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (var subKeyPath in regRoots)
        {
            ScanRegistryKey(Registry.LocalMachine, subKeyPath, foundGames);
            ScanRegistryKey(Registry.CurrentUser, subKeyPath, foundGames);
        }
    }

    private void ScanRegistryKey(RegistryKey rootKey, string subKeyPath, HashSet<string> foundGames)
    {
        try
        {
            using var key = rootKey.OpenSubKey(subKeyPath);
            if (key == null) return;

            foreach (var subName in key.GetSubKeyNames())
            {
                try
                {
                    using var subKey = key.OpenSubKey(subName);
                    if (subKey == null) continue;

                    string dispName = subKey.GetValue("DisplayName")?.ToString() ?? "";
                    string publisher = subKey.GetValue("Publisher")?.ToString() ?? "";
                    string installLoc = subKey.GetValue("InstallLocation")?.ToString() ?? "";

                    if (string.IsNullOrWhiteSpace(dispName)) continue;

                    if (IsGamingPublisherOrKeyword(dispName, publisher, installLoc))
                    {
                        RegisterGame(dispName);
                        foundGames.Add(dispName);

                        if (!string.IsNullOrWhiteSpace(installLoc) && Directory.Exists(installLoc))
                        {
                            ScanDirectoryForGameExes(installLoc);
                        }
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    private bool IsGamingPublisherOrKeyword(string name, string pub, string loc)
    {
        string combined = $"{name} {pub} {loc}".ToLower();

        string[] gamePublishers =
        {
            "riot games", "valve", "epic games", "electronic arts", "ubisoft", "blizzard", "rockstar",
            "activision", "bethesda", "cd projekt", "bandai namco", "capcom", "square enix", "sega",
            "2k games", "fromsoftware", "paradox interactive", "wargaming", "roblox", "mihoyo", "cognosphere"
        };

        if (gamePublishers.Any(p => combined.Contains(p))) return true;

        if (combined.Contains("aim lab") || combined.Contains("aim trainer") || combined.Contains("black ops") ||
            combined.Contains("call of duty") || combined.Contains("overwatch") || combined.Contains("valorant") ||
            combined.Contains("fortnite") || combined.Contains("roblox") || combined.Contains("minecraft"))
        {
            return true;
        }

        return false;
    }

    private void ScanDirectoryForGameExes(string dirPath)
    {
        try
        {
            var opt = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                ReturnSpecialDirectories = false
            };
            foreach (var exe in Directory.EnumerateFiles(dirPath, "*.exe", opt))
            {
                string exeName = Path.GetFileNameWithoutExtension(exe);
                if (IsLikelyGameExecutable(exeName))
                {
                    RegisterGame(exeName);
                }
            }
        }
        catch { }
    }

    private bool IsLikelyGameExecutable(string exeName)
    {
        if (string.IsNullOrWhiteSpace(exeName)) return false;
        if (IsExcluded(exeName)) return false;
        string lower = exeName.ToLowerInvariant();
        if (IsExcluded(lower)) return false;

        return true;
    }

    private void ScanDesktopAndStartMenu(HashSet<string> foundGames)
    {
        try
        {
            var shortcutFolders = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
            };

            foreach (var folder in shortcutFolders.Distinct())
            {
                if (!Directory.Exists(folder)) continue;

                foreach (var file in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (ext == ".lnk" || ext == ".url")
                    {
                        string title = Path.GetFileNameWithoutExtension(file).Trim();
                        if (string.IsNullOrWhiteSpace(title) || ExcludedNonGameProcesses.Contains(title)) continue;

                        if (MasterGameDatabase.Contains(title) || IsLikelyGameExecutable(title))
                        {
                            RegisterGame(title);
                            foundGames.Add(title);
                        }
                    }
                }
            }
        }
        catch { }
    }

    public bool RegisterGame(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName)) return false;
        gameName = gameName.Trim();
        if (IsExcluded(gameName)) return false;
        detectedCache[gameName] = true;
        return KnownGames.Add(gameName);
    }

    /// <summary>
    /// IA Dynamique d'analyse en temps réel (+300% d'intelligence) :
    /// 1. Filtre absolu anti faux-positifs (node, cmd, outils dev, navigateurs)
    /// 2. Correspondance Base Maîtresse (+300 jeux AAA/compétitifs)
    /// 3. Détection Fenêtre Active / Plein Écran au premier plan
    /// 4. Détection Moteurs Graphiques (DirectX 11/12, Vulkan, Unreal, Unity, DLSS)
    /// 5. Analyse PE Métadonnées & Répertoire de jeu
    /// </summary>
    public async Task<bool> IsGameAsync(Process process)
    {
        string name = process.ProcessName;

        // Filtre de sécurité absolu : ne jamais brider ou qualifier les apps bureautiques/système/dev
        if (IsExcluded(name))
        {
            return false;
        }

        // Les résultats positifs sont persistants; les négatifs sont temporaires
        // afin qu'une mise à jour des sources en ligne puisse corriger une erreur.
        if (detectedCache.TryGetValue(name, out bool isGame) && isGame &&
            (MasterGameDatabase.Contains(name) || KnownGames.Contains(name)))
        {
            return true;
        }

        if (negativeCheckCache.TryGetValue(name, out DateTime retryAfter))
        {
            if (retryAfter > DateTime.UtcNow) return false;
            negativeCheckCache.TryRemove(name, out _);
        }

        if (MasterGameDatabase.Contains(name) || KnownGames.Contains(name))
        {
            RegisterGame(name);
            SaveCache();
            return true;
        }

        IntPtr hWnd = IntPtr.Zero;
        string windowTitle = "";
        try
        {
            hWnd = process.MainWindowHandle;
            windowTitle = process.MainWindowTitle ?? "";
        }
        catch { }

        // Exceptions spéciales : Minecraft Java Edition (javaw.exe)
        if (name.Equals("javaw", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(windowTitle) && windowTitle.Contains("Minecraft", StringComparison.OrdinalIgnoreCase))
            {
                RegisterGame(name);
                SaveCache();
                return true;
            }
            return false;
        }

        // Sans fenêtre visible active avec un titre, les processus d'arrière-plan inconnus NE SONT PAS des jeux
        if (hWnd == IntPtr.Zero || string.IsNullOrWhiteSpace(windowTitle))
        {
            return false;
        }

        // Le plein écran seul ne suffit pas: un navigateur ou un lecteur vidéo
        // peut aussi occuper tout l'écran.
        bool isFullscreen = false;
        try
        {
            isFullscreen = NativeMethods.IsForegroundWindowFullscreen(process.Id);
        }
        catch { }

        // HEURISTIQUE 2 : Métadonnées PE & Chemin d'accès
        try
        {
            string? desc = process.MainModule?.FileVersionInfo?.FileDescription?.ToLower();
            string? prod = process.MainModule?.FileVersionInfo?.ProductName?.ToLower();
            string? company = process.MainModule?.FileVersionInfo?.CompanyName?.ToLower();
            string? path = process.MainModule?.FileName?.ToLower();

            // Mots-clés moteurs ou jeux
            string[] gameKeywords = { "video game", "unreal engine", "unity player", "godot engine", "riot games", "blizzard entertainment", "rockstar games", "electronic arts" };
            string combinedMeta = $"{desc} {prod} {company}";

            if (gameKeywords.Any(k => combinedMeta.Contains(k)))
            {
                RegisterGame(name);
                SaveCache();
                OnLog?.Invoke($"[IA] Détection Métadonnées Moteur : '{name}' identifié comme jeu.");
                return true;
            }

            // Dossiers typiques de jeux
            if (path != null && (path.Contains(@"\steamapps\common\") ||
                                 path.Contains(@"\riot games\") ||
                                 path.Contains(@"\epic games\") ||
                                 path.Contains(@"\xboxgames\") ||
                                 path.Contains(@"\roblox\")))
            {
                RegisterGame(name);
                SaveCache();
                OnLog?.Invoke($"[IA] Détection Dossier Jeux : '{name}' reconnu.");
                return true;
            }
        }
        catch { }

        // HEURISTIQUE 3 : Inspection Moteur 3D & SDKs Spécifiques aux Jeux (Steam, Unity, Unreal, Epic, DLSS)
        try
        {
            string[] pureGameSdks = {
                "unityplayer.dll", "steam_api.dll", "steam_api64.dll", "discord_game_sdk.dll",
                "eossdk-win64-shipping.dll", "nvngx_dlss.dll", "sl.interposer.dll", "galaxy64.dll", "galaxy.dll"
            };

            string[] genericGpuDlls = {
                "d3d11.dll", "d3d12.dll", "vulkan-1.dll"
            };

            bool hasGpuApi = false;
            string foundGpuDll = "";

            foreach (ProcessModule mod in process.Modules)
            {
                string mName = mod.ModuleName.ToLowerInvariant();
                if (pureGameSdks.Contains(mName))
                {
                    RegisterGame(name);
                    SaveCache();
                    OnLog?.Invoke($"[IA] Détection SDK Jeu ({mName}) : '{name}' validé comme jeu.");
                    return true;
                }
                if (genericGpuDlls.Contains(mName))
                {
                    hasGpuApi = true;
                    foundGpuDll = mName;
                }
            }

            // Pour les DLLs DirectX/Vulkan génériques, valider si la fenêtre est en plein écran ou grand affichage 3D
            if (hasGpuApi && isFullscreen)
            {
                RegisterGame(name);
                SaveCache();
                OnLog?.Invoke($"[IA] Détection Rendu 3D Plein Écran ({foundGpuDll}) : '{name}' validé comme jeu.");
                return true;
            }
        }
        catch { }

        // HEURISTIQUE 3.5 : Suffixes et motifs typiques de jeux vidéo compétitifs et modernes
        if (name.EndsWith("-Win64-Shipping", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith("_BE", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith("_EAC", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith("_Vulkan", StringComparison.OrdinalIgnoreCase))
        {
            RegisterGame(name);
            SaveCache();
            OnLog?.Invoke($"[IA] Détection Motif Exécutable ({name}) : Jeu reconnu.");
            return true;
        }

        // HEURISTIQUE 4 : Vérification en ligne (Steam Store + RAWG) en dernier recours
        // Uniquement si le processus a une fenêtre visible (évite les faux-positifs de services)
        try
        {
            bool foundOnline = await InternetIntel.IsGameOnlineAsync(name, this).ConfigureAwait(false);
            if (foundOnline)
            {
                SaveCache();
                return true;
            }
        }
        catch { }

        negativeCheckCache[name] = DateTime.UtcNow.Add(NegativeCheckCooldown);
        return false;
    }
}
