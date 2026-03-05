using System.Runtime.InteropServices;

namespace Dictator;

/// <summary>
/// Registers a system-wide hotkey via Win32 RegisterHotKey and dispatches
/// WM_HOTKEY messages through a WndProc subclass.
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 0xD1C7; // "DICT"

    // Modifier flags for RegisterHotKey
    public const uint MOD_ALT     = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT   = 0x0004;
    public const uint MOD_WIN     = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const int GWLP_WNDPROC = -4;

    private readonly IntPtr _hwnd;
    private readonly WndProcDelegate _wndProcDelegate;
    private IntPtr _prevWndProc;
    private bool _registered;
    private bool _disposed;

    public event Action? HotkeyPressed;

    public GlobalHotkey(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _wndProcDelegate = WndProc;

        // Subclass the window to intercept WM_HOTKEY
        _prevWndProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC,
            Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
    }

    public bool Register(uint modifiers, uint vk)
    {
        Unregister();
        _registered = RegisterHotKey(_hwnd, HOTKEY_ID, modifiers | MOD_NOREPEAT, vk);
        return _registered;
    }

    public void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(_hwnd, HOTKEY_ID);
            _registered = false;
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_HOTKEY && wParam == (IntPtr)HOTKEY_ID)
        {
            HotkeyPressed?.Invoke();
            return IntPtr.Zero;
        }
        return CallWindowProc(_prevWndProc, hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unregister();
        // Restore original WndProc
        if (_prevWndProc != IntPtr.Zero)
            SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _prevWndProc);
    }
}
