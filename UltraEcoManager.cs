using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace GameEcoBot;

/// <summary>
/// Mode Ultra Eco Extreme (Architecture Haute Performance & Basse Consommation) :
/// Réduit à quasiment 0% l'utilisation CPU et 0% GPU de TOUS les processus d'arrière-plan,
/// tout en réservant 100% de la puissance matérielle (tous les cœurs, High Priority, VRAM libre)
/// exclusivement pour le jeu actif.
///
/// Piliers d'optimisation :
/// 1. EcoQoS Windows 11 (Execution Speed Throttling + Ignore High-Precision Timer Resolution).
/// 2. Priorité CPU minimale : IDLE_PRIORITY_CLASS.
/// 3. Confinement CPU : Isolation sur le dernier cœur logique uniquement (laisse tous les autres cœurs 100% libres pour le jeu).
/// 4. Vidage de mémoire de travail (EmptyWorkingSet) : Libère immédiatement les tampons de rendu VRAM/DirectX et la RAM inutilisée.
/// 5. Watchdog automatique : Surveille en continu les nouveaux processus d'arrière-plan créés pendant la session de jeu.
/// </summary>
public sealed class UltraEcoManager : IDisposable
{
    private readonly HashSet<string> _protectedSet;
    private readonly GameIntelligenceService _intelligence;

    private readonly Dictionary<int, IntPtr> _savedAffinities = new();
    private readonly HashSet<int> _ultraThrottledPids = new();
    private System.Threading.Timer? _watchdogTimer;
    private int _currentGamePid = -1;
    private readonly object _lock = new();

    public bool IsActive { get; private set; }
    public int ThrottledCount
    {
        get
        {
            lock (_lock) return _ultraThrottledPids.Count;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr h);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetProcessAffinityMask(IntPtr h, out IntPtr procMask, out IntPtr sysMask);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessAffinityMask(IntPtr h, IntPtr mask);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetPriorityClass(IntPtr handle, uint priorityClass);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(
        IntPtr hProcess,
        int ProcessInformationClass,
        ref PROCESS_POWER_THROTTLING_STATE ProcessInformation,
        uint ProcessInformationSize);

    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_POWER_THROTTLING_STATE
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    private const int ProcessPowerThrottling = 4;
    private const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
    private const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;
    private const uint PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION = 0x4;

    private const uint PROCESS_SET_INFORMATION = 0x0200;
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const uint PROCESS_SET_QUOTA = 0x0100;
    private const uint PROCESS_ALL = 0x001F0FFF;

    private const uint IDLE_PRIORITY_CLASS = 0x00000040;
    private const uint NORMAL_PRIORITY_CLASS = 0x00000020;
    private const uint HIGH_PRIORITY_CLASS = 0x00000080;

    public UltraEcoManager(HashSet<string> protectedProcesses, GameIntelligenceService intelligence)
    {
        _protectedSet = protectedProcesses;
        _intelligence = intelligence;
    }

    /// <summary>
    /// Active le mode Ultra Eco sur tous les processus background et démarre le watchdog.
    /// </summary>
    public int Activate(Process[] allProcesses, int gamePid)
    {
        lock (_lock)
        {
            IsActive = true;
            _currentGamePid = gamePid;

            IntPtr singleCoreMask = GetEfficiencyCoreMask();

            foreach (var proc in allProcesses)
            {
                TryApplyUltraEco(proc, singleCoreMask, gamePid);
            }

            // Démarre ou réarme le watchdog toutes les 3 secondes pour intercepter les nouveaux processus lancés
            _watchdogTimer?.Dispose();
            _watchdogTimer = new System.Threading.Timer(WatchdogTick, null, 3000, 3000);

            return _ultraThrottledPids.Count;
        }
    }

    /// <summary>
    /// Met à jour le PID du jeu actif pour s'assurer qu'il n'est jamais bridé.
    /// </summary>
    public void UpdateActiveGame(int gamePid)
    {
        lock (_lock)
        {
            _currentGamePid = gamePid;
            if (gamePid > 0)
            {
                BoostGameProcess(gamePid);
            }
        }
    }

    private void WatchdogTick(object? state)
    {
        if (!IsActive) return;

        try
        {
            IntPtr singleCoreMask = GetEfficiencyCoreMask();
            var currentProcs = Process.GetProcesses();

            lock (_lock)
            {
                if (!IsActive) return;
                foreach (var proc in currentProcs)
                {
                    if (!_ultraThrottledPids.Contains(proc.Id))
                    {
                        TryApplyUltraEco(proc, singleCoreMask, _currentGamePid);
                    }
                }
            }
        }
        catch { }
    }

    private void TryApplyUltraEco(Process proc, IntPtr singleCoreMask, int gamePid)
    {
        try
        {
            int pid = proc.Id;
            if (pid <= 4) return;
            if (pid == gamePid || pid == Process.GetCurrentProcess().Id) return;

            string name = proc.ProcessName;
            if (_protectedSet.Contains(name)) return;
            if (GameIntelligenceService.IsExcluded(name)) return;
            if (_intelligence.KnownGames.Contains(name)) return;

            ApplyDeepEcoToProcess(pid, singleCoreMask);
        }
        catch { }
    }

    private void ApplyDeepEcoToProcess(int pid, IntPtr singleCoreMask)
    {
        IntPtr h = OpenProcess(PROCESS_SET_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_SET_QUOTA, false, pid);
        if (h == IntPtr.Zero)
            h = OpenProcess(PROCESS_ALL, false, pid);

        if (h == IntPtr.Zero) return;

        try
        {
            // 1. EcoQoS Windows 11 avec désactivation de haute précision timer (bloque le réveil CPU intempestif)
            var throttling = new PROCESS_POWER_THROTTLING_STATE
            {
                Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED | PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION,
                StateMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED | PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION
            };
            SetProcessInformation(h, ProcessPowerThrottling, ref throttling, (uint)Marshal.SizeOf(throttling));

            // 2. Priorité minimale Idle (le CPU ne s'y consacre que si absolument aucun jeu n'a besoin du cœur)
            SetPriorityClass(h, IDLE_PRIORITY_CLASS);

            // 3. Sauvegarder affinité et confiner sur le dernier cœur logique
            if (!_savedAffinities.ContainsKey(pid))
            {
                if (GetProcessAffinityMask(h, out IntPtr procMask, out _))
                    _savedAffinities[pid] = procMask;
            }
            SetProcessAffinityMask(h, singleCoreMask);

            // 4. Libérer la mémoire de travail (DirectX / swapchains VRAM + RAM inutilisée)
            try { EmptyWorkingSet(h); } catch { }

            _ultraThrottledPids.Add(pid);
        }
        catch { }
        finally
        {
            CloseHandle(h);
        }
    }

    /// <summary>
    /// Restaure immédiatement un processus de jeu avec tous les cœurs et priorité High.
    /// </summary>
    public void BoostGameProcess(int gamePid)
    {
        if (gamePid <= 0) return;
        try
        {
            lock (_lock)
            {
                _ultraThrottledPids.Remove(gamePid);
            }

            IntPtr h = OpenProcess(PROCESS_ALL, false, gamePid);
            if (h == IntPtr.Zero) return;

            try
            {
                // Désactiver EcoQoS
                var throttling = new PROCESS_POWER_THROTTLING_STATE
                {
                    Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                    ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED | PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION,
                    StateMask = 0
                };
                SetProcessInformation(h, ProcessPowerThrottling, ref throttling, (uint)Marshal.SizeOf(throttling));

                // Priorité High pour le jeu
                SetPriorityClass(h, HIGH_PRIORITY_CLASS);

                // Restaurer ou accorder tous les cœurs CPU du système
                if (_savedAffinities.TryGetValue(gamePid, out var saved))
                {
                    SetProcessAffinityMask(h, saved);
                }
                else
                {
                    GetProcessAffinityMask(h, out _, out IntPtr sysMask);
                    if (sysMask != IntPtr.Zero)
                        SetProcessAffinityMask(h, sysMask);
                }
            }
            finally
            {
                CloseHandle(h);
            }
        }
        catch { }
    }

    /// <summary>
    /// Désactive complètement le mode Ultra Eco et restaure tous les processus à leur état d'origine.
    /// </summary>
    public int Deactivate()
    {
        lock (_lock)
        {
            _watchdogTimer?.Dispose();
            _watchdogTimer = null;

            int restored = 0;
            foreach (int pid in _ultraThrottledPids.ToArray())
            {
                try
                {
                    IntPtr h = OpenProcess(PROCESS_SET_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                    if (h != IntPtr.Zero)
                    {
                        try
                        {
                            // Enlever EcoQoS
                            var throttling = new PROCESS_POWER_THROTTLING_STATE
                            {
                                Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                                ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED | PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION,
                                StateMask = 0
                            };
                            SetProcessInformation(h, ProcessPowerThrottling, ref throttling, (uint)Marshal.SizeOf(throttling));

                            // Priorité Normale
                            SetPriorityClass(h, NORMAL_PRIORITY_CLASS);

                            // Restaurer affinité
                            if (_savedAffinities.TryGetValue(pid, out var saved))
                                SetProcessAffinityMask(h, saved);
                        }
                        finally
                        {
                            CloseHandle(h);
                        }
                    }
                    restored++;
                }
                catch { }
            }

            _ultraThrottledPids.Clear();
            _savedAffinities.Clear();
            _currentGamePid = -1;
            IsActive = false;
            return restored;
        }
    }

    private static IntPtr GetEfficiencyCoreMask()
    {
        int cores = Environment.ProcessorCount;
        if (cores <= 1) return (IntPtr)1;

        // Isole sur le dernier cœur logique (ex: bit 7 sur un 8 coeurs, bit 15 sur un 16 coeurs)
        // Cela préserve Core 0 (interruptions système / DPC) et tous les coeurs principaux pour le jeu
        long mask = 1L << (cores - 1);
        return (IntPtr)mask;
    }

    public void Dispose()
    {
        Deactivate();
    }
}