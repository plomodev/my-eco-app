using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;

namespace GameEcoBot;

/// <summary>
/// Gestionnaire de services Windows haute précision pour le gaming.
/// Philosophie d'ingénierie système :
/// - Arrête temporairement les services inutiles au jeu (télémétrie, indexeur de disque, spooler, màj).
/// - Préserve à 100% les services indispensables (DirectX, Audio, Pilotes GPU, Anti-cheats, Steam, Xbox).
/// - Restaure fidèlement l'état initial des services à la désactivation ou à la fermeture de l'app.
/// </summary>
public static class GamingServiceManager
{
    // Services non indispensables au jeu vidéo qui consomment du CPU, des I/O disque ou de la bande passante
    private static readonly Dictionary<string, string> NonGamingServices = new(StringComparer.OrdinalIgnoreCase)
    {
        // Télémétrie & Envoi de diagnostics (CPU + Bande passante)
        { "DiagTrack",                                  "Télémétrie Microsoft Connected User" },
        { "dmwappushservice",                           "WAP Push Message Routing" },
        { "diagnosticshub.standardcollector.service",   "Collecteur de diagnostics Visual Studio / Windows" },

        // Indexation & Prélecture disque (I/O Disque + Pics CPU en plein jeu)
        { "WSearch",                                    "Windows Search (Indexeur de recherche)" },
        { "SysMain",                                    "SysMain / Superfetch (Mise en cache RAM agressive)" },

        // Mises à jour Windows en arrière-plan (Bande passante réseau + charge CPU)
        { "DoSvc",                                      "Optimisation de livraison (P2P Windows Update)" },
        { "UsoSvc",                                     "Update Orchestrator Service" },
        { "wuauserv",                                   "Service Windows Update" },

        // Rapports d'erreurs
        { "WerSvc",                                     "Rapport d'erreurs Windows (WER)" },
        { "wercplsupport",                              "Panneau de contrôle des rapports d'erreurs" },

        // Impression (aucun jeu n'a besoin d'imprimer)
        { "Spooler",                                    "Spouleur d'impression Windows" },
        { "PrintWorkflowUserSvc",                       "Workflow d'impression moderne" },

        // Télécopie & Médias obsolètes
        { "Fax",                                        "Service de télécopie Fax" },
        { "WMPNetworkSvc",                              "Partage réseau Lecteur Windows Media" },

        // Cartes hors-ligne & Géolocalisation
        { "MapsBroker",                                 "Gestionnaire de cartes téléchargées" },
        { "lfsvc",                                      "Service de géolocalisation" },

        // Bureau à distance
        { "TermService",                                "Services Bureau à distance (RDP)" },
        { "UmRdpService",                               "Redirection d'appareils RDP" },

        // Saisie tactile / Stylet (pour machines de bureau)
        { "TabletInputService",                         "Service clavier tactile & volet d'écriture" },

        // Suivi des liens réseau
        { "TrkWks",                                     "Client de suivi de lien distribué" }
    };

    // Services formellement SANCTUARISÉS (Interdiction absolue d'y toucher) :
    // - Audio : AudioSrv, AudioEndpointBuilder
    // - Pilotes & HID : HidServ, GameInputSvc, nvlddmkm
    // - Réseau essentiel : Dhcp, Dnscache, LanmanWorkstation, nsi
    // - Anti-cheats & Launchers : EasyAntiCheat, BattlEye, vgc, Steam Client Service
    // - Écosystème Xbox / Store : XblAuthManager, XblGameSave, XboxNetApiSvc

    private static readonly Dictionary<string, ServiceControllerStatus> _savedStates = new();
    private static readonly object _lock = new();
    private static bool _isGamingModeActive = false;

    public static bool IsGamingModeActive => _isGamingModeActive;

    /// <summary>
    /// Vérifie si l'application s'exécute avec les privilèges Administrateur.
    /// </summary>
    public static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Arrête les services non-gaming et sauvegarde leur état pour restauration.
    /// </summary>
    public static (int stopped, int alreadyStopped, int failed, string report) EnableGamingMode()
    {
        lock (_lock)
        {
            int stopped = 0, alreadyStopped = 0, failed = 0;
            var report = new StringBuilder();
            _savedStates.Clear();

            foreach (var (svcName, description) in NonGamingServices)
            {
                try
                {
                    using var svc = new ServiceController(svcName);
                    var status = svc.Status;
                    _savedStates[svcName] = status;

                    if (status == ServiceControllerStatus.Stopped ||
                        status == ServiceControllerStatus.StopPending)
                    {
                        alreadyStopped++;
                        report.AppendLine($"[INACTIF] {description} ({svcName})");
                        continue;
                    }

                    if (svc.CanStop)
                    {
                        svc.Stop();
                        try
                        {
                            svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(3));
                        }
                        catch { }
                        stopped++;
                        report.AppendLine($"[ARRÊTÉ] {description} ({svcName})");
                    }
                    else
                    {
                        failed++;
                        report.AppendLine($"[PROTÉGÉ] {description} ({svcName})");
                    }
                }
                catch (InvalidOperationException)
                {
                    // Le service n'est pas installé sur ce Windows, normal
                }
                catch (Exception ex)
                {
                    failed++;
                    report.AppendLine($"[ÉCHEC] {svcName}: {ex.Message}");
                }
            }

            _isGamingModeActive = true;
            return (stopped, alreadyStopped, failed, report.ToString());
        }
    }

    /// <summary>
    /// Restaure fidèlement tous les services qui étaient actifs avant le mode gaming.
    /// </summary>
    public static (int restored, int failed, string report) RestoreServices()
    {
        lock (_lock)
        {
            int restored = 0, failed = 0;
            var report = new StringBuilder();

            foreach (var (svcName, savedStatus) in _savedStates)
            {
                if (savedStatus != ServiceControllerStatus.Running &&
                    savedStatus != ServiceControllerStatus.StartPending)
                    continue;

                try
                {
                    using var svc = new ServiceController(svcName);
                    if (svc.Status == ServiceControllerStatus.Stopped)
                    {
                        svc.Start();
                        try
                        {
                            svc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(4));
                        }
                        catch { }
                        restored++;
                        report.AppendLine($"[RESTAURÉ] {svcName}");
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    report.AppendLine($"[ERREUR RESTAURATION] {svcName}: {ex.Message}");
                }
            }

            _savedStates.Clear();
            _isGamingModeActive = false;
            return (restored, failed, report.ToString());
        }
    }
}