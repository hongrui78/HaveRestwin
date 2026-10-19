using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace HaveRestwin;

/// 悬浮计时窗：常驻显示倒计时，鼠标悬停展开下拉控制面板，可拖动。
/// 面板与计时球默认居中对齐，超出显示器工作区时自动改为左/右对齐贴边。
public partial class FloatingWidgetWindow : Window
{
    private const string MoonGlyph = "\uE708";
    private const string PauseGlyph = "\uE769";
    private const string PlayGlyph = "\uE768";

    private const double PanelWidth = 270;
    private const double ScreenEdgeGap = 6;

    private readonly RestEngine engine = RestEngine.Shared;
    private readonly SettingsStore settings = SettingsStore.Shared;
    private readonly DispatcherTimer collapseTimer;

    public FloatingWidgetWindow()
    {
        InitializeComponent();

        collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        collapseTimer.Tick += (_, _) => CollapsePanel();

        MouseEnter += (_, _) => ExpandPanel();
        MouseLeave += (_, _) =>
        {
            collapseTimer.Stop();
            collapseTimer.Start();
        };

        engine.PropertyChanged += (_, _) => UpdateAll();
        settings.Changed += UpdateAll;
        RestOverlayController.Shared.PropertyChanged += OverlayChanged;

        UpdateAll();
    }

    /// 初始位置：主屏工作区右上角
    public void PlaceDefault()
    {
        double scale = NativeMethods.SystemDpi() / 96.0;
        var monitors = NativeMethods.GetMonitors();
        var primary = monitors.Find(m => m.IsPrimary);
        var work = primary.IsPrimary
            ? primary.WorkArea
            : new NativeMethods.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };
        Left = (work.Right - 30) / scale - ActualWidth;
        Top = work.Top / scale + 8;
    }

    // MARK: - 面板展开/收起与对齐

    private const double FadeInSec = 1.0;
    private const double FadeOutSec = 1.0;
    private bool panelFadingOut;

    private void AnimatePanelOpacity(double from, double to, double seconds, Action? done)
    {
        var anim = new DoubleAnimation(from, to, TimeSpan.FromSeconds(seconds))
        {
            EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut },
        };
        if (done != null) anim.Completed += (_, _) => done();
        Panel.BeginAnimation(UIElement.OpacityProperty, anim, HandoffBehavior.Compose);
    }

    private void ExpandPanel()
    {
        if (!RestOverlayController.Shared.IsClosed) return;
        collapseTimer.Stop();
        if (panelFadingOut)
        {
            // 淡出途中鼠标又进来：动画会停在中间值，直接接管重新淡入
            panelFadingOut = false;
            Panel.BeginAnimation(UIElement.OpacityProperty, null);
            Panel.Visibility = Visibility.Collapsed;
        }
        if (Panel.Visibility == Visibility.Visible) return;
        Panel.Opacity = 0;
        Panel.Visibility = Visibility.Visible;
        UpdateAll();
        AlignPanel();
        AnimatePanelOpacity(0, 1, FadeInSec, null);
    }

    private void CollapsePanel()
    {
        collapseTimer.Stop();
        if (panelFadingOut || Panel.Visibility != Visibility.Visible) return;
        panelFadingOut = true;
        AnimatePanelOpacity(1, 0, FadeOutSec, () =>
        {
            panelFadingOut = false;
            // 展开时可能把窗口整体左移过，收起时把窗口挪回计时球的位置
            double pillScreenLeft = Left + Pill.Margin.Left;
            Panel.Visibility = Visibility.Collapsed;
            Panel.Opacity = 1;
            Pill.Margin = new Thickness(0);
            Panel.Margin = new Thickness(0, 6, 0, 0);
            Left = pillScreenLeft;
        });
    }

    private void AlignPanel()
    {
        double scale = NativeMethods.SystemDpi() / 96.0;
        // 收起状态下窗口左缘 == 计时球左缘
        double pillLeft = Left + Pill.Margin.Left;
        double pillW = Pill.ActualWidth;
        double desired = pillLeft + pillW / 2 - PanelWidth / 2; // 默认：与时间居中对齐

        // 找计时球所在显示器的工作区，面板整体不得超出
        double centerPx = (pillLeft + pillW / 2) * scale;
        var monitors = NativeMethods.GetMonitors();
        var hit = monitors.Find(m => centerPx >= m.WorkArea.Left && centerPx < m.WorkArea.Right);
        var work = hit.WorkArea;
        if (work.Right <= work.Left)
        {
            var primary = monitors.Find(m => m.IsPrimary);
            work = primary.IsPrimary
                ? primary.WorkArea
                : new NativeMethods.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };
        }

        double minLeft = work.Left / scale + ScreenEdgeGap;
        double maxLeft = Math.Max(minLeft, work.Right / scale - ScreenEdgeGap - PanelWidth);
        desired = Math.Clamp(desired, minLeft, maxLeft);

        if (desired >= pillLeft)
        {
            // 面板向右铺开：窗口不动，给面板加左外边距（含右越界时贴右边=左对齐到 maxLeft）
            Pill.Margin = new Thickness(0);
            Panel.Margin = new Thickness(desired - pillLeft, 6, 0, 0);
        }
        else
        {
            // 面板要伸到计时球左侧：窗口整体左移，计时球加左外边距保持原位
            Left = desired;
            Pill.Margin = new Thickness(pillLeft - desired, 0, 0, 0);
            Panel.Margin = new Thickness(0, 6, 0, 0);
        }
    }

    private void OverlayChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RestOverlayController.IsClosed)
            && !RestOverlayController.Shared.IsClosed)
        {
            // 休息遮罩出现时收起下拉面板，对应 Mac 的 popover.performClose
            CollapsePanel();
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        // 只在按到计时球本体时拖动，面板区域不误触
        if (e.OriginalSource is DependencyObject d && IsInPath(d, Pill))
        {
            try { DragMove(); } catch (InvalidOperationException) { }
        }
    }

    private static bool IsInPath(DependencyObject? node, DependencyObject ancestor)
    {
        while (node != null)
        {
            if (ReferenceEquals(node, ancestor)) return true;
            node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node);
        }
        return false;
    }

    // MARK: - 按钮动作

    private void PauseToggle_Click(object sender, RoutedEventArgs e) => engine.TogglePause();
    private void Reset_Click(object sender, RoutedEventArgs e) => engine.ResetAll();
    private void RestNow_Click(object sender, RoutedEventArgs e) => engine.ManualRestNow();
    private void StartStand_Click(object sender, RoutedEventArgs e) => engine.BeginStandBreak();
    private void Settings_Click(object sender, RoutedEventArgs e) => Actions.OpenSettings?.Invoke();
    private void Quit_Click(object sender, RoutedEventArgs e) => Actions.Quit?.Invoke();

    // MARK: - 文案与状态刷新（每秒 tick 与各设置变更都会走到）

    private void UpdateAll()
    {
        if (PillText == null) return;

        PillText.Text = settings.ShowTimerInFloating ? engine.StatusText : MoonGlyph;

        EyeLabel.Text = L10n.E("Eye Care", "护眼");
        CountdownText.Text = CountdownLine();
        PauseToggleIcon.Content = engine.Status == RestEngine.State.Paused ? PlayGlyph : PauseGlyph;

        StandRow.Visibility = settings.StandEnabled ? Visibility.Visible : Visibility.Collapsed;
        StandLabel.Text = L10n.E("Stand", "站立");
        StandTimeText.Text = RestEngine.Mmss(Math.Max(0, engine.StandRemaining));

        bool pending = engine.Status == RestEngine.State.StandPending;
        StandPendingBox.Visibility = pending ? Visibility.Visible : Visibility.Collapsed;
        StandPendingLabel.Text = L10n.E("Time to Stand Up", "站立提醒到点了");
        StartStandButton.Content = L10n.E("Start Stand Break", "开始站立休息");

        PauseToggleIcon.ToolTip = engine.Status == RestEngine.State.Paused
            ? L10n.E("Resume", "继续") : L10n.E("Pause", "暂停");
        ResetIcon.ToolTip = L10n.E("Reset", "重置");
        RestNowIcon.ToolTip = L10n.E("Rest Now", "立刻休息");
        SettingsIcon.ToolTip = L10n.E("Settings", "设置");
        QuitIcon.ToolTip = L10n.E("Quit", "退出");
        ToolTipService.SetToolTip(Pill, L10n.E("Hover to open panel", "悬停展开控制面板"));
    }

    // 暂停时不再显示「计时已暂停」，直接显示冻结的倒计时（引擎暂停时本就不减 EyeRemaining）
    private string CountdownLine() => engine.Status switch
    {
        RestEngine.State.RestingEye => RestOverlayController.Shared.IsClosed
            ? L10n.E("Resting", "休息中")
            : L10n.E($"Rest {RestEngine.Mmss(Math.Max(0, RestOverlayController.Shared.Remaining))}",
                     $"休息 {RestEngine.Mmss(Math.Max(0, RestOverlayController.Shared.Remaining))}"),
        RestEngine.State.StandPending => L10n.E("Waiting for Stand Break", "等待站起来休息"),
        _ => RestEngine.Mmss(Math.Max(0, engine.EyeRemaining)),
    };
}

/// 视图层调用 App 动作的钩子，避免界面直接依赖入口类。对应 Mac 版 RestAppActions。
public static class Actions
{
    public static Action? OpenSettings;
    public static Action? Quit;
}
