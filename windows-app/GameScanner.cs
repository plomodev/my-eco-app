using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace GameEcoBot;

internal sealed record GameInstallation(string Name, string Source, string InstallPath);

internal static partial class GameScanner
{
    public static List<GameInstallation> ScanLocal()
    {
        var games = new Dictionary<string, GameInstallation>(StringComparer.OrdinalIgnoreCase);

        foreach (var steamRoot in FindSteamRoots())
        {
            ScanSteamLibrary(steamRoot, games);
            var libraryFile = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFile))
            {
                continue;
            }

            try
            {
                var contents = File.ReadAllText(libraryFile);
                foreach (Match match in SteamPathRegex().Matches(contents))
                {
                    var path = match.Groups["path"].Value.Replace(@"\\", @"\");
                    if (Path.IsPathRooted(path))
                    {
                        ScanSteamLibrary(path, games);
                    }
                }
            }
            catch (IOException)
            {
                // An unavailable library should not prevent scanning other local libraries.
            }
            catch (UnauthorizedAccessException)
            {
                // Skip libraries the current Windows user cannot read.
            }
        }

        ScanEpic(games);
        return games.Values.OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static IEnumerable<string> FindSteamRoots()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var registryLocations = new[]
        {
            (RegistryHive.CurrentUser, @"Software\Valve\Steam", RegistryView.Default),
            (RegistryHive.LocalMachine, @"SOFTWARE\Valve\Steam", RegistryView.Registry64),
            (RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", RegistryView.Registry32),
        };

        foreach (var (hive, keyPath, view) in registryLocations)
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var key = root.OpenSubKey(keyPath);
                var path = key?.GetValue("SteamPath")?.ToString()
                    ?? key?.GetValue("InstallPath")?.ToString();
                AddIfSteamRootExists(paths, path);
            }
            catch
            {
                // Some Windows editions restrict or omit individual registry views.
            }
        }

        AddIfSteamRootExists(paths, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam"));
        AddIfSteamRootExists(paths, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Steam"));
        AddIfSteamRootExists(paths, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "Steam"));

        return paths;
    }

    private static void AddIfSteamRootExists(HashSet<string> paths, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path)
            && Directory.Exists(path)
            && Directory.Exists(Path.Combine(path, "steamapps")))
        {
            paths.Add(Path.GetFullPath(path));
        }
    }

    private static void ScanSteamLibrary(string libraryRoot, IDictionary<string, GameInstallation> games)
    {
        var manifests = Path.Combine(libraryRoot, "steamapps");
        if (!Directory.Exists(manifests))
        {
            return;
        }

        try
        {
            foreach (var manifestPath in Directory.EnumerateFiles(manifests, "appmanifest_*.acf"))
            {
                try
                {
                    var contents = File.ReadAllText(manifestPath);
                    var name = GetSteamValue(contents, "name");
                    var installDirectory = GetSteamValue(contents, "installdir");
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(installDirectory))
                    {
                        continue;
                    }

                    var fullPath = Path.Combine(manifests, "common", installDirectory);
                    if (Directory.Exists(fullPath))
                    {
                        AddGame(games, new GameInstallation(name, "Steam", Path.GetFullPath(fullPath)));
                    }
                }
                catch (IOException)
                {
                    // Ignore a manifest that is being updated or is unreadable.
                }
                catch (UnauthorizedAccessException)
                {
                    // Ignore a protected manifest and continue with the rest.
                }
            }
        }
        catch (IOException)
        {
            // The library may have been disconnected during the scan.
        }
        catch (UnauthorizedAccessException)
        {
            // Skip inaccessible libraries.
        }
    }

    private static string? GetSteamValue(string contents, string key)
    {
        var match = Regex.Match(
            contents,
            $"\"{Regex.Escape(key)}\"\\s+\"(?<value>[^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static void ScanEpic(IDictionary<string, GameInstallation> games)
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var manifestFolder = Path.Combine(
            programData,
            "Epic",
            "EpicGamesLauncher",
            "Data",
            "Manifests");
        if (!Directory.Exists(manifestFolder))
        {
            return;
        }

        try
        {
            foreach (var manifestPath in Directory.EnumerateFiles(manifestFolder, "*.item"))
            {
                try
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
                    var root = document.RootElement;
                    var installPath = ReadJsonString(root, "InstallLocation");
                    var name = ReadJsonString(root, "DisplayName")
                        ?? ReadJsonString(root, "AppName");
                    if (string.IsNullOrWhiteSpace(installPath)
                        || string.IsNullOrWhiteSpace(name)
                        || !Directory.Exists(installPath))
                    {
                        continue;
                    }

                    AddGame(games, new GameInstallation(name, "Epic Games", Path.GetFullPath(installPath)));
                }
                catch (JsonException)
                {
                    // Ignore damaged or incomplete launcher manifests.
                }
                catch (IOException)
                {
                    // Ignore files that change during the scan.
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip unreadable manifests.
                }
            }
        }
        catch (IOException)
        {
            // The manifest directory may change during the scan.
        }
        catch (UnauthorizedAccessException)
        {
            // The current Windows user may not be able to read every manifest.
        }
    }

    private static string? ReadJsonString(JsonElement root, string propertyName)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }

    private static void AddGame(IDictionary<string, GameInstallation> games, GameInstallation game)
    {
        var key = game.InstallPath.TrimEnd(Path.DirectorySeparatorChar);
        games.TryAdd(key, game);
    }

    [GeneratedRegex("\"path\"\\s+\"(?<path>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SteamPathRegex();
}
