
/// <summary>
/// 흔들림 변칙 사건 모델 - 조사 결과와 만료 판정
/// </summary>
public class ShakeAnomalyEvent : AnomalyEvent
{
    /// <summary>
    /// 흔들림 변칙 사건의 시간 설정 저장
    /// </summary>
    /// <param name="times">해결 시간과 사후 복구 시간</param>
    public ShakeAnomalyEvent ( AnomalyEventTimes times ) : base ( times )
    {
    }
    /// <summary>
    /// 조사 완료 결과 반영
    /// </summary>
    /// <param name="isCorrect">조사 완료 여부</param>
    /// <returns>해결 또는 사후 복구 성공 여부</returns>
    public bool TryInvestigate ( bool isCorrect )
    {
        //사후 처리 만료가 조사 완료보다 우선
        if ( TryExpire ( ) == true )
            return false;

        return TryComplete ( isCorrect );
    }
}
