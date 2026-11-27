using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace GameEcoBot;

public enum SocdLayout
{
    Azerty, // Q / D (+ Z / S)
    Qwerty  // A / D (+ W / S)
}

/// <summary>
/// Service SOCD (Simultaneous Opposing Cardinal Directions / Snap Tap / Last Input Priority)
/// via un hook clavier WH_KEYBOARD_LL exécuté sur un thread STA dédié haute priorité.
/// </summary>
public sealed class SocdKeyboardService : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const uint WM_QUIT = 0x0012;
    private const uint LLKHF_INJECTED = 0x10;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private static readonly IntPtr MagicExtraInfo = new(0x534F4344); // "SOCD"

    private sealed class AxisState
    {
        public uint KeyA;
        public uint KeyB;
        public bool PhysADown;
        public bool PhysBDown;
        public int ActiveLogical; // 0 = aucun, 1 = KeyA, 2 = KeyB

        public void Reset()
        {
            PhysADown = false;
            PhysBDown = false;
            ActiveLogical = 0;
        }
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    private readonly object _sync = new();
    private readonly AxisState _horizontal = new() { KeyA = 0x51, KeyB = 0x44 }; // Q / D
    private readonly AxisState _vertical = new() { KeyA = 0x5A, KeyB = 0x53 };   // Z / S

    private volatile bool _isEnabled = false;
    private volatile bool _includeVertical = false;
    private SocdLayout _layout = SocdLayout.Azerty;

    private Thread? _hookThread;
    private uint _hookThreadId;
    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _hookProc;

    public bool IsEnabled => _isEnabled;
    public SocdLayout Layout => _layout;
    public bool IncludeVertical => _includeVertical;

    public void Configure(SocdLayout layout, bool includeVertical)
    {
        lock (_sync)
        {
            _layout = layout;
            _includeVertical = includeVertical;
            if (layout == SocdLayout.Azerty)
            {
                _horizontal.KeyA = 0x51; // Q
                _horizontal.KeyB = 0x44; // D
                _vertical.KeyA = 0x5A;   // Z
                _vertical.KeyB = 0x53;   // S
            }
            else
            {
                _horizontal.KeyA = 0x41; // A
                _horizontal.KeyB = 0x44; // D
                _vertical.KeyA = 0x57;   // W
                _vertical.KeyB = 0x53;   // S
            }
            _horizontal.Reset();
            _vertical.Reset();
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled == _isEnabled) return;
        if (enabled) Start();
        else Stop();
    }

    public void Start()
    {
        if (_isEnabled) return;
        lock (_sync)
        {
            _horizontal.Reset();
            _vertical.Reset();
        }
        _isEnabled = true;

        using var ready = new ManualResetEvent(false);
        _hookThread = new Thread(() =>
        {
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            _hookThreadId = GetCurrentThreadId();
            _hookProc = HookCallback;
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(null), 0);
            ready.Set();
            Application.Run();
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        })
        {
            IsBackground = true
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();
        ready.WaitOne(1500);
    }

    public void Stop()
    {
        _isEnabled = false;
        if (_hookThread != null)
        {
            if (_hookThreadId != 0)
            {
                PostThreadMessage(_hookThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            }
            if (_hookThread.IsAlive)
            {
                _hookThread.Join(500);
            }
            _hookThread = null;
            _hookThreadId = 0;
        }
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        lock (_sync)
        {
            _horizontal.Reset();
            _vertical.Reset();
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || !_isEnabled)
            return CallNextHookEx(_hookId, nCode, wParam, lParam);

        var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

        // Ne jamais retraiter les touches injectées par notre propre SendInput (zéro boucle infinie)
        if ((info.flags & LLKHF_INJECTED) != 0 || info.dwExtraInfo == MagicExtraInfo)
            return CallNextHookEx(_hookId, nCode, wParam, lParam);

        int msg = wParam.ToInt32();
        bool isDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
        bool isUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;
        if (!isDown && !isUp)
            return CallNextHookEx(_hookId, nCode, wParam, lParam);

        // Ne pas interférer avec les raccourcis système Ctrl / Alt / Win
        if (isDown && ((GetAsyncKeyState(0x11) & 0x8000) != 0 || (GetAsyncKeyState(0x12) & 0x8000) != 0 || (GetAsyncKeyState(0x5B) & 0x8000) != 0))
            return CallNextHookEx(_hookId, nCode, wParam, lParam);

        AxisState? axis = null;
        int which = 0;

        if (info.vkCode == _horizontal.KeyA) { axis = _horizontal; which = 1; }
        else if (info.vkCode == _horizontal.KeyB) { axis = _horizontal; which = 2; }
        else if (_includeVertical && info.vkCode == _vertical.KeyA) { axis = _vertical; which = 1; }
        else if (_includeVertical && info.vkCode == _vertical.KeyB) { axis = _vertical; which = 2; }

        if (axis == null)
            return CallNextHookEx(_hookId, nCode, wParam, lParam);

        bool swallow = ProcessAxisEvent(axis, which, isDown);
        return swallow ? (IntPtr)1 : CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static bool ProcessAxisEvent(AxisState axis, int which, bool isDown)
    {
        uint pressedVk = (which == 1) ? axis.KeyA : axis.KeyB;
        uint otherVk = (which == 1) ? axis.KeyB : axis.KeyA;
        bool wasThisDown = (which == 1) ? axis.PhysADown : axis.PhysBDown;
        bool isOtherDown = (which == 1) ? axis.PhysBDown : axis.PhysADown;
        int otherIdx = (which == 1) ? 2 : 1;

        if (isDown)
        {
            if (!wasThisDown)
            {
                if (which == 1) axis.PhysADown = true;
                else axis.PhysBDown = true;

                // Priorité au dernier appui : relâche l'autre touche si elle était active
                if (isOtherDown && axis.ActiveLogical == otherIdx)
                {
                    InjectKey(otherVk, false);
                }

                axis.ActiveLogical = which;
                return false; // Laisse passer ce KeyDown
            }
            else
            {
                // Auto-repeat clavier : bloque si l'autre touche a la priorité
                return axis.ActiveLogical != which;
            }
        }
        else
        {
            if (which == 1) axis.PhysADown = false;
            else axis.PhysBDown = false;

            if (axis.ActiveLogical == which)
            {
                if (isOtherDown)
                {
                    // Relâche proprement cette touche puis réactive l'autre touche encore maintenue
                    axis.ActiveLogical = otherIdx;
                    InjectReleaseThenPress(pressedVk, otherVk);
                    return true; // On bloque le KeyUp d'origine puisqu'on vient d'envoyer la séquence ordonnée
                }
                else
                {
                    axis.ActiveLogical = 0;
                    return false; // Laisse passer ce KeyUp
                }
            }
            else
            {
                // Cette touche était déjà coupée logiquement : avale le KeyUp redondant
                return true;
            }
        }
    }

    private static INPUT MakeKeyInput(uint vk, bool keyDown)
    {
        ushort scan = (ushort)MapVirtualKey(vk, 0);
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)vk,
                    wScan = scan,
                    dwFlags = keyDown ? 0u : KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = MagicExtraInfo
                }
            }
        };
    }

    private static void InjectKey(uint vk, bool keyDown)
    {
        var inputs = new[] { MakeKeyInput(vk, keyDown) };
        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    private static void InjectReleaseThenPress(uint releaseVk, uint pressVk)
    {
        var inputs = new[]
        {
            MakeKeyInput(releaseVk, false),
            MakeKeyInput(pressVk, true)
        };
        SendInput(2, inputs, Marshal.SizeOf<INPUT>());
    }

    public void Dispose()
    {
        Stop();
    }
}
