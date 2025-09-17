using System.Threading;
using System.Threading.Tasks;

namespace HushOps.Core.Humanization;

/// <summary>
/// 延时管理器接口（Core 抽象）。
/// - 统一管理拟人化等待；
/// - 面向“节律”而非“等待外部事件”，重型等待由专门服务处理。
/// </summary>
public interface IDelayManager
{
    Task WaitAsync(HumanWaitType waitType, int attemptNumber = 1, CancellationToken cancellationToken = default);
}

