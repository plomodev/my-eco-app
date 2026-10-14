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
    private string title = "Système";
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
            title = string.IsNullOrWhiteSpace(value) ? "Système" : value.Trim();
            Invalidate();
        }
    }

    public float? CpuUsage => cpuUsage;
    public float RamUsage => ramUsage;
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
        ForeColor = Color.FromArgb(230, 220, 255);

        sampleTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        sampleTimer.Tick += (_, _) => SampleMetrics();
        SampleMetrics();
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
        cpuUsage = ReadCpuUsage();
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

        using Font titleFont = new("Segoe UI", 8.5f, FontStyle.Bold);
        using Font valueFont = new("Segoe UI", 15f, FontStyle.Bold);
        using Font smallFont = new("Segoe UI", 8f, FontStyle.Regular);
        using Brush mutedBrush = new SolidBrush(Color.FromArgb(155, 140, 190));
        using Brush valueBrush = new SolidBrush(Color.FromArgb(238, 230, 255));

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
            "CPU" => cpuUsage.HasValue ? $"{cpuUsage.Value:0}%" : "…",
            "RAM" => $"{ramUsage:0}%",
            "GPU" => "N/D",
            "APPS" => appCount.ToString(),
            _ => "N/D"
        };
        SizeF metricSize = graphics.MeasureString(metric, valueFont);
        graphics.DrawString(metric, valueFont, valueBrush, bounds.Width - metricSize.Width - 12, 8);

        RectangleF chart = new(12, 58, Math.Max(10, bounds.Width - 24), Math.Max(10, bounds.Height - 70));
        using Pen gridPen = new(Color.FromArgb(42, 124, 92, 175), 1f);
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
            const string unavailable = "La télémétrie GPU n’est pas configurée";
            SizeF textSize = graphics.MeasureString(unavailable, smallFont);
            graphics.DrawString(
                unavailable,
                smallFont,
                mutedBrush,
                chart.Left + Math.Max(0, (chart.Width - textSize.Width) / 2),
                chart.Top + Math.Max(0, (chart.Height - textSize.Height) / 2));
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
            graphics.DrawString("Collecte des mesures…", smallFont, mutedBrush, chart.Left + 8, chart.Top + 8);
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
            "RAM" => Color.FromArgb(192, 132, 252),
            "APPS" => Color.FromArgb(216, 180, 254),
            _ => Color.FromArgb(168, 85, 247)
        };

        using GraphicsPath area = new();
        area.AddLines(points);
        area.AddLine(points[^1].X, chart.Bottom, points[0].X, chart.Bottom);
        area.CloseFigure();
        using LinearGradientBrush fill = new(
            new RectangleF(chart.Left, chart.Top, chart.Width, chart.Height),
            Color.FromArgb(72, accent),
            Color.FromArgb(4, accent),
            LinearGradientMode.Vertical);
        graphics.FillPath(fill, area);

        using Pen line = new(accent, 2f);
        if (points.Length == 1)
            graphics.DrawEllipse(line, points[0].X - 1, points[0].Y - 1, 2, 2);
        else
            graphics.DrawLines(line, points);
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
