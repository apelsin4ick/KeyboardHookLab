using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KeyboardHookLab.Interop;

public sealed class KeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr LowLevelKeyboardProc(
        int nCode, IntPtr wParam, IntPtr lParam);

    private readonly LowLevelKeyboardProc _callback;
    private IntPtr _hookHandle = IntPtr.Zero;
    private bool _disposed;

    public event Action<Keys>? KeyPressed;
    public bool IsInstalled => _hookHandle != IntPtr.Zero;

    public KeyboardHook()
    {
        _callback = HookCallback;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsInstalled)
            return;

        _hookHandle = SetWindowsHookEx(
            WH_KEYBOARD_LL, _callback, IntPtr.Zero, 0);

        if (_hookHandle == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error,
                "Не удалось установить клавиатурный hook.");
        }
    }

    public void Stop()
    {
        if (!IsInstalled)
            return;

        IntPtr handle = _hookHandle;

        if (!UnhookWindowsHookEx(handle))
        {
            int error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error,
                "Не удалось снять клавиатурный hook.");
        }

        _hookHandle = IntPtr.Zero;
    }

    private IntPtr HookCallback(
        int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 &&
            (wParam == (IntPtr)WM_KEYDOWN ||
             wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            try
            {
                var info =
                    Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                IntPtr foregroundWindow = GetForegroundWindow();

                if (foregroundWindow != IntPtr.Zero)
                {
                    GetWindowThreadProcessId(
                        foregroundWindow, out uint processId);

                    if (processId == (uint)Environment.ProcessId)
                        KeyPressed?.Invoke((Keys)info.vkCode);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка callback: {ex}");
            }
        }

        return CallNextHookEx(
            _hookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true,
        CallingConvention = CallingConvention.Winapi)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook, LowLevelKeyboardProc callback,
        IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true,
        CallingConvention = CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll",
        CallingConvention = CallingConvention.Winapi)]
    private static extern IntPtr CallNextHookEx(
        IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll",
        CallingConvention = CallingConvention.Winapi)]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll",
        CallingConvention = CallingConvention.Winapi)]
    private static extern uint GetWindowThreadProcessId(
        IntPtr hWnd, out uint processId);
}
