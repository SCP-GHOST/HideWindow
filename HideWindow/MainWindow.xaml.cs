using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using HideWindow.Controls;
using HideWindow.Services;

namespace HideWindow;

public class WindowInfo
{
    public IntPtr Handle { get; init; }
    public string Title { get; init; } = "";

    public override string ToString() => Title;
}

public partial class MainWindow : Window
{
    private const int MaxSelectors = 10;

    private sealed class WindowSelectorRow
    {
        public required Grid Root { get; init; }
        public required ComboBox Combo { get; init; }
        public required Slider Slider { get; init; }
        public required OutlinedTextBlock PercentText { get; init; }
    }

    private readonly MouseHook _mouseHook = new();
    private readonly WindowBlurer _blurer = new();
    private readonly ObservableCollection<WindowInfo> _windowList = new();
    private readonly List<WindowSelectorRow> _windowSelectors = new();

    public MainWindow()
    {
        InitializeComponent();
        EnsureSelectors();

        _mouseHook.TargetWindowClicked += OnTargetWindowClicked;
        _blurer.WindowBlurred += (_, _) => UpdateUI();
        _blurer.WindowRestored += (_, _) => UpdateUI();
        _blurer.TargetWindowGone += (_, _) => UpdateUI();
        StateChanged += (_, _) => UpdateMaximizeIcon();
        Closed += (_, _) =>
        {
            _mouseHook.Dispose();
            _blurer.Dispose();
        };
        UpdateMaximizeIcon();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        string skinDir = BackgroundSkin.GetSkinDirectory();
        BackgroundSkin skin = BackgroundSkin.Load(skinDir);
        skin.Apply(this, BgImage, DarkenOverlay);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();

    private void UpdateMaximizeIcon()
    {
        bool maximized = WindowState == WindowState.Maximized;
        MaximizeIcon.Visibility = maximized ? Visibility.Collapsed : Visibility.Visible;
        RestoreIcon.Visibility = maximized ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityText == null) return;
        int val = (int)Math.Round(e.NewValue);
        OpacityText.Text = val + "%";
        _blurer.CurrentAlphaPercent = val;
    }

    private void BlurButton_Click(object sender, RoutedEventArgs e)
    {
        switch (_blurer.State)
        {
            case BlurState.Idle:
                if (SelectModeCombo.SelectedIndex == 1)
                {
                    // 预选模式:按每行滑块的透明度批量虚化
                    List<(IntPtr Hwnd, int AlphaPercent)> targets = _windowSelectors
                        .Where(r => r.Combo.SelectedItem is WindowInfo)
                        .Select(r => (
                            Hwnd: ((WindowInfo)r.Combo.SelectedItem!).Handle,
                            AlphaPercent: (int)Math.Round(r.Slider.Value)))
                        .ToList();
                    if (targets.Count == 0)
                    {
                        StatusText.Text = "请先从列表中选择要虚化的窗口";
                        return;
                    }
                    if (_blurer.ApplyTargets(targets))
                        UpdateUI();
                }
                else
                {
                    // 点击下一个模式
                    _blurer.StartAwaiting();
                    _mouseHook.Install();
                    UpdateUI();
                }
                break;

            case BlurState.AwaitingClick:
                _mouseHook.Uninstall();
                _blurer.CancelAwaiting();
                UpdateUI();
                break;

            case BlurState.Blurring:
                _mouseHook.Uninstall();
                _blurer.Restore();
                UpdateUI();
                break;
        }
    }

    private void SelectModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetPanel == null) return;

        bool preset = SelectModeCombo.SelectedIndex == 1;
        PresetPanel.Visibility = preset ? Visibility.Visible : Visibility.Collapsed;
        GlobalOpacitySection.Visibility = preset ? Visibility.Collapsed : Visibility.Visible;
        if (preset)
        {
            EnsureSelectors();
            RefreshWindowList();
        }

        if (_blurer.State == BlurState.Blurring)
            _blurer.ReapplyAlphas();
    }

    private void AddWindowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_windowSelectors.Count < MaxSelectors)
            AddSelector();
    }

    private void RemoveWindowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_windowSelectors.Count > 1)
            RemoveSelector();
    }

    private void EnsureSelectors()
    {
        if (_windowSelectors.Count == 0)
            AddSelector();
    }

    private void AddSelector()
    {
        var root = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var combo = new ComboBox
        {
            ItemsSource = _windowList,
            ItemTemplate = (DataTemplate)FindResource("WindowItemTemplate"),
            FontSize = 12,
            Height = 24,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(combo, 0);

        var slider = new Slider
        {
            Minimum = 1,
            Maximum = 100,
            Value = 50,
            TickFrequency = 10,
            IsSnapToTickEnabled = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0)
        };
        Grid.SetColumn(slider, 1);

        var percent = new OutlinedTextBlock
        {
            Text = "50%",
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        };
        Grid.SetColumn(percent, 2);

        slider.ValueChanged += (_, e) =>
        {
            int val = (int)Math.Round(e.NewValue);
            percent.Text = val + "%";
            if (_blurer.State == BlurState.Blurring && combo.SelectedItem is WindowInfo w)
                _blurer.SetTargetAlpha(w.Handle, val);
        };

        root.Children.Add(combo);
        root.Children.Add(slider);
        root.Children.Add(percent);

        _windowSelectors.Add(new WindowSelectorRow
        {
            Root = root,
            Combo = combo,
            Slider = slider,
            PercentText = percent
        });
        WindowSelectorList.Children.Add(root);
        UpdateSelectorUi();
    }

    private void RemoveSelector()
    {
        WindowSelectorRow last = _windowSelectors[^1];
        _windowSelectors.RemoveAt(_windowSelectors.Count - 1);
        WindowSelectorList.Children.Remove(last.Root);
        UpdateSelectorUi();
    }

    private void UpdateSelectorUi()
    {
        WindowCountText.Text = $"{_windowSelectors.Count}/{MaxSelectors}";
        AddWindowButton.IsEnabled = _windowSelectors.Count < MaxSelectors;
        RemoveWindowButton.IsEnabled = _windowSelectors.Count > 1;
    }

    private void RefreshWindowList_Click(object sender, RoutedEventArgs e)
    {
        RefreshWindowList();
    }

    private void RefreshWindowList()
    {
        _windowList.Clear();
        Win32.EnumWindows((hwnd, _) =>
        {
            if (hwnd == IntPtr.Zero) return true;

            int length = Win32.GetWindowTextLength(hwnd);
            if (length == 0) return true;

            IntPtr parent = Win32.GetParent(hwnd);
            if (parent != IntPtr.Zero) return true; // skip child windows

            if ((Win32.GetWindowLong(hwnd, Win32.GWL_STYLE) & (int)Win32.WindowStyles.WS_VISIBLE) == 0)
                return true;

            var sb = new System.Text.StringBuilder(length + 1);
            Win32.GetWindowText(hwnd, sb, sb.Capacity);
            string title = sb.ToString();
            if (string.IsNullOrWhiteSpace(title)) return true;

            _windowList.Add(new WindowInfo { Handle = hwnd, Title = title });
            return true;
        }, IntPtr.Zero);
    }

    private void OnTargetWindowClicked(object? sender, IntPtr hwnd)
    {
        _mouseHook.Uninstall();
        if (_blurer.ApplyTarget(hwnd))
        {
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        switch (_blurer.State)
        {
            case BlurState.Idle:
                BlurButton.Content = "虚化";
                StatusText.Text = SelectModeCombo.SelectedIndex == 1
                    ? "从列表选择窗口后点击\"虚化\""
                    : "点击\"虚化\"后,再点击任意窗口即可将其变为透明";
                break;
            case BlurState.AwaitingClick:
                BlurButton.Content = "取消";
                StatusText.Text = "请点击要虚化的窗口(点击\"取消\"可退出)";
                break;
            case BlurState.Blurring:
                BlurButton.Content = "解除虚化";
                StatusText.Text = $"已虚化 {_blurer.TargetCount} 个窗口。拖动各窗口滑条实时调整透明度,或点击\"解除虚化\"恢复";
                break;
        }
    }
}
