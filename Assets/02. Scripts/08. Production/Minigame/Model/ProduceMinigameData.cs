using UnityEngine;

/// <summary>
/// 제작 미니게임 타입
/// </summary>
public enum ProduceMinigameType
{
    Timing,     //타이밍에 맞게 클릭
    ClickArea       //나타난 히트 포인트를 클릭
}

/// <summary>
/// 제작 미니게임 데이터 - 진행 횟수, 판정 구간과 실패 손실
/// </summary>
[CreateAssetMenu ( menuName = "GameSettings/Production/ProduceMinigameData" )]
public class ProduceMinigameData : ScriptableObject
{
    [SerializeField] ProduceMinigameType _actionType;     //제작 미니게임 타입
    [SerializeField, Min ( 1 )] int _requiredSuccessCount = 3;       //완성에 필요한 성공 횟수
    [SerializeField, Min ( 0.01f )] float _actionDuration = 2f;       //한 회차 제한시간
    [SerializeField, Min ( 0.01f )] float _handleTravelDuration = 1f;       //핸들 편도 이동시간
    [SerializeField, Range ( 0f , 1f )] float _successRangeStart = 0.4f;       //성공 구간 시작
    [SerializeField, Range ( 0f , 1f )] float _successRangeEnd = 0.6f;       //성공 구간 종료
    [SerializeField, Min ( 1 )] int _failureLossCount = 1;       //실패 시 소실 수량
    [SerializeField, Min ( 0.01f )] float _hitPointSpawnInterval = 0.5f;       //목표 출현 간격
    [SerializeField, Min ( 0.01f )] float _hitPointLifetime = 1.5f;       //목표 개별 유지시간

    /// <summary>
    /// 제작 미니게임 타입
    /// </summary>
    public ProduceMinigameType MinigameType => _actionType;

    /// <summary>
    /// 완성에 필요한 성공 횟수
    /// </summary>
    public int RequiredSuccessCount =>
        Mathf.Max ( 1 , _requiredSuccessCount );

    /// <summary>
    /// 제작 미니게임 제한시간(1회당)
    /// </summary>
    public float AttemptDuration => Mathf.Max ( 0.01f , _actionDuration );

    /// <summary>
    /// 핸들 편도 이동시간
    /// </summary>
    public float HandleTravelDuration => Mathf.Max ( 0.01f , _handleTravelDuration );

    /// <summary>
    /// 성공 구간 시작
    /// </summary>
    public float SuccessRangeStart => Mathf.Clamp01 ( _successRangeStart );

    /// <summary>
    /// 성공 구간 종료
    /// </summary>
    public float SuccessRangeEnd =>
        Mathf.Clamp ( _successRangeEnd , SuccessRangeStart , 1f );

    /// <summary>
    /// 실패 시 소실 수량
    /// </summary>
    public int FailureLossCount => Mathf.Max ( 1 , _failureLossCount );

    /// <summary>
    /// 목표 출현 간격
    /// </summary>
    public float HitPointSpawnInterval => Mathf.Max ( 0.01f , _hitPointSpawnInterval );

    /// <summary>
    /// 목표 개별 유지시간
    /// </summary>
    public float HitPointLifetime => Mathf.Max ( 0.01f , _hitPointLifetime );

}
