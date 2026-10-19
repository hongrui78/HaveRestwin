using System;
using System.Threading;

namespace HaveRestwin;

/// 命名 Mutex 实现的单实例互斥。进程退出（含崩溃）时内核自动释放。
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex mutex;

    private SingleInstance(Mutex m) => mutex = m;

    /// 抢到返回实例（须持有到进程结束）；已有实例在跑返回 null。
    public static SingleInstance? TryAcquire()
    {
        var m = new Mutex(initiallyOwned: false, @"Local\HaveARestWin_SingleInstance");
        try
        {
            if (m.WaitOne(0, false)) return new SingleInstance(m);
            m.Dispose();
            return null;
        }
        catch (AbandonedMutexException)
        {
            // 上一实例异常退出遗留的 mutex，抢到了照常继续
            return new SingleInstance(m);
        }
    }

    public void Dispose() => mutex.Dispose();
}
