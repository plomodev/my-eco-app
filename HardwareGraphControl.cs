using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GameEcoBot;

/// <summary>
/// Lightweight Windows performance graph used by the main dashboard.
/// GPU utilization is reported as unavailable because this project does
/// not include a GPU telemetry provider.
/// </summary>
public sealed class HardwareGraphControl : UserControl
{
    private const int SampleLimit = 48;
    private readonly Queue<float> cpuSamples = new();
    private readonly Queue<float> ramSamples = new();
    private readonly Queue<float> appSamples = new();
    private readonly System.Windows.Forms.Timer sampleTimer;

    private string mode = "CPU";
    private string title = "Systeme";
    private float? cpuUsage;
    private float ramUsage;
    private int appCount;

    private long previousIdle;
    private long previousKernel;
    private long previousUser;
    private bool hasPreviousCpuSample;

    public string Title
    {
        get => title;
        set
        {
            title = string.IsNullOrWhiteSpace(value) ? "Systeme" : value.Trim();
            Invalidate();
        }
    }

    public float? CpuUsage => cpuUsage;
    public float RamUsage => ramUsage;
    public float? GpuUsage => null;
    public event EventHandler? MetricsUpdated;
    public HardwareGraphControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        ForeColor = Color.FromArgb(230, 230, 230);

        sampleTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        sampleTimer.Tick += (_, _) => SampleMetrics();

        // Baseline pre-sampling
        float? initCpu = ReadCpuUsage();
        float initRam = Math.Clamp(NativeMethods.GetMemoryUsagePercentage(), 0f, 100f);
        int initApps = CountVisibleApplications();

        if (initCpu.HasValue)
        {
            cpuSamples.Enqueue(initCpu.Value);
            cpuSamples.Enqueue(initCpu.Value);
        }
        ramSamples.Enqueue(initRam);
        ramSamples.Enqueue(initRam);
        appSamples.Enqueue(initApps);
        appSamples.Enqueue(initApps);

        cpuUsage = initCpu;
        ramUsage = initRam;

        sampleTimer.Start();
    }

    public void SetMode(string selectedMode)
    {
        if (string.IsNullOrWhiteSpace(selectedMode))
            return;

        string normalized = selectedMode.Trim().ToUpperInvariant();
        if (normalized is not ("CPU" or "RAM" or "GPU" or "APPS"))
            return;

        mode = normalized;
        Invalidate();
    }

    private void SampleMetrics()
    {
        float? newCpu = ReadCpuUsage();
        if (newCpu.HasValue)
        {
            cpuUsage = newCpu.Value;
        }
        else if (!cpuUsage.HasValue && cpuSamples.Count > 0)
        {
            cpuUsage = cpuSamples.Last();
        }

        ramUsage = Math.Clamp(NativeMethods.GetMemoryUsagePercentage(), 0f, 100f);
        appCount = CountVisibleApplications();

        if (cpuUsage.HasValue)
            Enqueue(cpuSamples, cpuUsage.Value);
        Enqueue(ramSamples, ramUsage);
        Enqueue(appSamples, appCount);
        Invalidate();
        MetricsUpdated?.Invoke(this, EventArgs.Empty);
    }

    private static void Enqueue(Queue<float> samples, float value)
    {
        samples.Enqueue(value);
        while (samples.Count > SampleLimit)
            samples.Dequeue();
    }

    private static int CountVisibleApplications()
    {
        int count = 0;
        try
        {
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    if (process.MainWindowHandle != IntPtr.Zero)
                        count++;
                }
                catch
                {
                    // Some protected/system processes cannot expose a window.
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch
        {
            // Keep the dashboard responsive if the process list is unavailable.
        }

        return count;
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
            return null;

        double percentage = 100.0 * (totalDelta - idleDelta) / totalDelta;
        return (float)Math.Clamp(percentage, 0.0, 100.0);
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

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        Rectangle bounds = ClientRectangle;
        if (bounds.Width < 40 || bounds.Height < 50)
            return;

        using Font titleFont = AppFonts.GetBoldFont(8.5f);
        using Font valueFont = AppFonts.GetTitleFont(15f);
        using Font smallFont = AppFonts.GetBodyFont(8f);
        using Brush mutedBrush = new SolidBrush(Color.FromArgb(140, 140, 140));
        using Brush valueBrush = new SolidBrush(Color.FromArgb(240, 240, 240));

        string caption = mode switch
        {
            "CPU" => "UTILISATION CPU",
            "RAM" => "UTILISATION RAM",
            "GPU" => "UTILISATION GPU",
            "APPS" => "FENÊTRES D’APPLICATIONS",
            _ => "SYSTÈME"
        };
        graphics.DrawString(caption, titleFont, mutedBrush, 10, 8);
        graphics.DrawString(title, smallFont, mutedBrush, 10, 27);

        string metric = mode switch
        {
            "CPU" => cpuUsage.HasValue ? $"{cpuUsage.Value:0}%" : (cpuSamples.Count > 0 ? $"{cpuSamples.Last():0}%" : "0%"),
            "RAM" => $"{ramUsage:0}%",
            "GPU" => "N/D",
            "APPS" => appCount.ToString(),
            _ => "N/D"
        };
        SizeF metricSize = graphics.MeasureString(metric, valueFont);
        graphics.DrawString(metric, valueFont, valueBrush, bounds.Width - metricSize.Width - 12, 8);

        RectangleF chart = new(12, 58, Math.Max(10, bounds.Width - 24), Math.Max(10, bounds.Height - 70));
        using Pen gridPen = new(Color.FromArgb(42, 255, 255, 255), 1f);
        for (int row = 0; row <= 4; row++)
        {
            float y = chart.Top + chart.Height * row / 4f;
            graphics.DrawLine(gridPen, chart.Left, y, chart.Right, y);
        }

        for (int column = 0; column <= 6; column++)
        {
            float x = chart.Left + chart.Width * column / 6f;
            graphics.DrawLine(gridPen, x, chart.Top, x, chart.Bottom);
        }

        if (mode == "GPU")
        {
            const string unavailableMessage = "Telemetry GPU non configuree";
            SizeF messageSize = graphics.MeasureString(unavailableMessage, smallFont);
            graphics.DrawString(
                unavailableMessage,
                smallFont,
                mutedBrush,
                Math.Max(chart.Left + 8, (bounds.Width - messageSize.Width) / 2f),
                chart.Top + chart.Height / 2f);
            return;
        }

        Queue<float> selectedSamples = mode switch
        {
            "RAM" => ramSamples,
            "APPS" => appSamples,
            _ => cpuSamples
        };

        if (selectedSamples.Count < 2)
        {
            graphics.DrawString("Collecte des mesures...", smallFont, mutedBrush, chart.Left + 8, chart.Top + 8);
            return;
        }

        float[] values = selectedSamples.ToArray();
        float maximum = mode == "APPS"
            ? Math.Max(10f, (float)Math.Ceiling(values.Max() / 10f) * 10f)
            : 100f;

        PointF[] points = new PointF[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            float x = chart.Left + chart.Width * i / Math.Max(1, values.Length - 1);
            float normalized = Math.Clamp(values[i] / maximum, 0f, 1f);
            float y = chart.Bottom - normalized * chart.Height;
            points[i] = new PointF(x, y);
        }

        Color accent = mode switch
        {
            "RAM" => Color.FromArgb(168, 85, 247),
            "GPU" => Color.FromArgb(192, 132, 252),
            "APPS" => Color.FromArgb(109, 40, 217),
            _ => Color.FromArgb(124, 58, 237)
        };

        using GraphicsPath area = new();
        if (points.Length > 2)
        {
            area.AddCurve(points, 0.35f);
            area.AddLine(points[^1], new PointF(points[^1].X, chart.Bottom));
            area.AddLine(new PointF(points[^1].X, chart.Bottom), new PointF(points[0].X, chart.Bottom));
        }
        else
        {
            area.AddLines(points);
            area.AddLine(points[^1], new PointF(points[^1].X, chart.Bottom));
            area.AddLine(new PointF(points[^1].X, chart.Bottom), new PointF(points[0].X, chart.Bottom));
        }
        area.CloseFigure();
        using LinearGradientBrush fill = new(
            new RectangleF(chart.Left, chart.Top, chart.Width, chart.Height),
            Color.FromArgb(75, accent),
            Color.FromArgb(5, accent),
            LinearGradientMode.Vertical);
        graphics.FillPath(fill, area);

        using Pen line = new(accent, 2.2f)
        {
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        if (points.Length == 1)
        {
            graphics.DrawEllipse(line, points[0].X - 1, points[0].Y - 1, 2, 2);
        }
        else if (points.Length > 2)
        {
            graphics.DrawCurve(line, points, 0.35f);
        }
        else
        {
            graphics.DrawLines(line, points);
        }

        // Point lumineux actuel (style Task Manager fluide)
        PointF lastPoint = points[^1];
        using (SolidBrush dotGlow = new(Color.FromArgb(60, accent)))
            graphics.FillEllipse(dotGlow, lastPoint.X - 5, lastPoint.Y - 5, 10, 10);
        using (SolidBrush dotBrush = new(accent))
            graphics.FillEllipse(dotBrush, lastPoint.X - 3, lastPoint.Y - 3, 6, 6);
        using (Pen dotRing = new(Color.White, 1.2f))
            graphics.DrawEllipse(dotRing, lastPoint.X - 3, lastPoint.Y - 3, 6, 6);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            sampleTimer.Stop();
            sampleTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
