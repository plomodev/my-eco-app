using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace GameEcoBot;

public static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_POWER_THROTTLING_STATE
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    public const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
    public const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;
    public const int ProcessPowerThrottling = 4;

    public const uint PROCESS_TERMINATE = 0x0001;
    public const uint PROCESS_SET_INFORMATION = 0x0200;
    public const uint PROCESS_SET_QUOTA = 0x0100;
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    public const uint IDLE_PRIORITY_CLASS = 0x00000040;
    public const uint BELOW_NORMAL_PRIORITY_CLASS = 0x00004000;
    public const uint NORMAL_PRIORITY_CLASS = 0x00000020;
    public const uint HIGH_PRIORITY_CLASS = 0x00000080;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    public static bool KillProcess(int processId)
    {
        try
        {
            IntPtr h = OpenProcess(PROCESS_TERMINATE, false, processId);
            if (h != IntPtr.Zero)
            {
                bool terminated = TerminateProcess(h, 1);
                CloseHandle(h);
                if (terminated) return true;
            }
        }
        catch { }

        try
        {
            using var proc = System.Diagnostics.Process.GetProcessById(processId);
            proc.Kill();
            return true;
        }
        catch { }

        return false;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetPriorityClass(IntPtr handle, uint priorityClass);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetProcessInformation(
        IntPtr hProcess,
        int ProcessInformationClass,
        ref PROCESS_POWER_THROTTLING_STATE ProcessInformation,
        uint ProcessInformationSize);

    [DllImport("psapi.dll")]
    public static extern int EmptyWorkingSet(IntPtr hwProc);

    // Mouse Hook Win32 APIs for Angle Snapping
    public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    public const int WH_MOUSE_LL = 14;
    public const int WM_MOUSEMOVE = 0x0200;
    public const uint LLMHF_INJECTED = 0x01;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    public const uint MOUSEEVENTF_MOVE = 0x0001;

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    // Global Hotkey (F8 to toggle Angle Snapping in-game)
    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public static bool ApplyEcoMode(int pid, bool enable)
    {
        IntPtr handle = OpenProcess(PROCESS_SET_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_SET_QUOTA, false, pid);
        if (handle == IntPtr.Zero) return false;

        try
        {
            var throttling = new PROCESS_POWER_THROTTLING_STATE
            {
                Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                StateMask = enable ? PROCESS_POWER_THROTTLING_EXECUTION_SPEED : 0
            };

            SetProcessInformation(handle, ProcessPowerThrottling, ref throttling, (uint)Marshal.SizeOf(throttling));
            SetPriorityClass(handle, enable ? IDLE_PRIORITY_CLASS : NORMAL_PRIORITY_CLASS);
            if (enable)
            {
                EmptyWorkingSet(handle);
            }
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    public static bool BoostGameProcess(int pid)
    {
        IntPtr handle = OpenProcess(PROCESS_SET_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero) return false;

        try
        {
            var throttling = new PROCESS_POWER_THROTTLING_STATE
            {
                Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                StateMask = 0
            };

            SetProcessInformation(handle, ProcessPowerThrottling, ref throttling, (uint)Marshal.SizeOf(throttling));
            SetPriorityClass(handle, HIGH_PRIORITY_CLASS);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(IntPtr hIcon);

    // ── Statut Mémoire RAM Win32 instantané (0% CPU, exact à l'octet près) ──
    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public static float GetMemoryUsagePercentage()
    {
        try
        {
            var mem = new MEMORYSTATUSEX();
            mem.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
            if (GlobalMemoryStatusEx(ref mem))
            {
                return (float)mem.dwMemoryLoad;
            }
        }
        catch { }
        return 0f;
    }

    public static bool TrimRam(int pid)
    {
        IntPtr handle = OpenProcess(PROCESS_SET_QUOTA | PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero) return false;
        try
        {
            return EmptyWorkingSet(handle) != 0;
        }
        catch { return false; }
        finally
        {
            CloseHandle(handle);
        }
    }

    // ── Commandes Média Windows (WM_APPCOMMAND) ─────────────────────────
    public const int WM_APPCOMMAND = 0x0319;
    public const int APPCOMMAND_MEDIA_PLAY_PAUSE    = 14;
    public const int APPCOMMAND_MEDIA_NEXTTRACK     = 11;
    public const int APPCOMMAND_MEDIA_PREVIOUSTRACK = 12;
    public const int APPCOMMAND_MEDIA_STOP          = 13;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    /// <summary>Envoie une commande média (Play/Pause, Next, Prev) via WM_APPCOMMAND.</summary>
    public static void SendMediaCommand(IntPtr hWnd, int command)
    {
        try
        {
            SendMessage(hWnd, WM_APPCOMMAND, hWnd, (IntPtr)(command << 16));
        }
        catch { }
    }

    // ── Optimisation Latence Timer Windows (1ms pour gaming fluide) ──
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    public static extern uint TimeBeginPeriod(uint uMilliseconds);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    public static extern uint TimeEndPeriod(uint uMilliseconds);

    private static bool _timerResolutionActive = false;

    public static void EnableHighPrecisionTimer(bool enable)
    {
        try
        {
            if (enable && !_timerResolutionActive)
            {
                TimeBeginPeriod(1);
                _timerResolutionActive = true;
            }
            else if (!enable && _timerResolutionActive)
            {
                TimeEndPeriod(1);
                _timerResolutionActive = false;
            }
        }
        catch { }
    }

    // ── Détection Plein Écran pour les Jeux ──
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

    public static bool IsForegroundWindowFullscreen(int pid)
    {
        try
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            GetWindowThreadProcessId(fg, out int fgPid);
            if (fgPid != pid) return false;
            if (!GetWindowRect(fg, out RECT rect)) return false;

            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            if (screen == null) return false;

            int w = rect.Right - rect.Left;
            int h = rect.Bottom - rect.Top;
            return w >= screen.Bounds.Width && h >= screen.Bounds.Height;
        }
        catch
        {
            return false;
        }
    }

    // ── Optimisation No Delay Souris (0ms Raw Input Response) ──
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SystemParametersInfoGetSpeed(uint uiAction, uint uiParam, ref int pvParam, uint fWinIni);

    public const uint SPI_GETMOUSE = 0x0003;
    public const uint SPI_SETMOUSE = 0x0004;
    public const uint SPIF_UPDATEINIFILE = 0x01;
    public const uint SPIF_SENDCHANGE = 0x02;

    public const uint SPI_GETMOUSESPEED = 0x0070;
    public const uint SPI_SETMOUSESPEED = 0x0071;

    private static int[]? _originalMouseParams = null;
    private static int _originalMouseSpeed = 10;
    private static bool _originalSpeedSaved = false;

    /// <summary>
    /// Active le mode Ultra Faible Latence (No Delay) pour la souris :
    /// 1. Résolution timer Windows à 1ms pour une réponse USB instantanée.
    /// 2. Neutralise l'accélération Windows (Enhanced Pointer Precision) pour un suivi 1:1 pur sans temporisation.
    /// 3. Définit la vitesse du curseur sur 10 (standard 6/11, ratio 1:1 sans interpolation).
    /// </summary>
    public static bool EnableMouseNoDelay(bool enable)
    {
        try
        {
            EnableHighPrecisionTimer(enable);

            IntPtr ptr = Marshal.AllocHGlobal(sizeof(int) * 3);
            try
            {
                if (enable)
                {
                    if (_originalMouseParams == null)
                    {
                        if (SystemParametersInfo(SPI_GETMOUSE, 0, ptr, 0))
                        {
                            _originalMouseParams = new int[3];
                            Marshal.Copy(ptr, _originalMouseParams, 0, 3);
                        }
                    }

                    if (!_originalSpeedSaved)
                    {
                        int currentSpeed = 10;
                        if (SystemParametersInfoGetSpeed(SPI_GETMOUSESPEED, 0, ref currentSpeed, 0))
                        {
                            _originalMouseSpeed = currentSpeed;
                            _originalSpeedSaved = true;
                        }
                    }

                    // 0 = désactive complètement les seuils et l'accélération Windows
                    int[] noDelayParams = new int[] { 0, 0, 0 };
                    Marshal.Copy(noDelayParams, 0, ptr, 3);
                    SystemParametersInfo(SPI_SETMOUSE, 0, ptr, SPIF_SENDCHANGE);

                    // Vitesse 1:1 standard Windows (10/20 = 6ème cran sur 11)
                    SystemParametersInfo(SPI_SETMOUSESPEED, 0, (IntPtr)10, SPIF_SENDCHANGE);
                }
                else
                {
                    if (_originalMouseParams != null)
                    {
                        Marshal.Copy(_originalMouseParams, 0, ptr, 3);
                        SystemParametersInfo(SPI_SETMOUSE, 0, ptr, SPIF_SENDCHANGE);
                    }

                    if (_originalSpeedSaved)
                    {
                        SystemParametersInfo(SPI_SETMOUSESPEED, 0, (IntPtr)_originalMouseSpeed, SPIF_SENDCHANGE);
                    }
                }
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        catch
        {
            return false;
        }
    }

    // ── Bascule Plan d'Alimentation Haute Performance / Performance Ultime pour le jeu ──
    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, out IntPtr ActivePolicyGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveScheme(IntPtr UserRootPowerKey, ref Guid SchemeGuid);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    private static Guid? _previousPowerScheme = null;
    private const string PowerPlanBackupPath = @"Software\GameEcoBot";
    private const string PowerPlanBackupValue = "OriginalPowerSchemeGuid";
    private static readonly Guid BalancedPowerGuid = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    private static readonly Guid HighPerformanceGuid = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    private static readonly Guid UltimatePerformanceGuid = new("e9a42b02-d5df-448d-aa00-03f14749eb61");

    private static Guid? LoadOriginalPowerScheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PowerPlanBackupPath, false);
        string? saved = key?.GetValue(PowerPlanBackupValue) as string;
        return Guid.TryParse(saved, out Guid scheme) ? scheme : null;
    }

    private static bool SaveOriginalPowerScheme(Guid scheme)
    {
        using var key = Registry.CurrentUser.CreateSubKey(PowerPlanBackupPath);
        if (key == null) return false;
        key.SetValue(PowerPlanBackupValue, scheme.ToString("D"), RegistryValueKind.String);
        return true;
    }

    private static void ClearOriginalPowerScheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PowerPlanBackupPath, true);
        key?.DeleteValue(PowerPlanBackupValue, false);
    }

    private static bool TryGetActivePowerScheme(out Guid scheme)
    {
        scheme = Guid.Empty;
        uint result = PowerGetActiveScheme(IntPtr.Zero, out IntPtr activeGuidPtr);
        if (result != 0 || activeGuidPtr == IntPtr.Zero)
        {
            if (activeGuidPtr != IntPtr.Zero) LocalFree(activeGuidPtr);
            return false;
        }
        try
        {
            scheme = Marshal.PtrToStructure<Guid>(activeGuidPtr);
            return true;
        }
        finally
        {
            LocalFree(activeGuidPtr);
        }
    }

    public static bool SetUltimateGamingPowerPlan(bool enable)
    {
        try
        {
            if (enable)
            {
                if (!_previousPowerScheme.HasValue)
                {
                    _previousPowerScheme = LoadOriginalPowerScheme();
                    if (!_previousPowerScheme.HasValue)
                    {
                        if (!TryGetActivePowerScheme(out Guid currentScheme))
                            return false;
                        _previousPowerScheme = currentScheme;
                        if (!SaveOriginalPowerScheme(currentScheme))
                        {
                            _previousPowerScheme = null;
                            return false;
                        }
                    }
                }

                Guid target = UltimatePerformanceGuid;
                if (PowerSetActiveScheme(IntPtr.Zero, ref target) != 0)
                {
                    target = HighPerformanceGuid;
                    if (PowerSetActiveScheme(IntPtr.Zero, ref target) != 0)
                        return false;
                }
                return true;
            }

            Guid? originalScheme = _previousPowerScheme ?? LoadOriginalPowerScheme();
            if (!originalScheme.HasValue) return true;
            Guid restore = originalScheme.Value;
            if (PowerSetActiveScheme(IntPtr.Zero, ref restore) != 0)
                return false;

            _previousPowerScheme = null;
            ClearOriginalPowerScheme();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool RestoreNormalPowerPlan()
    {
        try
        {
            if (_previousPowerScheme.HasValue || LoadOriginalPowerScheme().HasValue)
                return SetUltimateGamingPowerPlan(false);

            if (!TryGetActivePowerScheme(out Guid currentScheme))
                return false;
            if (currentScheme != UltimatePerformanceGuid && currentScheme != HighPerformanceGuid)
                return true;

            Guid balanced = BalancedPowerGuid;
            return PowerSetActiveScheme(IntPtr.Zero, ref balanced) == 0;
        }
        catch
        {
            return false;
        }
    }

    // ── Optimisation Réseau & Ping NoDelay (-15ms de latence TCP) ──

    /// <summary>
    /// Active l'optimisation réseau esport :
    /// 1. Désactive l'algorithme de Nagle (TCPNoDelay = 1, TcpAckFrequency = 1) sur les cartes réseau.
    /// 2. Désactive le bridage réseau multimédia Windows (NetworkThrottlingIndex = 0xFFFFFFFF).
    /// 3. Supprime la temporisation système réservée (SystemResponsiveness = 0).
    /// 4. Débloque 100% de la bande passante réservée QoS (NonBestEffortLimit = 0).
    /// </summary>
    public static (bool Success, string Message) EnableNetworkPingOptimization(bool enable)
    {
        try
        {
            int adapterCount = 0;

            // 1. Désactivation de l'algorithme de Nagle sur toutes les cartes réseau
            using var interfacesKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", true);
            if (interfacesKey != null)
            {
                foreach (var ifaceName in interfacesKey.GetSubKeyNames())
                {
                    try
                    {
                        using var ifaceKey = interfacesKey.OpenSubKey(ifaceName, true);
                        if (ifaceKey != null)
                        {
                            if (enable)
                            {
                                ifaceKey.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                                ifaceKey.SetValue("TCPNoDelay", 1, RegistryValueKind.DWord);
                                ifaceKey.SetValue("TcpDelAckTicks", 0, RegistryValueKind.DWord);
                                adapterCount++;
                            }
                            else
                            {
                                ifaceKey.DeleteValue("TcpAckFrequency", false);
                                ifaceKey.DeleteValue("TCPNoDelay", false);
                                ifaceKey.DeleteValue("TcpDelAckTicks", false);
                                adapterCount++;
                            }
                        }
                    }
                    catch { }
                }
            }

            // 2. Multimédia System Profile : Débrider la pile réseau
            using var profileKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", true);
            if (profileKey != null)
            {
                if (enable)
                {
                    profileKey.SetValue("NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord);
                    profileKey.SetValue("SystemResponsiveness", 0, RegistryValueKind.DWord);
                }
                else
                {
                    profileKey.SetValue("NetworkThrottlingIndex", 10, RegistryValueKind.DWord);
                    profileKey.SetValue("SystemResponsiveness", 20, RegistryValueKind.DWord);
                }
            }

            // 3. QoS Packet Scheduler
            try
            {
                using var pschedKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Psched");
                if (pschedKey != null)
                {
                    if (enable)
                    {
                        pschedKey.SetValue("NonBestEffortLimit", 0, RegistryValueKind.DWord);
                    }
                    else
                    {
                        pschedKey.DeleteValue("NonBestEffortLimit", false);
                    }
                }
            }
            catch { }

            if (enable)
            {
                return (true, $"⚡ Ping NoDelay ACTIVÉ sur {adapterCount} cartes réseau (Nagle OFF, ACK immédiat, QoS 100%).");
            }
            else
            {
                return (true, "ℹ️ Paramètres réseau Windows restaurés par défaut.");
            }
        }
        catch (UnauthorizedAccessException)
        {
            return (false, "⚠️ Droits Administrateur requis pour appliquer les tweaks réseau TCP. Lancez l'application en tant qu'administrateur !");
        }
        catch (Exception ex)
        {
            return (false, $"⚠️ Erreur réseau : {ex.Message}");
        }
    }

    public static bool IsNetworkPingOptimizationEnabled()
    {
        try
        {
            using var profileKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", false);
            if (profileKey != null)
            {
                object? val = profileKey.GetValue("NetworkThrottlingIndex");
                if (val is int intVal && intVal == -1) return true;
            }
        }
        catch { }
        return false;
    }
}

