using System;
using System.Collections.Generic;
using System.Globalization;

namespace HaveRestwin;

/// 运行时可切换语言（简中源文案 / 繁中转写 / 英文），移植自 Mac 版 Localization.swift。
public static class L10n
{
    // 内置提示文字默认值（简中为源，繁/英各一），语言切换时用于迁移
    public const string EyeZhDefault = "休息一下，眺望远方";
    public const string EyeHantDefault = "休息一下，眺望遠方";
    public const string EyeEnDefault = "Take a break, gaze into the distance";
    public const string StandZhDefault = "站起来活动一下";
    public const string StandHantDefault = "站起來活動一下";
    public const string StandEnDefault = "Stand up and move around";

    public static readonly IReadOnlySet<string> EyeMigratable =
        new HashSet<string> { EyeZhDefault, EyeHantDefault, EyeEnDefault };
    public static readonly IReadOnlySet<string> StandMigratable =
        new HashSet<string> { StandZhDefault, StandHantDefault, StandEnDefault };

    /// 系统首选语言是否为英文（不依赖 SettingsStore，可在其构造期安全调用）。
    public static bool SystemSaysEnglish()
    {
        var code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return !code.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
    }

    /// 解析当前生效语言；系统语言解析为英文或简中。
    public static AppLanguage Current
    {
        get
        {
            switch (SettingsStore.Shared.AppLanguage)
            {
                case "english": return AppLanguage.English;
                case "chinese": return AppLanguage.Simplified;
                case "traditional": return AppLanguage.Traditional;
                default: return SystemSaysEnglish() ? AppLanguage.English : AppLanguage.Simplified;
            }
        }
    }

    public static string AppName => Current == AppLanguage.English ? "Have A Break" : "休休";

    /// 二选一：英文 / 中文（简或繁）。简中是源文案；繁中由 ToTraditional 转换。
    public static string E(string english, string chinese) => Current switch
    {
        AppLanguage.English => english,
        AppLanguage.Traditional => ToTraditional(chinese),
        _ => chinese,
    };

    /// 简转繁：先查整句短语表，未命中再逐字转换。
    public static string ToTraditional(string s)
    {
        if (PhraseTable.TryGetValue(s, out var full)) return full;
        var chars = new char[s.Length];
        for (int i = 0; i < s.Length; i++)
            chars[i] = CharTable.TryGetValue(s[i], out var t) ? t : s[i];
        return new string(chars);
    }

    private static readonly Dictionary<string, string> PhraseTable = new()
    {
        ["休息一下，眺望远方"] = "休息一下，眺望遠方",
        ["站起来活动一下"] = "站起來活動一下",
        ["立即退出"] = "立即結束",
        ["设置"] = "設定",
        ["点击继续计时"] = "點擊繼續計時",
        ["点击暂停计时"] = "點擊暫停計時",
        ["站立提醒到点了"] = "站立提醒到點了",
        ["开始站立休息"] = "開始站立休息",
        ["继续"] = "繼續",
        ["暂停"] = "暫停",
        ["立刻休息"] = "立刻休息",
        ["退出"] = "退出",
        ["计时已暂停"] = "計時已暫停",
        ["等待站起来休息"] = "等待站起來休息",
        ["已暂停"] = "已暫停",
        ["该站起来了"] = "該站起來了",
        ["通用"] = "一般",
        ["关于"] = "關於",
        ["语言"] = "語言",
        ["系统语言"] = "系統語言",
        ["简体中文"] = "簡體中文",
        ["繁体中文"] = "繁體中文",
        ["开机启动"] = "開機啟動",
        ["悬浮窗显示计时"] = "懸浮窗顯示計時",
        ["菜单栏显示计时器"] = "選單列顯示計時器",
        ["休息结束播放声音"] = "休息結束播放聲音",
        ["提示面板显示退出按钮"] = "提示面板顯示退出按鈕",
        ["开启站立提醒"] = "開啟站立提醒",
        ["计划"] = "計畫",
        ["分钟"] = "分鐘",
        ["显示"] = "顯示",
        ["预览"] = "預覽",
        ["预览提示面板"] = "預覽提示面板",
        ["悬浮窗常驻，护眼休息与站立提醒。"] = "懸浮窗常駐，護眼休息與站立提醒。",
        ["眼睛休息与站立提醒，常驻菜单栏。"] = "眼睛休息與站立提醒，常駐選單列。",
    };

    /// 逐字表：覆盖本项目用到的简繁差异字，供含插值的动态文案兜底。
    private static readonly Dictionary<char, char> CharTable = new()
    {
        ['设'] = '設', ['语'] = '語', ['系'] = '係', ['统'] = '統', ['点'] = '點', ['击'] = '擊',
        ['继'] = '續', ['续'] = '續', ['计'] = '計', ['时'] = '時', ['暂'] = '暫', ['开'] = '開',
        ['该'] = '該', ['来'] = '來', ['关'] = '關', ['于'] = '於', ['机'] = '機', ['启'] = '啟',
        ['栏'] = '欄', ['显'] = '顯', ['结'] = '結', ['声'] = '聲', ['钮'] = '鈕', ['无'] = '無',
        ['预'] = '預', ['览'] = '覽', ['划'] = '劃', ['钟'] = '鐘', ['驻'] = '駐', ['与'] = '與',
        ['闲'] = '閒', ['后'] = '後', ['远'] = '遠', ['单'] = '單', ['动'] = '動', ['简'] = '簡',
        ['体'] = '體', ['繁'] = '繁', ['活'] = '活', ['跃'] = '躍', ['网'] = '網', ['图'] = '圖',
        ['会'] = '會', ['从'] = '從', ['儿'] = '兒', ['几'] = '幾', ['场'] = '場', ['变'] = '變',
        ['发'] = '發', ['台'] = '臺', ['众'] = '眾', ['别'] = '別', ['内'] = '內', ['加'] = '加',
        ['悬'] = '懸', ['浮'] = '浮', ['窗'] = '窗', ['护'] = '護', ['眼'] = '眼', ['站'] = '站',
        ['颜'] = '顏', ['择'] = '擇',
    };
}

public enum AppLanguage
{
    System,
    English,
    /// 简体中文（存储值沿用 "chinese" 与 Mac 版兼容）
    Simplified,
    /// 繁体中文
    Traditional,
}
