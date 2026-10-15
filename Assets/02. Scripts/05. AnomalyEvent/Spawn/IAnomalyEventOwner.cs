/// <summary>
/// 적 사건 연결 - 발생 자격과 시작 결과 반영
/// </summary>
public interface IAnomalyEventOwner
{
    /// <summary>
    /// 자신의 사건 결과 연결
    /// </summary>
    AnomalyLinker EventLinker { get; }

    /// <summary>
    /// 사건 발생 자격과 거절 사유 검사
    /// </summary>
    /// <param name="room">요청 방</param>
    /// <param name="reason">거절 사유</param>
    /// <returns>시작 가능 여부</returns>
    bool CheckStart ( Room room, out string reason );

    /// <summary>
    /// 요청 방에서 사건을 받을 수 있는지 확인
    /// </summary>
    /// <param name="room">요청한 방</param>
    /// <returns>발생 자격 여부</returns>
    bool CanStart ( Room room );

    /// <summary>
    /// 실제 사건 시작 반영
    /// </summary>
    /// <param name="room">사건이 발생한 방</param>
    void HandleEventStarted ( Room room );
}
