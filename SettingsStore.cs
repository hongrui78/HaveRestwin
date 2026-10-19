using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace HaveRestwin;

/// 设置项存储：JSON 持久化到 %APPDATA%\HaveRestwin\settings.json，
/// 变更时广播 Changed 事件驱动引擎与各界面刷新。对应 Mac 版 SettingsStore.swift。
public sealed class SettingsStore : INotifyPropertyChanged
{
    public static SettingsStore Shared { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    /// 任意设置项变更（含持久化完成后）的总线，对应 Mac 的 .settingsChanged 通知。
    public event Action? Changed;

    private bool observing;

    private string appLanguage;
    private bool showTimerInFloating, playSoundOnBreakEnd, showExitButton;
    private bool standEnabled, launchAtLogin;
    private int standBreakSeconds, eyeFrequencyMinutes, eyeBreakSeconds, standMinutes;
    private string eyeMessage, standMessage;

    public string AppLanguage
    {
        get => appLanguage;
        set { if (appLanguage == value) return; appLanguage = value; OnChanged(); ApplyLanguageDefaults(); OnEdited(); }
    }
    public bool ShowTimerInFloating
    {
        get => showTimerInFloating;
        set { if (showTimerInFloating == value) return; showTimerInFloating = value; OnChanged(); OnEdited(); }
    }
    public bool PlaySoundOnBreakEnd
    {
        get => playSoundOnBreakEnd;
        set { if (playSoundOnBreakEnd == value) return; playSoundOnBreakEnd = value; OnChanged(); OnEdited(); }
    }
    public bool ShowExitButton
    {
        get => showExitButton;
        set { if (showExitButton == value) return; showExitButton = value; OnChanged(); OnEdited(); }
    }
    /// 空闲自动暂停固定开启：无操作满 1 分钟暂停计时，不再作为设置项暴露
    public const int IdlePauseMinutes = 1;
    public bool StandEnabled
    {
        get => standEnabled;
        set { if (standEnabled == value) return; standEnabled = value; OnChanged(); OnEdited(); }
    }
    public int StandBreakSeconds
    {
        get => standBreakSeconds;
        set { if (standBreakSeconds == value || value < 5 || value > 3600) return; standBreakSeconds = value; OnChanged(); OnEdited(); }
    }
    public int EyeFrequencyMinutes
    {
        get => eyeFrequencyMinutes;
        set { if (eyeFrequencyMinutes == value || value < 1 || value > 9999) return; eyeFrequencyMinutes = value; OnChanged(); OnEdited(); }
    }
    public int EyeBreakSeconds
    {
        get => eyeBreakSeconds;
        set { if (eyeBreakSeconds == value || value < 5 || value > 3600) return; eyeBreakSeconds = value; OnChanged(); OnEdited(); }
    }
    public string EyeMessage
    {
        get => eyeMessage;
        set { if (eyeMessage == value) return; eyeMessage = value; OnChanged(); OnEdited(); }
    }
    public int StandMinutes
    {
        get => standMinutes;
        set { if (standMinutes == value || value < 1 || value > 9999) return; standMinutes = value; OnChanged(); OnEdited(); }
    }
    public string StandMessage
    {
        get => standMessage;
        set { if (standMessage == value) return; standMessage = value; OnChanged(); OnEdited(); }
    }
    public bool LaunchAtLogin
    {
        get => launchAtLogin;
        set
        {
            if (launchAtLogin == value) return;
            launchAtLogin = value;
            OnChanged();
            if (observing) SyncLoginItem();
        }
    }

    private SettingsStore()
    {
        var dto = DiskSettings.Load();
        appLanguage = dto.AppLanguage ?? "system";
        showTimerInFloating = dto.ShowTimerInFloating ?? true;
        playSoundOnBreakEnd = dto.PlaySoundOnBreakEnd ?? true;
        showExitButton = dto.ShowExitButton ?? true;
        standEnabled = dto.StandEnabled ?? false;
        standBreakSeconds = dto.StandBreakSeconds ?? 40;
        eyeFrequencyMinutes = dto.EyeFrequencyMinutes ?? 22;
        eyeBreakSeconds = dto.EyeBreakSeconds ?? 20;
        eyeMessage = dto.EyeMessage ?? L10n.EyeZhDefault;
        standMinutes = dto.StandMinutes ?? 45;
        standMessage = dto.StandMessage ?? L10n.StandZhDefault;
        launchAtLogin = ReadLoginItem();
        observing = true;

        // 首启时把仍是内置默认值的提示文字对齐到当前语言。
        // 构造期内不能读 L10n.Current（它会回访 SettingsStore.Shared），本地解析。
        AppLanguage lang;
        switch (appLanguage)
        {
            case "english": lang = HaveRestwin.AppLanguage.English; break;
            case "chinese": lang = HaveRestwin.AppLanguage.Simplified; break;
            case "traditional": lang = HaveRestwin.AppLanguage.Traditional; break;
            default: lang = L10n.SystemSaysEnglish() ? HaveRestwin.AppLanguage.English : HaveRestwin.AppLanguage.Simplified; break;
        }
        ApplyLanguageDefaults(lang);
    }

    private void ApplyLanguageDefaults() => ApplyLanguageDefaults(L10n.Current);

    private void ApplyLanguageDefaults(AppLanguage lang)
    {
        if (L10n.EyeMigratable.Contains(eyeMessage))
            eyeMessage = lang switch
            {
                HaveRestwin.AppLanguage.English => L10n.EyeEnDefault,
                HaveRestwin.AppLanguage.Traditional => L10n.EyeHantDefault,
                _ => L10n.EyeZhDefault,
            };
        if (L10n.StandMigratable.Contains(standMessage))
            standMessage = lang switch
            {
                HaveRestwin.AppLanguage.English => L10n.StandEnDefault,
                HaveRestwin.AppLanguage.Traditional => L10n.StandHantDefault,
                _ => L10n.StandZhDefault,
            };
        OnChanged(nameof(EyeMessage));
        OnChanged(nameof(StandMessage));
        if (observing) Save();
    }

    private void OnEdited()
    {
        if (!observing) return;
        Save();
        Changed?.Invoke();
    }

    private void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void Save() => DiskSettings.Save(Snapshot());

    private DiskSettings Snapshot() => new()
    {
        AppLanguage = appLanguage,
        ShowTimerInFloating = showTimerInFloating,
        PlaySoundOnBreakEnd = playSoundOnBreakEnd,
        ShowExitButton = showExitButton,
        StandEnabled = standEnabled,
        StandBreakSeconds = standBreakSeconds,
        EyeFrequencyMinutes = eyeFrequencyMinutes,
        EyeBreakSeconds = eyeBreakSeconds,
        EyeMessage = eyeMessage,
        StandMinutes = standMinutes,
        StandMessage = standMessage,
    };

    // MARK: - 开机自启（HKCU Run 键）

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "HaveARest";

    private static void SyncLoginItem()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (Shared.launchAtLogin)
            {
                var exe = Environment.ProcessPath;
                if (exe != null) key.SetValue(RunValueName, $"\"{exe}\"");
            }
            else
            {
                if (key.GetValue(RunValueName) != null) key.DeleteValue(RunValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception e)
        {
            Log("开机启动设置失败 " + e.Message);
        }
    }

    private static bool ReadLoginItem()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(RunValueName) != null;
        }
        catch
        {
            return false;
        }
    }

    internal static void Log(string msg) =>
        File.AppendAllText(LogPath(), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {msg}{Environment.NewLine}");

    private static string LogPath() => Path.Combine(SettingsDir, "error.log");

    private static string SettingsDir
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HaveRestwin");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// JSON 持久化模型；可空字段兼容旧文件缺项。
    private sealed class DiskSettings
    {
        [JsonPropertyName("appLanguage")] public string? AppLanguage { get; set; }
        [JsonPropertyName("showTimerInMenuBar")] public bool? ShowTimerInFloating { get; set; }
        [JsonPropertyName("playSoundOnBreakEnd")] public bool? PlaySoundOnBreakEnd { get; set; }
        [JsonPropertyName("showExitButton")] public bool? ShowExitButton { get; set; }
        [JsonPropertyName("standEnabled")] public bool? StandEnabled { get; set; }
        [JsonPropertyName("standBreakSeconds")] public int? StandBreakSeconds { get; set; }
        [JsonPropertyName("eyeFrequencyMinutes")] public int? EyeFrequencyMinutes { get; set; }
        [JsonPropertyName("eyeBreakSeconds")] public int? EyeBreakSeconds { get; set; }
        [JsonPropertyName("eyeMessage")] public string? EyeMessage { get; set; }
        [JsonPropertyName("standMinutes")] public int? StandMinutes { get; set; }
        [JsonPropertyName("standMessage")] public string? StandMessage { get; set; }

        public static DiskSettings Load()
        {
            try
            {
                var path = Path.Combine(SettingsDir, "settings.json");
                if (File.Exists(path))
                    return JsonSerializer.Deserialize<DiskSettings>(File.ReadAllText(path)) ?? new();
            }
            catch (Exception e)
            {
                Log("读取设置失败 " + e.Message);
            }
            return new();
        }

        public static void Save(DiskSettings s)
        {
            try
            {
                var path = Path.Combine(SettingsDir, "settings.json");
                File.WriteAllText(path, JsonSerializer.Serialize(s,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception e)
            {
                Log("保存设置失败 " + e.Message);
            }
        }
    }
}
