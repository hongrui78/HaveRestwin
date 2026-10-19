using System.Windows;
using System.Windows.Threading;

namespace HaveRestwin;

/// 应用入口：单实例检查 → 创建悬浮计时窗 → 启动计时引擎。
/// 对应 Mac 版 AppDelegate.swift（菜单栏图标在 Windows 上改为桌面悬浮球）。
public partial class App : Application
{
    private SingleInstance? singleInstance;
    private FloatingWidgetWindow? widget;
    private SettingsWindow? settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            SettingsStore.Log("未处理异常: " + args.Exception);
            args.Handled = true; // 记日志继续跑，别让整个小工具崩掉
        };

        // 只允许运行一个实例：抢不到锁说明已有实例在跑，提示后退出。
        singleInstance = SingleInstance.TryAcquire();
        if (singleInstance == null)
        {
            MessageBox.Show(
                L10n.E(
                    $"Only one copy of {L10n.AppName} can run at a time.\n" +
                    "The floating timer already on your screen is the one you want.",
                    $"{L10n.AppName} 同时只能运行一个。\n屏幕上已有的悬浮计时就是正在运行的那个。"),
                L10n.E($"{L10n.AppName} is already running", $"{L10n.AppName} 已经在运行了"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        Actions.OpenSettings = OpenSettings;
        Actions.Quit = () => Shutdown();

        widget = new FloatingWidgetWindow();
        widget.Show();
        widget.UpdateLayout();
        widget.PlaceDefault();

        var engine = RestEngine.Shared;
        // 到点直接弹站立休息面板（对应 Mac 对 $status == .standPending 的订阅）
        engine.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(RestEngine.Status)
                && engine.Status == RestEngine.State.StandPending)
                engine.BeginStandBreak();
        };
        engine.Start();
    }

    private void OpenSettings()
    {
        // 首次创建后复用同一实例：关闭再打开只是重新 Show，保留滚动位置等状态
        settingsWindow ??= new SettingsWindow();
        if (!settingsWindow.IsVisible) settingsWindow.Show();
        settingsWindow.Activate();
    }
}
