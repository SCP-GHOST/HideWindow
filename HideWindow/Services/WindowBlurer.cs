using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;

namespace HideWindow.Services;

public enum BlurState
{
    Idle,
    AwaitingClick,
    Blurring
}

internal sealed class WindowBlurer : IDisposable
{
    private sealed class TargetState
    {
        public IntPtr Hwnd;
        public int OriginalExStyle;
        public bool WasLayered;
        public int AlphaPercent = 50;
    }

    private readonly List<TargetState> _targets = new();
    private int _currentAlphaPercent = 50;
    private readonly DispatcherTimer _healthCheckTimer;
    private bool _disposed;

    public BlurState State { get; private set; } = BlurState.Idle;
    public int TargetCount => _targets.Count;
    public IReadOnlyList<IntPtr> TargetHandles => _targets.Select(t => t.Hwnd).ToList();

    public int CurrentAlphaPercent
    {
        get => _currentAlphaPercent;
        set
        {
            _currentAlphaPercent = Math.Clamp(value, 1, 100);
            if (State != BlurState.Blurring || _targets.Count == 0) return;

            // 全局滑块:同步应用到所有目标
            foreach (TargetState state in _targets)
            {
                state.AlphaPercent = _currentAlphaPercent;
                SetAlpha(state);
            }
        }
    }

    public event EventHandler<IntPtr>? WindowBlurred;
    public event EventHandler? WindowRestored;
    public event EventHandler<IntPtr>? TargetWindowGone;

    public WindowBlurer()
    {
        _healthCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _healthCheckTimer.Tick += HealthCheck;
        _healthCheckTimer.Start();
    }

    public void StartAwaiting() => State = BlurState.AwaitingClick;

    public void CancelAwaiting()
    {
        if (State == BlurState.AwaitingClick)
            State = BlurState.Idle;
    }

    public bool ApplyTarget(IntPtr hwnd) => ApplyTargets([(hwnd, _currentAlphaPercent)]);

    public bool ApplyTargets(IEnumerable<(IntPtr Hwnd, int AlphaPercent)> targets)
    {
        List<(IntPtr Hwnd, int Alpha)> list = targets
            .Where(t => t.Hwnd != IntPtr.Zero)
            .GroupBy(t => t.Hwnd)
            .Select(g => g.First())
            .ToList();
        if (list.Count == 0) return false;

        Restore();

        foreach ((IntPtr hwnd, int alpha) in list)
        {
            int exStyle = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
            var state = new TargetState
            {
                Hwnd = hwnd,
                OriginalExStyle = exStyle,
                WasLayered = (exStyle & Win32.WS_EX_LAYERED) != 0,
                AlphaPercent = Math.Clamp(alpha, 1, 100)
            };
            _targets.Add(state);

            if (!state.WasLayered)
                Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, exStyle | Win32.WS_EX_LAYERED);
            SetAlpha(state);
        }

        State = BlurState.Blurring;
        WindowBlurred?.Invoke(this, list[0].Hwnd);
        return true;
    }

    /// <summary>单独调整某个目标窗口的透明度(预选模式每行滑块使用)。</summary>
    public bool SetTargetAlpha(IntPtr hwnd, int alphaPercent)
    {
        TargetState? state = _targets.FirstOrDefault(t => t.Hwnd == hwnd);
        if (state == null) return false;

        state.AlphaPercent = Math.Clamp(alphaPercent, 1, 100);
        SetAlpha(state);
        return true;
    }

    /// <summary>对已有目标重新应用 layered 样式和各自透明度(切换模式时防止丢失)。</summary>
    public void ReapplyAlphas()
    {
        if (_targets.Count == 0) return;

        foreach (TargetState state in _targets)
        {
            if (!Win32.IsWindow(state.Hwnd)) continue;

            int exStyle = Win32.GetWindowLong(state.Hwnd, Win32.GWL_EXSTYLE);
            if ((exStyle & Win32.WS_EX_LAYERED) == 0)
                Win32.SetWindowLong(state.Hwnd, Win32.GWL_EXSTYLE, exStyle | Win32.WS_EX_LAYERED);
            SetAlpha(state);
        }
    }

    public void Restore()
    {
        if (_targets.Count == 0) return;

        foreach (TargetState state in _targets)
        {
            if (!Win32.IsWindow(state.Hwnd)) continue;

            if (!state.WasLayered)
                Win32.SetWindowLong(state.Hwnd, Win32.GWL_EXSTYLE, state.OriginalExStyle);
            Win32.SetLayeredWindowAttributes(state.Hwnd, 0, 255, Win32.LWA_ALPHA);
        }

        _targets.Clear();
        State = BlurState.Idle;
        WindowRestored?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyAlpha()
    {
        foreach (TargetState state in _targets)
            SetAlpha(state);
    }

    private void SetAlpha(TargetState state)
    {
        byte alpha = (byte)(state.AlphaPercent * 255 / 100);
        Win32.SetLayeredWindowAttributes(state.Hwnd, 0, alpha, Win32.LWA_ALPHA);
    }

    private void HealthCheck(object? sender, EventArgs e)
    {
        if (_targets.Count == 0) return;

        IntPtr? gone = null;
        for (int i = _targets.Count - 1; i >= 0; i--)
        {
            TargetState state = _targets[i];
            if (!Win32.IsWindow(state.Hwnd))
            {
                _targets.RemoveAt(i);
                gone ??= state.Hwnd;
                continue;
            }

            // 防止目标窗口在自身刷新/重绘后丢失 layered alpha,定时重新应用。
            int exStyle = Win32.GetWindowLong(state.Hwnd, Win32.GWL_EXSTYLE);
            if ((exStyle & Win32.WS_EX_LAYERED) == 0)
            {
                Win32.SetWindowLong(state.Hwnd, Win32.GWL_EXSTYLE,
                    exStyle | Win32.WS_EX_LAYERED);
            }
            SetAlpha(state);
        }

        if (_targets.Count == 0)
        {
            State = BlurState.Idle;
            TargetWindowGone?.Invoke(this, gone ?? IntPtr.Zero);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Restore();
        _healthCheckTimer.Stop();
        GC.SuppressFinalize(this);
    }

    ~WindowBlurer()
    {
        Restore();
    }
}
