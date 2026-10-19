using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace HaveRestwin;

/// 设置窗口：通用 / 休息 / 关于 三个标签页。对应 Mac 版 SettingsView.swift。
/// 语言文案集中由 UpdateTexts() 刷新，语言切换即时生效。
public partial class SettingsWindow : Window
{
    private readonly SettingsStore settings = SettingsStore.Shared;
    private bool loaded;

    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = settings;
        Loaded += (_, _) =>
        {
            loaded = true;
            LanguageCombo.SelectedValue = settings.AppLanguage;
            UpdateTexts();
        };
        settings.Changed += UpdateTexts;
        settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsStore.EyeMessage)) UpdateEyePreview();
            if (e.PropertyName == nameof(SettingsStore.StandMessage)) UpdateStandPreview();
        };
    }

    private void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!loaded) return;
        if (LanguageCombo.SelectedValue is string lang)
            settings.AppLanguage = lang;
    }

    private void IdleLayout_Invalidate(object sender, RoutedEventArgs e)
    {
        if (IdleMinutesRow == null || IdlePauseCheck == null) return;
        IdleMinutesRow.Visibility = IdlePauseCheck.IsChecked == true
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Kind_Changed(object sender, RoutedEventArgs e)
    {
        if (EyePanel == null || StandPanel == null) return;
        bool eye = RadioEye?.IsChecked == true;
        EyePanel.Visibility = eye ? Visibility.Visible : Visibility.Collapsed;
        StandPanel.Visibility = eye ? Visibility.Collapsed : Visibility.Visible;
    }

    private void PreviewEye_Click(object sender, RoutedEventArgs e) =>
        RestOverlayController.Shared.Preview(EyePreviewText.Text, settings.EyeBreakSeconds);

    private void PreviewStand_Click(object sender, RoutedEventArgs e) =>
        RestOverlayController.Shared.Preview(StandPreviewText.Text, settings.StandBreakSeconds);

    // MARK: - 文案刷新

    private void UpdateTexts()
    {
        if (!loaded) return;

        Title = L10n.E("Settings", "设置");
        GeneralTab.Header = L10n.E("General", "通用");
        RestTab.Header = L10n.E("Rest", "休息");
        AboutTab.Header = L10n.E("About", "关于");

        LanguageLabel.Text = L10n.E("Language", "语言");
        LangSystemItem.Content = L10n.E("System Language", "系统语言");
        LangSimplifiedItem.Content = L10n.E("Simplified Chinese", "简体中文");
        LangTraditionalItem.Content = L10n.E("Traditional Chinese", "繁体中文");

        LaunchCheck.Content = L10n.E("Launch at Login", "开机启动");
        ShowTimerCheck.Content = L10n.E("Show Timer", "悬浮窗显示计时");
        SoundCheck.Content = L10n.E("Play Sound When Rest Ends", "休息结束播放声音");
        ExitBtnCheck.Content = L10n.E("Show Exit Button on Prompt", "提示面板显示退出按钮");
        IdlePauseCheck.Content = L10n.E("Pause When Idle", "无操作暂停计时");
        IdleMinutesLabel.Text = L10n.E(
            $"Pause after {settings.IdlePauseMinutes} min idle",
            $"空闲 {settings.IdlePauseMinutes} 分钟后暂停");
        IdleMinutesUnit.Text = L10n.E("min", "分钟");
        StandEnabledCheck.Content = L10n.E("Enable Stand Reminders", "开启站立提醒");

        RadioEye.Content = L10n.E("Eyes", "眼睛");
        RadioStand.Content = L10n.E("Stand", "站立");

        EyePlanTitle.Text = L10n.E("Schedule", "计划");
        EyeEveryLabel.Text = L10n.E("Every", "每");
        EyeMinLabel.Text = L10n.E("min", "分钟");
        EyeRestLabel.Text = L10n.E("rest", "休息");
        EyeSecLabel.Text = L10n.E("sec", "秒");
        EyeDisplayTitle.Text = L10n.E("Display", "显示");
        EyeMsgLabel.Text = L10n.E("Eye Rest Prompt", "休息提示文字");
        EyePreviewCaption.Text = L10n.E("Preview", "预览");
        EyePreviewButton.Content = L10n.E("Preview Prompt", "预览提示面板");

        StandPlanTitle.Text = L10n.E("Schedule", "计划");
        StandEveryLabel.Text = L10n.E("Every", "每");
        StandMinLabel.Text = L10n.E("min", "分钟");
        StandRestLabel.Text = L10n.E("rest", "休息");
        StandSecLabel.Text = L10n.E("sec", "秒");
        StandDisplayTitle.Text = L10n.E("Display", "显示");
        StandMsgLabel.Text = L10n.E("Stand Prompt", "站立提示文字");
        StandPreviewCaption.Text = L10n.E("Preview", "预览");
        StandPreviewButton.Content = L10n.E("Preview Prompt", "预览提示面板");

        AboutName.Text = L10n.AppName;
        AboutVersion.Text = L10n.E($"Version {ShortVersion()}", $"版本 {ShortVersion()}");
        AboutTagline.Text = L10n.E(
            "Eye breaks and stand reminders, floating on your desktop.",
            "悬浮窗常驻，护眼休息与站立提醒。");

        UpdateEyePreview();
        UpdateStandPreview();
        IdleLayout_Invalidate(this, new RoutedEventArgs());
    }

    private void UpdateEyePreview()
    {
        if (EyePreviewText == null) return;
        EyePreviewText.Text = settings.EyeMessage.Length > 0
            ? settings.EyeMessage
            : L10n.E(L10n.EyeEnDefault, L10n.EyeZhDefault);
    }

    private void UpdateStandPreview()
    {
        if (StandPreviewText == null) return;
        StandPreviewText.Text = settings.StandMessage.Length > 0
            ? settings.StandMessage
            : L10n.E(L10n.StandEnDefault, L10n.StandZhDefault);
    }

    // MARK: - 关闭时提交未失去焦点的输入框

    /// 数字/文字框用 LostFocus 提交：用户改完直接关窗会丢改动，这里在关闭前统一 UpdateSource
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        FlushPendingEdits(this);
        base.OnClosing(e);
    }

    private static void FlushPendingEdits(DependencyObject root)
    {
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is TextBox box)
                box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            else
                FlushPendingEdits(child);
        }
    }

    private static string ShortVersion() =>
        typeof(SettingsWindow).Assembly.GetName().Version?.ToString(3)
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "1.0.0";
}
