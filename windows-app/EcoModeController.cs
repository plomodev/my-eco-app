using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GameEcoBot;

internal sealed partial class EcoModeController
{
    private readonly string _restoreFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GameEcoBot",
        "previous-power-plan.txt");

    public bool IsEcoActive => File.Exists(_restoreFile);

    public string Enable()
    {
        if (IsEcoActive)
        {
            return "Le mode Éco est déjà activé.";
        }

        var previousPlan = GetActivePlan();
        RunPowerCfg("/setactive SCHEME_MIN");
        var ecoPlan = GetActivePlan();
        if (string.Equals(previousPlan, ecoPlan, StringComparison.OrdinalIgnoreCase))
        {
            return "Windows utilise déjà ce plan d’alimentation.";
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_restoreFile)!);
            File.WriteAllText(_restoreFile, previousPlan);
        }
        catch
        {
            try
            {
                RunPowerCfg($"/setactive {previousPlan}");
            }
            catch
            {
                // Keep the original storage error as the message shown to the user.
            }

            throw;
        }

        return "Mode Éco activé. Le plan d’alimentation précédent a été mémorisé.";
    }

    public string RestorePreviousPlan()
    {
        if (!File.Exists(_restoreFile))
        {
            return "Aucun plan précédent n’est enregistré.";
        }

        var previousPlan = File.ReadAllText(_restoreFile).Trim();
        if (!Guid.TryParse(previousPlan, out _))
        {
            throw new InvalidDataException("Le plan d’alimentation enregistré est invalide.");
        }

        RunPowerCfg($"/setactive {previousPlan}");
        File.Delete(_restoreFile);
        return "Le plan d’alimentation précédent a été restauré.";
    }

    private static string GetActivePlan()
    {
        var output = RunPowerCfg("/getactivescheme");
        var match = GuidRegex().Match(output);
        if (!match.Success)
        {
            throw new InvalidOperationException("Windows n’a pas indiqué le plan d’alimentation actif.");
        }

        return match.Value;
    }

    private static string RunPowerCfg(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "powercfg.exe",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("Impossible de démarrer powercfg.exe.");

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(10_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Windows n’a pas répondu à la demande de plan d’alimentation.");
        }

        if (process.ExitCode != 0)
        {
            var details = string.IsNullOrWhiteSpace(standardError)
                ? standardOutput.Trim()
                : standardError.Trim();
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(details)
                    ? "Windows a refusé de modifier le plan d’alimentation."
                    : details);
        }

        return standardOutput;
    }

    [GeneratedRegex("[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}", RegexOptions.CultureInvariant)]
    private static partial Regex GuidRegex();
}
