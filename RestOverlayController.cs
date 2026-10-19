using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace HaveRestwin;

/// 全屏休息提示：每台显示器一个置顶半透明白色面板，倒计时结束自动关闭。
/// 对应 Mac 版 RestOverlayController.swift。
public sealed class RestOverlayController : INotifyPropertyChanged
{
    public enum BreakKind { EyeCountdown, Manual, Stand }

    public static RestOverlayController Shared { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private const double VeilOpacity = 0.90;
    private const double FadeOutSeconds = 0.45;

    private readonly List<RestOverlayWindow> panels = new();
    private DispatcherTimer? countdownTimer;
    private Action? onFinished;
    private bool isClosing;

    public bool IsClosed { get; private set; } = true;
    public BreakKind Kind { get; private set; } = BreakKind.Manual;
    public string Message { get; private set; } = "";
    public int Remaining { get; private set; }

    public string RemainingText => RestEngine.Mmss(Math.Max(0, Remaining));
    public bool ShowExitButton => SettingsStore.Shared.ShowExitButton;
    public string ExitButtonText => L10n.E("Exit Now", "立即退出");
    public Visibility ExitButtonVisibility => ShowExitButton ? Visibility.Visible : Visibility.Collapsed;
    public ICommand DismissCommand { get; }

    private RestOverlayController()
    {
        DismissCommand = new RelayCommand(_ => Dismiss());
        SettingsStore.Shared.Changed += () =>
        {
            OnChanged(nameof(ShowExitButton));
            OnChanged(nameof(ExitButtonVisibility));
            OnChanged(nameof(ExitButtonText));
        };
    }

    public void BeginEyeBreak(string message, int seconds, Action finished) =>
        Present(message, BreakKind.EyeCountdown, seconds, finished);

    public void BeginManualBreak(string message, int seconds, Action finished) =>
        Present(message, BreakKind.Manual, seconds, finished);

    public void BeginStandBreak(string message, int seconds, Action finished) =>
        Present(message, BreakKind.Stand, seconds, finished);

    /// 设置页预览：无完成回调
    public void Preview(string message, int seconds) =>
        Present(message, BreakKind.EyeCountdown, seconds, null);

    private void Present(string message, BreakKind kind, int seconds, Action? finished)
    {
        if (!IsClosed || isClosing) return;
        IsClosed = false;
        Kind = kind;
        Message = message;
        Remaining = seconds;
        onFinished = finished;

        double scale = NativeMethods.SystemDpi() / 96.0;
        foreach (var monitor in NativeMethods.GetMonitors())
        {
            var panel = new RestOverlayWindow(this)
            {
                Left = monitor.Bounds.X / scale,
                Top = monitor.Bounds.Y / scale,
                Width = Math.Max(1, monitor.Bounds.Width / scale),
                Height = Math.Max(1, monitor.Bounds.Height / scale),
            };
            panel.Show();
            panels.Add(panel);
        }

        OnChanged(nameof(IsClosed));
        OnChanged(nameof(Message));
        OnChanged(nameof(RemainingText));

        // 所有类型统一走实时倒计时：Remaining 每秒递减，归 0 自动关闭
        countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        countdownTimer.Tick += (_, _) => CountdownTick();
        countdownTimer.Start();
    }

    private void CountdownTick()
    {
        if (IsClosed || isClosing) return;
        Remaining--;
        OnChanged(nameof(RemainingText));
        if (Remaining <= 0) Dismiss();
    }

    public void Dismiss()
    {
        if (IsClosed || isClosing) return;
        IsClosed = true;
        isClosing = true;
        countdownTimer?.Stop();
        countdownTimer = null;
        OnChanged(nameof(IsClosed));

        var closing = panels.ToArray();
        panels.Clear();
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(FadeOutSeconds))
        {
            EasingFunction = new CircleEase { EasingMode = EasingMode.EaseInOut },
        };
        fade.Completed += (_, _) =>
        {
            foreach (var w in closing) w.Close();
            isClosing = false;
            var callback = onFinished;
            onFinished = null;
            callback?.Invoke();
        };
        foreach (var w in closing) w.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
