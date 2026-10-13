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
            OnLog?.Invoke("🌐 [Internet] Récupération Top 100 jeux Steam (SteamSpy All-Time)...");
            var names1 = await GetSteamSpyTopNamesAsync(SteamSpyTop).ConfigureAwait(false);
            foreach (var name in names1)
                if (intel.RegisterGame(name)) added++;

            OnLog?.Invoke($"🌐 [Internet] +{added} jeux All-Time intégrés. Récup jeux récents...");

            int added2 = 0;
            var names2 = await GetSteamSpyTopNamesAsync(SteamSpyRecent).ConfigureAwait(false);
            foreach (var name in names2)
                if (intel.RegisterGame(name)) added2++;

            OnLog?.Invoke($"🌐 [Internet] +{added2} jeux tendance (2 semaines) intégrés.");
            added += added2;
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"⚠️ [Internet] SteamSpy indisponible : {ex.Message}");
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

        try
        {
            // 1. Steam Store Search (rapide, fiable)
            isGame = await CheckSteamStoreAsync(searchName).ConfigureAwait(false);

            // 2. Si pas trouvé sur Steam, tenter RAWG
            if (!isGame)
                isGame = await CheckRawgAsync(searchName).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"⚠️ [Internet] Vérif en ligne de '{processName}' échouée : {ex.Message}");
        }

        // Mettre en cache le résultat
        await cacheSem.WaitAsync().ConfigureAwait(false);
        try { onlineCheckCache[processName] = isGame; }
        finally { cacheSem.Release(); }

        if (isGame)
        {
            intel.RegisterGame(processName);
            OnLog?.Invoke($"🌐 [Internet] '{processName}' → Identifié comme JEU via recherche en ligne !");
        }

        return isGame;
    }

    private async Task<bool> CheckSteamStoreAsync(string name)
    {
        try
        {
            string url = string.Format(SteamSearchUrl, Uri.EscapeDataString(name));
            string json = await http.GetStringAsync(url).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var items)) return false;
            foreach (var item in items.EnumerateArray())
            {
                string itemName = item.TryGetProperty("name", out var np) ? np.GetString() ?? "" : "";
                if (FuzzyMatch(name, itemName)) return true;
            }
        }
        catch { /* Steam peut être bloqué -> pas grave */ }
        return false;
    }

    private async Task<bool> CheckRawgAsync(string name)
    {
        try
        {
            string url = $"{RawgBase}/games?search={Uri.EscapeDataString(name)}&page_size=5";
            string json = await http.GetStringAsync(url).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("results", out var results)) return false;
            foreach (var game in results.EnumerateArray())
            {
                string gameName = game.TryGetProperty("name", out var np) ? np.GetString() ?? "" : "";
                if (FuzzyMatch(name, gameName)) return true;
            }
        }
        catch { /* RAWG peut échouer -> pas grave */ }
        return false;
    }

    /// <summary>
    /// Nettoie le nom de processus pour la recherche :
    /// enlève suffixes techniques, remplace underscore/tiret par espace.
    /// </summary>
    public static string CleanProcessName(string raw)
    {
        string[] suffixes =
        {
            "-Win64-Shipping", "-Win32-Shipping", "_Win64", "_Win32",
            "_x64", "_x86", "_BE", "_EAC", "_DX11", "_DX12", "_Vulkan",
            "-shipping", "-game", "-launcher", "_beta", "-beta"
        };
        string name = raw;
        foreach (var s in suffixes)
            if (name.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                name = name[..^s.Length];
        return name.Replace('-', ' ').Replace('_', ' ').Trim();
    }

    /// <summary>
    /// Correspondance floue : le nom cherché doit être contenu dans le résultat
    /// ou vice-versa (tolérance casse + mots).
    /// </summary>
    private static bool FuzzyMatch(string search, string result)
    {
        if (string.IsNullOrWhiteSpace(search) || string.IsNullOrWhiteSpace(result)) return false;
        string s = search.ToLowerInvariant().Trim();
        string r = result.ToLowerInvariant().Trim();
        if (r.Contains(s) || s.Contains(r)) return true;
        var wordsS = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var wordsR = r.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (wordsS.Length == 0) return false;
        int matches = wordsS.Count(w => w.Length > 2 && wordsR.Any(wr => wr.Contains(w)));
        return matches >= Math.Max(1, (int)(wordsS.Length * 0.6));
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
            OnLog?.Invoke($"🌐 [Internet] Vérification en ligne de {unknowns.Count} processus inconnus...");
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
                OnLog?.Invoke($"🌐 [Internet] {internetFound} nouveaux jeux identifiés en ligne !");
            total += internetFound;
        }

        return total;
    }
}
