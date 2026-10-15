using UnityEngine;

/// <summary>
/// 변칙 사건 설정 - 해결 시간과 사후 복구 시간 보관
/// </summary>
[CreateAssetMenu ( menuName = "GameSettings/Anomaly/AnomalyEventSettings" )]
public class AnomalyEventSettings : ScriptableObject
{
    [SerializeField, Min ( 0.01f )] float _timeLimit = 10f;     //해결 제한 시간, 초
    [SerializeField, Min ( 0f )] float _recoveryTimeLimit = 15f;        //실패 후 사후 복구 제한 시간, 초

    /// <summary>
    /// 설정을 검사하고 발생 건에 사용할 시간 반환
    /// </summary>
    /// <param name="times">발생 건에 복사할 시간 값</param>
    /// <param name="reason">잘못된 설정 사유</param>
    /// <returns>유효한 시간 설정 여부</returns>
    public bool TryGetTimes (
        out AnomalyEventTimes times, out string reason )
    {
        //발생 건이 SO의 이후 변경에 영향을 받지 않도록 시간 값을 복사
        times = new AnomalyEventTimes( _timeLimit, _recoveryTimeLimit );
        return times.CheckValid( out reason );
    }
}
