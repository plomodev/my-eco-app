using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace GameEcoBot;

internal sealed class HardwareMonitor
{
    private FileTime? _previousIdle;
    private FileTime? _previousKernel;
    private FileTime? _previousUser;

    public HardwareSnapshot Sample()
    {
        double? cpuUsage = null;
        if (GetSystemTimes(out var idle, out var kernel, out var user))
        {
            if (_previousIdle is { } oldIdle
                && _previousKernel is { } oldKernel
                && _previousUser is { } oldUser)
            {
                var idleDelta = Math.Max(0, idle.Value - oldIdle.Value);
                var totalDelta = Math.Max(
                    0,
                    (kernel.Value - oldKernel.Value) + (user.Value - oldUser.Value));
                if (totalDelta > 0)
                {
                    cpuUsage = Math.Clamp(100d * (1d - (double)idleDelta / totalDelta), 0d, 100d);
                }
            }

            _previousIdle = idle;
            _previousKernel = kernel;
            _previousUser = user;
        }
        else
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Impossible de lire l’activité du processeur.");
        }

        var memory = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref memory))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Impossible de lire la mémoire système.");
        }

        var used = memory.TotalPhysical - memory.AvailablePhysical;
        var ramUsage = memory.TotalPhysical > 0
            ? 100d * used / memory.TotalPhysical
            : 0d;

        return new HardwareSnapshot(
            cpuUsage,
            Math.Clamp(ramUsage, 0d, 100d),
            used,
            memory.TotalPhysical,
            ReadCpuName());
    }

    private static string ReadCpuName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return key?.GetValue("ProcessorNameString")?.ToString()?.Trim()
                ?? "Processeur Windows";
        }
        catch
        {
            return "Processeur Windows";
        }
    }

    private static ulong ReadFileTime(FileTime value) =>
        ((ulong)value.High << 32) | value.Low;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out FileTime idleTime,
        out FileTime kernelTime,
        out FileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;

        public readonly ulong Value => ReadFileTime(this);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }
}

internal sealed record HardwareSnapshot(
    double? CpuUsage,
    double RamUsage,
    ulong UsedMemory,
    ulong TotalMemory,
    string CpuName);
