using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Threading;

namespace HaveRestwin;

/// 计时核心：眼睛/站立周期、空闲检测与暂停恢复。移植自 Mac 版 RestEngine.swift。
/// 全部在 UI 线程运行（DispatcherTimer），与 WPF 界面同线程，无需加锁。
public sealed class RestEngine : INotifyPropertyChanged
{
    public enum State { Active, Paused, RestingEye, StandPending }

    public static RestEngine Shared { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly SettingsStore settings = SettingsStore.Shared;
    private DispatcherTimer? ticker;
    private bool autoPausedByIdle;
    /// 上一秒的闲置秒数，用来识别「离开很久之后回来操作」
    private double lastIdleSeconds;
    /// 进入暂停状态的时刻。用墙钟而非每秒累加，避免系统睡眠时统计不到
    private DateTime? pauseStartedAt;

    /// 连续离开超过这个时长，回来操作时眼睛和站立周期都从头计
    public const double AbsenceResetAfter = 5 * 60;
    /// 暂停状态持续超过这个时长，再次操作时眼睛和站立计时从头计
    public const double PausedResetAfter = 5 * 60;
    /// 眼睛休息和站立提醒挤在这个窗口内就只提示站立，不连着弹两个面板
    public const int MergeWindowSeconds = 60;

    private State status = State.Active;
    private int eyeRemaining;
    private int eyeTotal;
    private int standRemaining;

    public State Status
    {
        get => status;
        private set { if (status == value) return; status = value; OnChanged(); OnChanged(nameof(StatusText)); }
    }
    public int EyeRemaining
    {
        get => eyeRemaining;
        private set { if (eyeRemaining == value) return; eyeRemaining = value; OnChanged(); OnChanged(nameof(StatusText)); OnChanged(nameof(Fraction)); }
    }
    public int EyeTotal
    {
        get => eyeTotal;
        private set { if (eyeTotal == value) return; eyeTotal = value; OnChanged(); }
    }
    public int StandRemaining
    {
        get => standRemaining;
        private set { if (standRemaining == value) return; standRemaining = value; OnChanged(); }
    }

    public double Fraction => EyeTotal > 0 ? Math.Clamp((double)EyeRemaining / EyeTotal, 0, 1) : 0;

    private RestEngine()
    {
        EyeTotal = settings.EyeFrequencyMinutes * 60;
        EyeRemaining = EyeTotal;
        StandRemaining = settings.StandMinutes * 60;
        settings.Changed += SettingsChanged;
    }

    public void Start()
    {
        ticker = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        ticker.Tick += (_, _) => Tick();
        ticker.Start();
    }

    private void Tick()
    {
        double idleSeconds = NativeMethods.SecondsSinceUserInput();
        if (DidReturnFromAbsence(lastIdleSeconds, idleSeconds, AbsenceResetAfter))
            RearmAfterAbsence();
        lastIdleSeconds = idleSeconds;

        RefreshIdlePauseState(idleSeconds);

        if (!RestOverlayController.Shared.IsClosed) return;

        if (Status == State.Active)
        {
            EyeRemaining--;
            if (EyeRemaining <= 0)
            {
                if (settings.StandEnabled && StandRemaining <= MergeWindowSeconds)
                {
                    Reset();
                    Status = State.StandPending;
                }
                else
                {
                    BeginEyeBreak();
                }
            }
        }
        // 站立周期独立计时，只有站完才归零；pending 期间不再累减，避免倒计时变负数
        if (settings.StandEnabled && Status == State.Active)
        {
            StandRemaining--;
            if (StandRemaining <= 0) Status = State.StandPending;
        }
    }

    /// 闲置秒数突然归零 = 刚有操作；上一秒已经攒到阈值 = 离开够久后回来
    public static bool DidReturnFromAbsence(double lastIdle, double nowIdle, double threshold) =>
        nowIdle < lastIdle - 1 && lastIdle >= threshold;

    /// 离开归来：两个周期都从头计。状态保持原样，手动暂停不会被偷偷恢复
    private void RearmAfterAbsence()
    {
        EyeTotal = Math.Max(10, settings.EyeFrequencyMinutes * 60);
        EyeRemaining = EyeTotal;
        RearmStandTimer();
    }

    private void RefreshIdlePauseState(double idleSeconds)
    {
        if (idleSeconds >= SettingsStore.IdlePauseMinutes * 60.0)
        {
            if (!autoPausedByIdle && Status == State.Active)
            {
                Pause();
                autoPausedByIdle = true;
            }
        }
        else if (autoPausedByIdle)
        {
            autoPausedByIdle = false;
            ResumeFromIdlePause();
        }
    }

    /// 从空闲自动暂停中恢复：若这段暂停已攒够 PausedResetAfter，把眼睛和站立计时都从头计
    private void ResumeFromIdlePause()
    {
        if (pauseStartedAt != null && DateTime.Now - pauseStartedAt >= TimeSpan.FromSeconds(PausedResetAfter))
            RearmAfterAbsence();
        Resume();
    }

    private void BeginEyeBreak()
    {
        Status = State.RestingEye;
        RestOverlayController.Shared.BeginEyeBreak(settings.EyeMessage, settings.EyeBreakSeconds, () =>
        {
            PlaySoundIfNeeded();
            Reset();
        });
    }

    private static MediaPlayer? soundPlayer;

    private static void PlaySoundIfNeeded()
    {
        if (!SettingsStore.Shared.PlaySoundOnBreakEnd) return;
        try
        {
            var mediaDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media");
            foreach (var name in new[] { "Alarm07.wav", "Windows Notify.wav", "ding.wav" })
            {
                var wav = Path.Combine(mediaDir, name);
                if (!File.Exists(wav)) continue;
                soundPlayer ??= new MediaPlayer();
                soundPlayer.Open(new Uri(wav));
                soundPlayer.Play();
                return;
            }
        }
        catch
        {
            // 提示音失败不该影响休息流程
        }
    }

    /// 用户点「立刻休息」：无固定时长的休息面板，退出后重置计时
    public void ManualRestNow()
    {
        if (!RestOverlayController.Shared.IsClosed) return;
        Status = State.RestingEye;
        RestOverlayController.Shared.BeginManualBreak(settings.EyeMessage, settings.EyeBreakSeconds, () =>
        {
            PlaySoundIfNeeded();
            Reset();
        });
    }

    /// 站立提醒到点时弹出，由用户点「退出」或倒计时结束
    public void BeginStandBreak()
    {
        if (!RestOverlayController.Shared.IsClosed) return;
        RestOverlayController.Shared.BeginStandBreak(settings.StandMessage, settings.StandBreakSeconds, () =>
        {
            PlaySoundIfNeeded();
            RearmStandTimer();
            if (Status == State.StandPending) Status = State.Active;
        });
    }

    private void RearmStandTimer() =>
        StandRemaining = Math.Max(60, settings.StandMinutes * 60);

    /// 重置眼睛周期。站立周期不跟着归零，否则「眼睛 22 分钟」会不断刷新「站立 45 分钟」，站立提醒永远到不了点
    public void Reset()
    {
        EyeTotal = Math.Max(10, settings.EyeFrequencyMinutes * 60);
        EyeRemaining = EyeTotal;
        autoPausedByIdle = false;
        pauseStartedAt = null;
        Status = State.Active;
    }

    /// 面板「重置」按钮专用：眼睛与站立两个周期同时从头计
    public void ResetAll()
    {
        Reset();
        RearmStandTimer();
    }

    public void Pause()
    {
        if (Status != State.Active) return;
        Status = State.Paused;
        pauseStartedAt = DateTime.Now;
    }

    public void Resume()
    {
        if (Status != State.Paused) return;
        pauseStartedAt = null;
        autoPausedByIdle = false;
        Status = State.Active;
    }

    public void TogglePause()
    {
        if (Status == State.Paused) Resume();
        else if (Status == State.Active) Pause();
    }

    /// 设置变化后对齐总量
    private void SettingsChanged()
    {
        EyeTotal = Math.Max(10, settings.EyeFrequencyMinutes * 60);
        if (EyeRemaining > EyeTotal) EyeRemaining = EyeTotal;

        int standTotal = Math.Max(60, settings.StandMinutes * 60);
        if (settings.StandEnabled)
        {
            StandRemaining = Math.Min(StandRemaining, standTotal);
        }
        else
        {
            // 关闭时清掉挂起状态并重新武装，避免状态卡在 StandPending、眼睛计时停住；
            // 重新打开则是完整的新周期，而不是立刻弹面板
            if (Status == State.StandPending) Status = State.Active;
            RearmStandTimer();
        }
    }

    /// 悬浮球上的状态文案，对应 Mac 的 menuBarTimeText
    public string StatusText => Status switch
    {
        State.Paused => L10n.E("Paused", "已暂停"),
        State.RestingEye => L10n.E("Resting", "休息中"),
        State.StandPending => L10n.E("Stand Time", "该站起来了"),
        _ => Mmss(Math.Max(0, EyeRemaining)),
    };

    /// 分钟也补零，配合等宽数字让宽度恒定，不会每秒左右晃
    public static string Mmss(int seconds)
    {
        int s = Math.Max(0, seconds);
        return $"{s / 60:00}:{s % 60:00}";
    }

    private void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
