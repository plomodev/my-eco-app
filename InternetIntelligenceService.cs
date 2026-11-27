using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GameEcoBot;

/// <summary>
/// Service d'intelligence internet : enrichit la base de jeux connue
/// via RAWG (~500k jeux), SteamSpy (top jeux populaires) et Steam Store.
/// Permet aussi de vérifier en ligne si un nom de processus inconnu est un jeu.
/// </summary>
public class InternetIntelligenceService
{
    // HttpClient partagé avec timeout court pour ne pas bloquer l'UI
    private static readonly HttpClient http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    static InternetIntelligenceService()
    {
        http.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent", "GameEcoBot/2.0 (Windows Gaming Optimizer)");
    }

    // Cache mémoire des résultats de vérification en ligne (nom -> est un jeu ?)
    private readonly Dictionary<string, bool> onlineCheckCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim cacheSem = new SemaphoreSlim(1, 1);

    public event Action<string>? OnLog;

    // ─────────────────────────────────────────────────────────────────
    // SteamSpy  (https://steamspy.com/api.php)
    //    - Top 100 jeux toutes périodes (all, 2weeks) - sans clé
    // ─────────────────────────────────────────────────────────────────
    private const string SteamSpyTop = "https://steamspy.com/api.php?request=top100forever";
    private const string SteamSpyRecent = "https://steamspy.com/api.php?request=top100in2weeks";

    // ─────────────────────────────────────────────────────────────────
    // Steam Store Search  (sans clé)
    //    - Cherche un app par nom -> retourne un appid + nom officiel
    // ─────────────────────────────────────────────────────────────────
    private const string SteamSearchUrl = "https://store.steampowered.com/api/storesearch/?term={0}&l=english&cc=FR";

    // ─────────────────────────────────────────────────────────────────
    // RAWG API  (https://rawg.io/apidocs)
    //    - ~500 000 jeux, gratuit, pas de clé requise pour usage light
    // ─────────────────────────────────────────────────────────────────
    private const string RawgBase = "https://api.rawg.io/api";

    /// <summary>
    /// Récupère la liste des top jeux Steam via SteamSpy et les enregistre.
    /// Retourne le nombre de jeux ajoutés.
    /// </summary>
    public async Task<int> FetchTopSteamGamesAsync(GameIntelligenceService intel)
    {
        int added = 0;
        try
        {
            OnLog?.Invoke("[INTERNET] Récupération Top 100 jeux Steam (SteamSpy All-Time)...");
            var names1 = await GetSteamSpyTopNamesAsync(SteamSpyTop).ConfigureAwait(false);
            foreach (var name in names1)
                if (intel.RegisterGame(name)) added++;

            OnLog?.Invoke($"[INTERNET] +{added} jeux All-Time intégrés. Récup jeux récents...");

            int added2 = 0;
            var names2 = await GetSteamSpyTopNamesAsync(SteamSpyRecent).ConfigureAwait(false);
            foreach (var name in names2)
                if (intel.RegisterGame(name)) added2++;

            OnLog?.Invoke($"[INTERNET] +{added2} jeux tendance (2 semaines) intégrés.");
            added += added2;
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"[ATTENTION] [Internet] SteamSpy indisponible : {ex.Message}");
        }
        return added;
    }

    private async Task<List<string>> GetSteamSpyTopNamesAsync(string url)
    {
        var result = new List<string>();
        string json = await http.GetStringAsync(url).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.TryGetProperty("name", out var nameProp))
            {
                string name = nameProp.GetString() ?? "";
                if (!string.IsNullOrWhiteSpace(name) && !GameIntelligenceService.IsExcluded(name))
                    result.Add(name);
            }
        }
        return result;
    }

    /// <summary>
    /// Cherche un processus inconnu sur Steam et RAWG pour vérifier
    /// si c'est un jeu vidéo. Retourne true si identifié comme jeu.
    /// </summary>
    public async Task<bool> IsGameOnlineAsync(string processName, GameIntelligenceService intel)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;
        if (GameIntelligenceService.IsExcluded(processName)) return false;

        // Vérifier le cache mémoire d'abord
        await cacheSem.WaitAsync().ConfigureAwait(false);
        try
        {
            if (onlineCheckCache.TryGetValue(processName, out bool cached))
                return cached;
        }
        finally { cacheSem.Release(); }

        bool isGame = false;
        string searchName = CleanProcessName(processName);
        bool hasReliableAnswer = false;

        try
        {
            // 1. Steam Store Search (rapide, fiable)
            bool? steamResult = await CheckSteamStoreAsync(searchName).ConfigureAwait(false);
            if (steamResult == true)
            {
                isGame = true;
                hasReliableAnswer = true;
            }

            // 2. Si pas trouvé sur Steam, tenter RAWG
            if (!isGame)
            {
                bool? rawgResult = await CheckRawgAsync(searchName).ConfigureAwait(false);
                if (rawgResult.HasValue)
                {
                    hasReliableAnswer = true;
                    isGame = rawgResult.Value;
                }
                if (steamResult.HasValue) hasReliableAnswer = true;
            }
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"[ATTENTION] [Internet] Vérif en ligne de '{processName}' échouée : {ex.Message}");
        }

        // Ne pas transformer une panne réseau/API en résultat négatif permanent.
        if (hasReliableAnswer)
        {
            await cacheSem.WaitAsync().ConfigureAwait(false);
            try { onlineCheckCache[processName] = isGame; }
            finally { cacheSem.Release(); }
        }

        if (isGame)
        {
            intel.RegisterGame(processName);
            OnLog?.Invoke($"[INTERNET] '{processName}' -> Identifié comme JEU via recherche en ligne !");
        }

        return isGame;
    }

    private async Task<bool?> CheckSteamStoreAsync(string name)
    {
        try
        {
            string url = string.Format(SteamSearchUrl, Uri.EscapeDataString(name));
            using var response = await http.GetAsync(url).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                return null;
            foreach (var item in items.EnumerateArray())
            {
                string itemName = item.TryGetProperty("name", out var np) ? np.GetString() ?? "" : "";
                if (FuzzyMatch(name, itemName)) return true;
            }
            return false;
        }
        catch (HttpRequestException) { return null; }
        catch (OperationCanceledException) { return null; }
        catch (JsonException) { return null; }
    }

    private async Task<bool?> CheckRawgAsync(string name)
    {
        try
        {
            string? apiKey = Environment.GetEnvironmentVariable("RAWG_API_KEY");
            string keyParameter = string.IsNullOrWhiteSpace(apiKey)
                ? ""
                : $"&key={Uri.EscapeDataString(apiKey)}";
            string url = $"{RawgBase}/games?search={Uri.EscapeDataString(name)}&page_size=5{keyParameter}";
            using var response = await http.GetAsync(url).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                return null;
            foreach (var game in results.EnumerateArray())
            {
                string gameName = game.TryGetProperty("name", out var np) ? np.GetString() ?? "" : "";
                if (FuzzyMatch(name, gameName)) return true;
            }
            return false;
        }
        catch (HttpRequestException) { return null; }
        catch (OperationCanceledException) { return null; }
        catch (JsonException) { return null; }
    }

    /// <summary>
    /// Nettoie le nom de processus pour la recherche :
    /// enlève suffixes techniques, remplace underscore/tiret par espace.
    /// </summary>
    public static string CleanProcessName(string raw)
    {
        string[] suffixes =
        {
            "_EAC_EOS", "-Win64-Shipping", "-Win32-Shipping", "_Win64-Shipping", "_Win32-Shipping",
            "_Win64", "_Win32", "_x64", "_x86", "_BE", "_EAC", "_EOS", "_DX11", "_DX12", "_Vulkan",
            "-shipping", "_shipping", "-game", "_game", "-launcher", "_launcher", "-client", "_client",
            "_beta", "-beta"
        };
        string name = raw.Trim();
        bool removedSuffix;
        do
        {
            removedSuffix = false;
            foreach (var suffix in suffixes)
            {
                if (!name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
                name = name[..^suffix.Length];
                removedSuffix = true;
                break;
            }
        } while (removedSuffix && name.Length > 0);

        return name.Replace('-', ' ').Replace('_', ' ').Trim();
    }

    /// <summary>
    /// Correspondance prudente : égalité normalisée ou petite faute de frappe.
    /// Les sous-chaînes seules ne suffisent pas à identifier un jeu.
    /// </summary>
    private static bool FuzzyMatch(string search, string result)
    {
        if (string.IsNullOrWhiteSpace(search) || string.IsNullOrWhiteSpace(result)) return false;
        string s = NormalizeForMatch(search);
        string r = NormalizeForMatch(result);
        if (s.Length < 4 || r.Length < 4) return false;

        string[] genericNames = { "game", "games", "client", "launcher", "online", "edition", "demo", "setup", "service", "helper" };
        if (genericNames.Contains(s, StringComparer.OrdinalIgnoreCase)) return false;
        if (string.Equals(s, r, StringComparison.Ordinal)) return true;

        int minLength = Math.Min(s.Length, r.Length);
        int allowedDistance = minLength >= 10 ? 2 : minLength >= 6 ? 1 : 0;
        return BoundedLevenshteinDistance(s, r, allowedDistance) <= allowedDistance;
    }

    private static string NormalizeForMatch(string value)
    {
        string cleaned = CleanProcessName(value);
        return new string(cleaned
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static int BoundedLevenshteinDistance(string left, string right, int maxDistance)
    {
        if (Math.Abs(left.Length - right.Length) > maxDistance) return maxDistance + 1;

        int[] previous = Enumerable.Range(0, right.Length + 1).ToArray();
        int[] current = new int[right.Length + 1];

        for (int i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            int rowMinimum = current[0];

            for (int j = 1; j <= right.Length; j++)
            {
                int substitutionCost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + substitutionCost);
                rowMinimum = Math.Min(rowMinimum, current[j]);
            }

            if (rowMinimum > maxDistance) return maxDistance + 1;
            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    /// <summary>
    /// Enrichissement complet : SteamSpy top games + validation en ligne
    /// pour les processus inconnus passés en paramètre.
    /// Retourne le total de jeux nouvellement ajoutés.
    /// </summary>
    public async Task<int> FullEnrichmentAsync(GameIntelligenceService intel, IEnumerable<string> unknownProcesses)
    {
        int total = 0;

        // Phase 1 : SteamSpy Top 100 (très rapide, ~1-2s)
        total += await FetchTopSteamGamesAsync(intel).ConfigureAwait(false);

        // Phase 2 : vérification en ligne des processus inconnus (parallèle, max 5)
        var unknowns = unknownProcesses
            .Where(p => !GameIntelligenceService.IsExcluded(p) && !intel.KnownGames.Contains(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unknowns.Count > 0)
        {
            OnLog?.Invoke($"[INTERNET] Vérification en ligne de {unknowns.Count} processus inconnus...");
            var semSlots = new SemaphoreSlim(5);
            int internetFound = 0;
            var tasks = unknowns.Select(async proc =>
            {
                await semSlots.WaitAsync().ConfigureAwait(false);
                try
                {
                    bool found = await IsGameOnlineAsync(proc, intel).ConfigureAwait(false);
                    if (found) Interlocked.Increment(ref internetFound);
                }
                finally { semSlots.Release(); }
            });
            await Task.WhenAll(tasks).ConfigureAwait(false);
            if (internetFound > 0)
                OnLog?.Invoke($"[INTERNET] {internetFound} nouveaux jeux identifiés en ligne !");
            total += internetFound;
        }

        return total;
    }
}
