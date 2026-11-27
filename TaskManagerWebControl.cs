using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GameEcoBot;

/// <summary>
/// Contrôle Task Manager inspiré de Windows 11 Fluent,
/// affichant des courbes splines Catmull-Rom en JavaScript et la liste dynamique des processus.
/// Bascule de manière transparente sur le moniteur natif fluide si WebView2 est indisponible.
/// </summary>
public sealed class TaskManagerWebControl : UserControl
{
    private WebView2? webView;
    private readonly HardwareGraphControl fallbackGraph;
    private readonly System.Windows.Forms.Timer telemetryTimer;
    private bool isWebViewReady = false;
    private bool isInitializing = false;

    // CPU sampling state
    private long previousIdle;
    private long previousKernel;
    private long previousUser;
    private bool hasPreviousCpuSample;

    // Cached metrics
    private float? currentCpu;
    private float currentRam;
    private readonly HashSet<int> throttledPids = new();
    private readonly Dictionary<int, (TimeSpan totalProcessorTime, DateTime sampleTime)> processCpuTracker = new();

    public float? CpuUsage => currentCpu;
    public float RamUsage => currentRam;
    public float? GpuUsage => null;

    public void SyncThrottledPids(IEnumerable<int> pids)
    {
        throttledPids.Clear();
        foreach (int pid in pids)
            throttledPids.Add(pid);
        SampleAndBroadcastMetrics();
    }

    public void SetMode(string mode)
    {
        fallbackGraph.SetMode(mode);
        if (isWebViewReady && webView?.CoreWebView2 != null)
        {
            string jsRes = mode.ToUpperInvariant() switch
            {
                "RAM" => "RAM",
                "GPU" => "GPU",
                "APPS" => "CPU",
                _ => "CPU"
            };
            _ = webView.CoreWebView2.ExecuteScriptAsync($"selectResource('{jsRes}')");
        }
    }

    public event EventHandler<string>? OnLogMessage;
    public event EventHandler? OnRamCleanRequested;
    public event EventHandler? OnEcoToggleRequested;
    public event EventHandler? MetricsUpdated;

    public TaskManagerWebControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        BackColor = Color.FromArgb(10, 10, 10);
        ForeColor = Color.FromArgb(245, 245, 245);

        // Fallback graphique natif prêt immédiatement
        fallbackGraph = new HardwareGraphControl
        {
            Dock = DockStyle.Fill,
            Title = "Moniteur de performances systeme"
        };
        Controls.Add(fallbackGraph);

        // Pre-warm CPU sampler so first real call produces a valid delta
        ReadCpuUsage();
        currentRam = Math.Clamp(NativeMethods.GetMemoryUsagePercentage(), 0f, 100f);

        telemetryTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        telemetryTimer.Tick += (_, _) => SampleAndBroadcastMetrics();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!isWebViewReady && !isInitializing)
        {
            _ = InitializeWebViewAsync();
        }
    }

    private async Task InitializeWebViewAsync()
    {
        if (isInitializing || isWebViewReady) return;
        isInitializing = true;

        try
        {
            webView = new WebView2
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(10, 10, 10)
            };

            Controls.Add(webView);

            // Dossier de profil dédié pour WebView2
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GameEcoBot",
                "WebView2_Profile");
            Directory.CreateDirectory(userDataFolder);

            CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await webView.EnsureCoreWebView2Async(env);

            webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            webView.CoreWebView2.Settings.IsBuiltInErrorPageEnabled = false;

            webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;
            webView.CoreWebView2.ProcessFailed += (_, _) => ShowFallback("Processus WebView2 arrêté.");

            webView.NavigationCompleted += (s, e) =>
            {
                if (e.IsSuccess)
                {
                    isWebViewReady = true;
                    webView.BringToFront();
                    fallbackGraph.Visible = false;
                    SampleAndBroadcastMetrics();
                }
                else
                {
                    ShowFallback("Chargement HTML interrompu.");
                }
            };

            webView.NavigateToString(TaskManagerHtml.GetHtml());

            telemetryTimer.Start();
            SampleAndBroadcastMetrics();
        }
        catch (Exception ex)
        {
            ShowFallback(ex.Message);
            telemetryTimer.Start();
        }
        finally
        {
            isInitializing = false;
        }
    }

    private void ShowFallback(string? reason = null)
    {
        try
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ShowFallback(reason)));
                return;
            }

            fallbackGraph.Visible = true;
            fallbackGraph.BringToFront();
            if (webView != null)
            {
                webView.Visible = false;
            }

            if (!string.IsNullOrEmpty(reason))
            {
                OnLogMessage?.Invoke(this, $"[INFO] Moniteur actif (Moteur natif) : {reason}");
            }
        }
        catch { }
    }

    private void WebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            string rawJson = e.WebMessageAsJson;
            using JsonDocument doc = JsonDocument.Parse(rawJson);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("action", out JsonElement actionProp))
                return;

            string action = actionProp.GetString() ?? "";

            switch (action)
            {
                case "killProcess":
                    if (root.TryGetProperty("pid", out JsonElement pidProp) && pidProp.TryGetInt32(out int pidToKill))
                    {
                        string procName = root.TryGetProperty("name", out JsonElement nameProp) ? nameProp.GetString() ?? $"PID {pidToKill}" : $"PID {pidToKill}";
                        bool killed = NativeMethods.KillProcess(pidToKill);
                        if (killed)
                        {
                            OnLogMessage?.Invoke(this, $"[FIN] Fin de tâche réussie : {procName} (PID {pidToKill}) terminé.");
                        }
                        else
                        {
                            OnLogMessage?.Invoke(this, $"[ATTENTION] Impossible de terminer {procName} (PID {pidToKill}) - Privilèges insuffisants.");
                        }
                        SampleAndBroadcastMetrics();
                    }
                    break;

                case "toggleEco":
                    if (root.TryGetProperty("pid", out JsonElement ecoPidProp) && ecoPidProp.TryGetInt32(out int ecoPid))
                    {
                        bool isCurrentlyEco = throttledPids.Contains(ecoPid);
                        if (isCurrentlyEco)
                        {
                            throttledPids.Remove(ecoPid);
                            NativeMethods.ApplyEcoMode(ecoPid, false);
                            OnLogMessage?.Invoke(this, $"[INFO] Mode Eco desactive pour PID {ecoPid}");
                        }
                        else
                        {
                            throttledPids.Add(ecoPid);
                            NativeMethods.ApplyEcoMode(ecoPid, true);
                            OnLogMessage?.Invoke(this, $"[ECO] Mode Eco applique pour PID {ecoPid}");
                        }
                        SampleAndBroadcastMetrics();
                    }
                    break;

                case "cleanRam":
                    OnRamCleanRequested?.Invoke(this, EventArgs.Empty);
                    break;

                case "toggleGlobalEco":
                    OnEcoToggleRequested?.Invoke(this, EventArgs.Empty);
                    break;

                case "ready":
                    SampleAndBroadcastMetrics();
                    break;
            }
        }
        catch (Exception ex)
        {
            OnLogMessage?.Invoke(this, $"[WARN] Message Task Manager non traite : {ex.Message}");
        }
    }

    private void SampleAndBroadcastMetrics()
    {
        float? sampledCpu = ReadCpuUsage();
        if (sampledCpu.HasValue)
        {
            currentCpu = sampledCpu.Value;
        }
        currentRam = Math.Clamp(NativeMethods.GetMemoryUsagePercentage(), 0f, 100f);

        MetricsUpdated?.Invoke(this, EventArgs.Empty);

        if (!isWebViewReady || webView?.CoreWebView2 == null)
            return;

        try
        {
            var memInfo = GetDetailedMemory();
            int procCount = 0;
            int threadCount = 0;
            int handleCount = 0;

            var processList = new List<TaskManagerProcessInfo>();
            DateTime now = DateTime.UtcNow;

            try
            {
                Process[] processes = Process.GetProcesses();
                procCount = processes.Length;
                var activePids = new HashSet<int>();

                foreach (Process p in processes)
                {
                    try
                    {
                        int pid = p.Id;
                        activePids.Add(pid);
                        threadCount += p.Threads.Count;
                        handleCount += p.HandleCount;

                        long wsBytes = p.WorkingSet64;
                        double memMb = wsBytes / (1024.0 * 1024.0);

                        double procCpu = 0.0;
                        try
                        {
                            TimeSpan currentTotal = p.TotalProcessorTime;
                            if (processCpuTracker.TryGetValue(pid, out var prev))
                            {
                                double elapsedMs = (now - prev.sampleTime).TotalMilliseconds;
                                if (elapsedMs > 200)
                                {
                                    double cpuMs = (currentTotal - prev.totalProcessorTime).TotalMilliseconds;
                                    procCpu = (cpuMs / (elapsedMs * Environment.ProcessorCount)) * 100.0;
                                    procCpu = Math.Clamp(procCpu, 0.0, 100.0);
                                }
                            }
                            processCpuTracker[pid] = (currentTotal, now);
                        }
                        catch { }

                        if (p.MainWindowHandle != IntPtr.Zero || memMb > 35 || procCpu > 0.1)
                        {
                            processList.Add(new TaskManagerProcessInfo
                            {
                                pid = pid,
                                name = p.ProcessName,
                                memoryMb = Math.Round(memMb, 1),
                                cpuPercent = Math.Round(procCpu, 1),
                                isEco = throttledPids.Contains(pid)
                            });
                        }
                    }
                    catch
                    {
                        // Ignore access denied on protected processes
                    }
                    finally
                    {
                        p.Dispose();
                    }
                }

                if (processCpuTracker.Count > 350)
                {
                    var dead = processCpuTracker.Keys.Where(k => !activePids.Contains(k)).ToList();
                    foreach (var k in dead) processCpuTracker.Remove(k);
                }
            }
            catch { }

            var topProcesses = processList
                .OrderByDescending(x => x.cpuPercent)
                .ThenByDescending(x => x.memoryMb)
                .Take(50)
                .ToList();

            TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            string uptimeStr = $"{(int)uptime.TotalDays} j {uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";

            var payload = new
            {
                cpuUsage = currentCpu,
                cpuSpeedGhz = (double?)null,
                processCount = procCount,
                threadCount = threadCount,
                handleCount = handleCount,
                uptime = uptimeStr,
                ramUsage = currentRam,
                ramUsedGb = memInfo.UsedGb,
                ramAvailGb = memInfo.AvailGb,
                ramTotalGb = memInfo.TotalGb,
                ramCachedGb = (double?)null,
                gpuUsage = (float?)null,
                diskUsage = (float?)null,
                netUsage = (float?)null,
                isGlobalEco = throttledPids.Count > 0,
                processes = topProcesses
            };

            string json = JsonSerializer.Serialize(payload);
            webView.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch
        {
            // Ignore temporary sampling error
        }
    }

    private (double TotalGb, double UsedGb, double AvailGb) GetDetailedMemory()
    {
        try
        {
            var mem = new NativeMethods.MEMORYSTATUSEX();
            mem.dwLength = (uint)Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>();
            if (NativeMethods.GlobalMemoryStatusEx(ref mem))
            {
                double total = mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                double avail = mem.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
                double used = Math.Max(0, total - avail);
                return (total, used, avail);
            }
        }
        catch { }

        return (16.0, 8.0, 8.0);
    }

    private float? ReadCpuUsage()
    {
        if (!GetSystemTimes(out NativeFileTime idleTime, out NativeFileTime kernelTime, out NativeFileTime userTime))
            return null;

        long idle = ToInt64(idleTime);
        long kernel = ToInt64(kernelTime);
        long user = ToInt64(userTime);

        if (!hasPreviousCpuSample)
        {
            previousIdle = idle;
            previousKernel = kernel;
            previousUser = user;
            hasPreviousCpuSample = true;
            return null;
        }

        long idleDelta = idle - previousIdle;
        long totalDelta = (kernel - previousKernel) + (user - previousUser);
        previousIdle = idle;
        previousKernel = kernel;
        previousUser = user;

        if (totalDelta <= 0)
            return currentCpu ?? 1.5f;

        double percentage = 100.0 * (totalDelta - idleDelta) / totalDelta;
        float val = (float)Math.Clamp(percentage, 0.0, 100.0);
        return val > 0.1f ? val : (currentCpu ?? 1.5f);
    }

    private static long ToInt64(NativeFileTime value) =>
        ((long)value.High << 32) | value.Low;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileTime
    {
        public uint Low;
        public uint High;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out NativeFileTime idleTime,
        out NativeFileTime kernelTime,
        out NativeFileTime userTime);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            telemetryTimer.Stop();
            telemetryTimer.Dispose();
            webView?.Dispose();
        }

        base.Dispose(disposing);
    }
}

public sealed class TaskManagerProcessInfo
{
    public int pid { get; set; }
    public string name { get; set; } = "";
    public double memoryMb { get; set; }
    public double cpuPercent { get; set; }
    public bool isEco { get; set; }
}
