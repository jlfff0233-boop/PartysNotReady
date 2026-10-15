using UnityEngine;

/// <summary>
/// 변칙 발생 압력 설정 - 증가, 판정과 재발생 시간 보관
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/Anomaly/PressureData" )]
public class AnomalyPressureData : ScriptableObject
{
    [Header( "----- 압력 -----" )]
    [SerializeField, Min( 0.01f )] float _maxPressure;		//방별 최대 압력
    [SerializeField, Range( 0f, 1f )] float _pressureChanceScale;		//압력 비율에 곱할 일반 발생 확률 계수
    [SerializeField, Min( 0f )] float _threshold;		//우선 발생 임계치
    [SerializeField, Min( 0.01f )] float _checkInterval;		//발생 판정 간격
    [SerializeField, Min( 0f )] float _decreaseOnSuccess;		//성공 후 감소량

    [Header( "----- 증가량 -----" )]
    [SerializeField, Min( 0f )] float _increase;		//초당 기본 증가량
    [SerializeField, Min( 0f )] float _unvisitedBonus;		//플레이어가 없는 방의 초당 추가 증가량

    [Header( "----- 대기시간 -----" )]
    [SerializeField, Min( 0f )] float _roomCooldown;		//방별 재발생 대기시간
    [SerializeField, Min( 0f )] float _globalCooldown;		//전체 재발생 대기시간

    /// <summary>
    /// 방별 최대 압력
    /// </summary>
    public float MaxPressure => _maxPressure;

    /// <summary>
    /// 초당 기본 증가량
    /// </summary>
    public float Increase => _increase;

    /// <summary>
    /// 플레이어가 없는 방의 초당 추가 증가량
    /// </summary>
    public float UnvisitedBonus => _unvisitedBonus;

    /// <summary>
    /// 우선 발생 임계치
    /// </summary>
    public float Threshold => _threshold;

    /// <summary>
    /// 발생 판정 간격
    /// </summary>
    public float CheckInterval => _checkInterval;

    /// <summary>
    /// 압력 비율에 곱할 일반 발생 확률 계수
    /// </summary>
    public float PressureChanceScale => _pressureChanceScale;

    /// <summary>
    /// 발생 성공 후 압력 감소량
    /// </summary>
    public float DecreaseOnSuccess => _decreaseOnSuccess;

    /// <summary>
    /// 방별 재발생 대기시간
    /// </summary>
    public float RoomCooldown => _roomCooldown;

    /// <summary>
    /// 전체 재발생 대기시간
    /// </summary>
    public float GlobalCooldown => _globalCooldown;


    /// <summary>
    /// 압력 설정 검사
    /// </summary>
    /// <param name="reason">설정 오류</param>
    /// <returns>설정 유효 여부</returns>
    public bool CheckSettings ( out string reason )
    {
        reason = string.Empty;

        if ( IsNonNegative( _maxPressure ) == false || _maxPressure == 0f ||
             IsNonNegative( _threshold ) == false || _threshold == 0f ||
             _threshold > _maxPressure )
            reason = "최대 압력과 임계치는 양수이며 임계치는 최대 압력 이하여야 합니다.";

        else if ( IsNonNegative( _checkInterval ) == false || _checkInterval == 0f )
            reason = "발생 판정 간격은 유한한 양수여야 합니다.";

        else if ( IsNonNegative( _pressureChanceScale ) == false || _pressureChanceScale > 1f )
            reason = "일반 발생 확률 계수는 0~1이어야 합니다.";

        else if ( IsNonNegative( _increase ) == false ||
                  IsNonNegative( _unvisitedBonus ) == false ||
                  IsNonNegative( _decreaseOnSuccess ) == false )
            reason = "압력 증가량과 감소량은 유한한 0 이상 값이어야 합니다.";

        else if ( IsNonNegative( _roomCooldown ) == false ||
                  IsNonNegative( _globalCooldown ) == false )
            reason = "재발생 대기시간은 유한한 0 이상 값이어야 합니다.";

        return reason.Length == 0;
    }

    /// <summary>
    /// 유한한 0 이상 값 검사
    /// </summary>
    /// <param name="value">검사할 값</param>
    /// <returns>조건 충족 여부</returns>
    bool IsNonNegative ( float value )
    {
        return float.IsNaN( value ) == false &&
            float.IsInfinity( value ) == false && value >= 0f;
    }
}
