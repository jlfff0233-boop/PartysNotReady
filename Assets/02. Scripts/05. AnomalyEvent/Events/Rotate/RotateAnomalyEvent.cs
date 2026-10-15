
/// <summary>
/// 회전 변칙 사건 모델 - 초기 회전 복구에 따른 해결 판정
/// </summary>
public class RotateAnomalyEvent : AnomalyEvent
{
    /// <summary>
    /// 회전 변칙 사건의 해결 시간과 사후 복구 시간 설정
    /// </summary>
    /// <param name="times">해결 시간과 사후 복구 시간</param>
    public RotateAnomalyEvent ( AnomalyEventTimes times ) : base( times )
    {
    }

    /// <summary>
    /// 초기 회전값 복귀에 따른 해결/사후 복구
    /// </summary>
    /// <param name="angle">초기 회전을 기준으로 한 현재 각도(도)</param>
    /// <returns>해결/사후 복구 성공 여부</returns>
    public bool TryRestore ( float angle )
    {
        //사후 처리 만료가 수동 복구보다 우선
        if ( TryExpire( ) == true )
            return false;

        //회전 기능에서 복구 완료 시
        //각도를 정확히 0으로 맞추므로 초기 회전 복귀 여부만 판정
        return TryComplete( angle == 0f );
    }
}
