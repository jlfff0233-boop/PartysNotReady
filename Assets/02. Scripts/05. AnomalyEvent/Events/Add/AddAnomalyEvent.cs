
/// <summary>
/// 아이템 추가 변칙 사건 모델 - 발생 조건, 조사 제거 판정
/// </summary>
public class AddAnomalyEvent : AnomalyEvent
{
    /// <summary>
    /// 아이템 추가 변칙 사건 생성
    /// </summary>
    /// <param name="times">해결 시간과 사후 복구 시간</param>
    public AddAnomalyEvent ( AnomalyEventTimes times ) : base( times )
    {
    }


    /// <summary>
    /// 해당 물건의 조사 제거 결과 반영
    /// </summary>
    /// <param name="targetId">사건 발생 때 저장한 조사 ID</param>
    /// <param name="investigatedId">제거 완료된 조사 ID</param>
    /// <param name="isPresent">제거 후 물건 존재 여부</param>
    /// <param name="isVisible">제거 후 실제 표시 여부</param>
    /// <returns>해결 또는 사후 복구 여부</returns>
    public bool TryRemove (
        int targetId, int investigatedId,
        bool isPresent, bool isVisible )
    {
        //사후 처리 만료가 수동 복구보다 우선
        if ( TryExpire( ) == true )
            return false;

        //같은 조사로 물건의 존재와 렌더러 활성 상태가 모두 해제돼야 해결
        return TryComplete(
            targetId == investigatedId &&
            isPresent == false &&
            isVisible == false );
    }

    /// <summary>
    /// 물건을 등장시킬 수 있는 상태와 제한시간 확인
    /// </summary>
    /// <param name="isReady">등장 기능 준비 여부</param>
    /// <param name="isPresent">물건 존재 여부</param>
    /// <param name="isVisible">실제 표시 여부</param>
    /// <param name="normalLocked">다른 사건의 잠금 여부</param>
    /// <param name="timeLimit">해결 제한시간, 초</param>
    /// <returns>발생 가능 여부</returns>
    public static bool CanStart (
        bool isReady, bool isPresent, bool isVisible,
        bool normalLocked, float timeLimit )
    {
        return isReady == true &&
               isPresent == false &&
               isVisible == false &&
               normalLocked == false &&
               float.IsNaN( timeLimit ) == false &&
               float.IsInfinity( timeLimit ) == false &&
               timeLimit > 0f;
    }

}
