
/// <summary>
/// 아이템 폐기 대상 - 폐기 가능 상태와 처리 요청 제공
/// </summary>
public interface IItemDisposable
{
    /// <summary>
    /// 현재 폐기 가능 여부
    /// </summary>
    bool CanDispose { get; }

    /// <summary>
    /// 현재 아이템 폐기 처리
    /// </summary>
    /// <param name="reason">폐기 실패 사유</param>
    /// <returns>폐기 처리 여부</returns>
    bool TryDispose ( out string reason );
}