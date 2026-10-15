/// <summary>
/// 사건 시간 값 - 발생 시 복사한 해결 시간과 사후 복구 시간
/// </summary>
public struct AnomalyEventTimes
{
    float _timeLimit;       //해결 제한 시간, 초
    float _recoveryTimeLimit;       //실패 후 사후 복구 제한 시간, 초

    /// <summary>
    /// 해결 제한 시간
    /// </summary>
    public float TimeLimit => _timeLimit;

    /// <summary>
    /// 실패 이후 복구 제한 시간
    /// </summary>
    public float RecoveryTimeLimit => _recoveryTimeLimit;

    /// <summary>
    /// 발생 건에 사용할 시간 값 저장
    /// </summary>
    /// <param name="timeLimit">해결 제한 시간(초)</param>
    /// <param name="recoveryTimeLimit">실패 이후 복구 제한 시간(초)</param>
    public AnomalyEventTimes ( float timeLimit , float recoveryTimeLimit )
    {
        _timeLimit = timeLimit;
        _recoveryTimeLimit = recoveryTimeLimit;
    }

    /// <summary>
    /// 해결 시간과 사후 복구 시간의 유효성 확인
    /// </summary>
    /// <param name="reason">잘못된 설정 사유</param>
    /// <returns>유효한 시간 설정 여부</returns>
    public bool CheckValid ( out string reason )
    {
        reason = string.Empty;

        //사건을 진행할 해결 시간은 반드시 양수
        if ( float.IsNaN ( TimeLimit ) == true ||
             float.IsInfinity ( TimeLimit ) == true || TimeLimit <= 0f )
        {
            reason = "해결 제한 시간은 유한한 양수여야 합니다.";
            return false;
        }

        //0초이면 실패 직후부터 만료 처리가 가능하도록 허용
        if ( float.IsNaN ( RecoveryTimeLimit ) == true ||
             float.IsInfinity ( RecoveryTimeLimit ) == true || RecoveryTimeLimit < 0f )
        {
            reason = "사후 복구 제한 시간은 유한한 0 이상의 값이어야 합니다.";
            return false;
        }

        return true;
    }
}
