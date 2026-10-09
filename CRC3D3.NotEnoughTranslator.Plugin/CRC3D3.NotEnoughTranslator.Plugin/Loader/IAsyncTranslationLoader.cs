using System.Threading;
using System.Threading.Tasks;

namespace CRC3D3.NotEnoughTranslator.Plugin.Loader;

public interface IAsyncTranslationLoader
{
    /// <summary>
    /// 启动异步翻译资源加载，并通过任务返回加载结果。
    /// Start asynchronous translation-resource loading and return its result through a task.
    /// </summary>
    /// <param name="cancellationToken">加载操作的取消令牌 / The cancellation token for the loading operation.</param>
    /// <returns>表示资源加载结果的任务 / A task representing the resource-loading result.</returns>
    Task<TranslationLoadResult> StartLoadingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 请求取消当前加载操作。
    /// Request cancellation of the current loading operation.
    /// </summary>
    void Cancel();
}
