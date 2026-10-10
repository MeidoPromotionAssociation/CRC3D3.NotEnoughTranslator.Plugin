using System;
using System.Threading;
using System.Threading.Tasks;
using CRC3D3.NotEnoughTranslator.Plugin.Utils;

namespace CRC3D3.NotEnoughTranslator.Plugin.Loader;

internal sealed class ResourceModule<T> : IDisposable
{
    private readonly string _name;
    private readonly Func<CancellationToken, Task<T>> _load;
    private readonly Action<T> _apply;
    private CancellationTokenSource _cancellation;
    private bool _disposed;

    public bool IsLoading => _cancellation != null;
    public bool IsLoaded { get; private set; }

    /// <summary>
    /// 创建资源模块，绑定异步加载函数和完成后的资源发布回调。
    /// Create a resource module with an asynchronous loader and a result-publication callback.
    /// </summary>
    /// <param name="name">用于日志标识的模块名称 / The module name used in logs.</param>
    /// <param name="load">接收取消令牌并返回资源任务的加载函数 / The loader accepting a cancellation token and returning a resource task.</param>
    /// <param name="apply">在调用方同步上下文中发布结果的回调 / The callback publishing results on the caller's synchronization context.</param>
    public ResourceModule(string name, Func<CancellationToken, Task<T>> load, Action<T> apply)
    {
        _name = name;
        _load = load;
        _apply = apply;
    }

    /// <summary>
    /// 在 Unity 主线程启动新加载，请求取消旧加载并保留已发布资源。
    /// Start a new load on Unity's main thread, requesting cancellation of the old load while retaining published resources.
    /// </summary>
    public void Reload()
    {
        if (_disposed) return;
        CancelPending();
        _cancellation = new CancellationTokenSource();
        _ = LoadAsync(_cancellation);
    }

    /// <summary>
    /// 等待加载完成，在原同步上下文中发布仍有效的结果，并释放本轮取消源。
    /// Await loading, publish only a current result on the captured context, and dispose this load's cancellation source.
    /// </summary>
    /// <param name="cancellation">本轮加载独占的取消源，由此方法释放 / The cancellation source owned and disposed by this load.</param>
    /// <returns>表示本轮加载及收尾处理的任务 / A task representing this load and its cleanup.</returns>
    private async Task LoadAsync(CancellationTokenSource cancellation)
    {
        try
        {
            var result = await _load(cancellation.Token);
            if (_disposed || _cancellation != cancellation || cancellation.IsCancellationRequested) return;
            _apply(result);
            IsLoaded = true;
        }
        catch (OperationCanceledException)
        {
            LogManager.Debug($"[{_name}] Loading cancelled/加载已取消");
        }
        catch (Exception exception)
        {
            LogManager.Error($"[{_name}] Load failed; previous resources retained/加载失败，保留上次资源: {exception}");
        }
        finally
        {
            if (_cancellation == cancellation) _cancellation = null;
            cancellation.Dispose();
        }
    }

    /// <summary>
    /// 停用模块并请求取消加载，阻止后续结果回写。
    /// Dispose the module and request cancellation to prevent later result publication.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelPending();
        IsLoaded = false;
    }

    /// <summary>
    /// 撤销当前加载的发布资格并请求取消，由完成回调负责释放取消源。
    /// Invalidate the current load and request cancellation; its completion path disposes the source.
    /// </summary>
    private void CancelPending()
    {
        var cancellation = _cancellation;
        _cancellation = null;
        cancellation?.Cancel();
    }
}
