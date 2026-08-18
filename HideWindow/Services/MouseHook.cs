using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace HideWindow.Services;

internal sealed class MouseHook : IDisposable
{
    private static IntPtr _hookId = IntPtr.Zero;
    private static WeakReference<MouseHook>? _instanceRef;
    private readonly Dispatcher _dispatcher;
    private bool _disposed;

    public event EventHandler<IntPtr>? TargetWindowClicked;

    public MouseHook()
    {
        _dispatcher = Application.Current?.Dispatcher
                      ?? throw new InvalidOperationException("MouseHook must be created on a WPF UI thread.");
    }

    public void Install()
    {
        if (_hookId != IntPtr.Zero) return;
        _instanceRef = new WeakReference<MouseHook>(this);
        _hookId = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _proc, Win32.HMODULE_self, 0);
        if (_hookId == IntPtr.Zero)
            throw new InvalidOperationException($"SetWindowsHookEx failed, error={Marshal.GetLastWin32Error()}");
    }

    public void Uninstall()
    {
        if (_hookId != IntPtr.Zero)
        {
            Win32.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private static readonly Win32.LowLevelMouseProc _proc = HookCallback;

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)Win32.WM_LBUTTONUP)
        {
            var data = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
            IntPtr hwnd = Win32.WindowFromPoint(data.Pt);
            if (hwnd != IntPtr.Zero)
            {
                IntPtr rootHwnd = Win32.GetAncestor(hwnd, Win32.GA_ROOT);
                if (rootHwnd != IntPtr.Zero)
                {
                    uint pid;
                    Win32.GetWindowThreadProcessId(rootHwnd, out pid);
                    if (Process.GetCurrentProcess().Id != pid)
                    {
                        if (_instanceRef != null && _instanceRef.TryGetTarget(out var instance))
                        {
                            var targetHwnd = rootHwnd;
                            instance._dispatcher.BeginInvoke(() =>
                            {
                                instance.TargetWindowClicked?.Invoke(null, targetHwnd);
                            });
                        }
                    }
                }
            }
        }
        return Win32.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Uninstall();
        GC.SuppressFinalize(this);
    }

    ~MouseHook() => Uninstall();
}
