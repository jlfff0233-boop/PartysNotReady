
/// <summary>
/// 변칙 발생 대상 - 방별 발생 자격과 시작 요청
/// </summary>
public interface IAnomalySpawnTarget
{
    /// <summary>
    /// 발생 자격과 거절 사유 검사
    /// </summary>
    /// <param name="room">요청 방</param>
    /// <param name="reason">거절 사유</param>
    /// <returns>시작 가능 여부</returns>
    bool CheckStart ( Room room, out string reason );

    /// <summary>
    /// 실제 시작과 실패 사유 조회
    /// </summary>
    /// <param name="room">요청 방</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>시작 성공 여부</returns>
    bool TryStart ( Room room, out string reason );

    /// <summary>
    /// 요청 방의 발생 자격 검사
    /// </summary>
    /// <param name="room">요청한 방</param>
    /// <returns>시작 가능 여부</returns>
    bool CanStart ( Room room );

    /// <summary>
    /// 요청 방에서 발생 시작
    /// </summary>
    /// <param name="room">요청한 방</param>
    /// <returns>실제 시작 여부</returns>
    bool TryStart ( Room room );

}
