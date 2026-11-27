using System.Drawing;

namespace GameEcoBot;

/// <summary>
/// Gestionnaire centralisé des polices — utilise Segoe UI Variable Text,
/// la police moderne Windows 11, nette à toutes les résolutions.
/// </summary>
public static class AppFonts
{
    // "Segoe UI Variable Text" = police Windows 11 variable, anti-alias parfait.
    // Fallback sur "Segoe UI" si on est sur Windows 10.
    private static readonly string BodyFamily    = ResolveFont("Segoe UI Variable Text", "Segoe UI");
    private static readonly string TitleFamily   = ResolveFont("Segoe UI Variable Display", "Segoe UI");
    private static readonly string DisplayFamily = ResolveFont("Segoe UI Variable Display", "Segoe UI");

    public static void Initialize() { /* polices système — rien à charger */ }

    public static Font GetFont(float size, FontStyle style = FontStyle.Regular)
        => new Font(BodyFamily, size, style, GraphicsUnit.Point);

    public static Font GetTitleFont(float size, FontStyle style = FontStyle.Bold)
        => new Font(TitleFamily, size, style, GraphicsUnit.Point);

    public static Font GetBrandFont(float size, FontStyle style = FontStyle.Bold)
        => new Font(DisplayFamily, size, style, GraphicsUnit.Point);

    public static Font GetBodyFont(float size, FontStyle style = FontStyle.Regular)
        => new Font(BodyFamily, size, style, GraphicsUnit.Point);

    public static Font GetBoldFont(float size)
        => new Font(BodyFamily, size, FontStyle.Bold, GraphicsUnit.Point);

    /// <summary>Returns the first font family that is installed on this system.</summary>
    private static string ResolveFont(params string[] candidates)
    {
        var installed = new System.Drawing.Text.InstalledFontCollection();
        var names = new HashSet<string>(
            installed.Families.Select(f => f.Name),
            StringComparer.OrdinalIgnoreCase);
        foreach (var name in candidates)
            if (names.Contains(name))
                return name;
        return candidates[^1]; // last as safe fallback
    }
}
