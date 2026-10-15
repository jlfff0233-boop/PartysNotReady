
/// <summary>
/// 이동 변칙 사건 모델 - 발생 조건과 재배치 해결 판정
/// </summary>
public class DisplaceAnomalyEvent : AnomalyEvent
{
    /// <summary>
    /// 이동 변칙 사건의 해결 시간과 사후 복구 시간 설정
    /// </summary>
    /// <param name="times">해결 시간과 사후 복구 시간</param>
    public DisplaceAnomalyEvent ( AnomalyEventTimes times ) : base ( times )
    {
    }

    /// <summary>
    /// 실제 배치 완료 결과로 해결 또는 사후 복구
    /// </summary>
    /// <param name="hasPlacement">배치 대상 존재 여부</param>
    /// <param name="isHeld">현재 보유 여부</param>
    /// <returns>해결 또는 복구 성공 여부</returns>
    public bool TryPlace ( bool hasPlacement , bool isHeld )
    {
        //사후 처리 만료가 재배치 완료보다 우선
        if ( TryExpire ( ) == true )
            return false;

        //원래 위치와 비교하지 않고
        //실제 배치와 보유 해제 여부로 판정
        return TryComplete ( hasPlacement == true && isHeld == false );
    }

    /// <summary>
    /// 이동 변칙 사건의 정상 기준과 제한 시간 확인
    /// </summary>
    /// <param name="hasNormal">정상 기준 존재 여부</param>
    /// <param name="normalLocked">정상 기준 잠금 여부</param>
    /// <param name="timeLimit">해결 제한 시간(초)</param>
    /// <param name="reason">발생 불가 사유</param>
    /// <returns>발생 조건 충족 여부</returns>
    public static bool CanStart (
        bool hasNormal , bool normalLocked , float timeLimit ,
        out string reason )
    {
        reason = null;

        if ( hasNormal == false )
        {
            reason = "정상 기준이 없습니다.";
            return false;
        }

        if ( normalLocked == true )
        {
            reason = "다른 사건이 정상 기준을 잠그고 있습니다.";
            return false;
        }

        if ( float.IsNaN ( timeLimit ) || float.IsInfinity ( timeLimit ) || timeLimit <= 0f )
        {
            reason = "제한 시간은 유효한 양수여야 합니다.";
            return false;
        }

        return true;
    }
}
