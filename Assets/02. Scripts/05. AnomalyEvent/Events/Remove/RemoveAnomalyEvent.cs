using UnityEngine;

/// <summary>
/// 제거 변칙 사건 모델 - 발생 조건과 정상 복구 결과 판정
/// </summary>
public class RemoveAnomalyEvent : AnomalyEvent
{
    /// <summary>
    /// 제거 변칙 사건의 제한시간 초기화
    /// </summary>
    /// <param name="times">해결 시간과 사후 복구 시간</param>
    public RemoveAnomalyEvent ( AnomalyEventTimes times ) : base( times )
    {
    }


    /// <summary>
    /// 같은 조사에서 시작한 정상 복구 결과 반영
    /// </summary>
    /// <param name="targetId">사건의 조사 ID</param>
    /// <param name="restoredId">정상 복구 완료 알림의 조사 ID</param>
    /// <param name="isPresent">정상 물건 존재 여부</param>
    /// <param name="isVisible">실제 표시 여부</param>
    /// <param name="isRestoring">재충전 또는 복구 공간 대기 중 여부</param>
    /// <returns>해결 또는 사후 복구 성공 여부</returns>
    public bool TryRestore (
        int targetId, int restoredId, bool isPresent, bool isVisible, bool isRestoring )
    {
        //사후 처리 만료가 수동 복구보다 우선
        if ( TryExpire( ) == true )
            return false;

        //빈자리 조사만으로 해결하지 않고 정상 물건의 복구 완료까지 확인
        return TryComplete(
            targetId == restoredId && isPresent == true &&
            isVisible == true && isRestoring == false );
    }

    /// <summary>
    /// 제거 기능과 정상 잠금 및 제한시간 확인
    /// </summary>
    /// <param name="canRemove">복구용 제거 가능 여부</param>
    /// <param name="normalLocked">정상 상태 잠금 여부</param>
    /// <param name="timeLimit">해결 제한시간(초)</param>
    /// <returns>발생 가능 여부</returns>
    public static bool CanStart ( bool canRemove, bool normalLocked, float timeLimit )
    {
        return canRemove == true && normalLocked == false &&
               float.IsNaN( timeLimit ) == false &&
               float.IsInfinity( timeLimit ) == false && timeLimit > 0f;
    }

}
