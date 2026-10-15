using UnityEngine;

/// <summary>
/// 분위기 변화 설정
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/GameFlow/MoodData" )]
public class MoodData : ScriptableObject
{
    [SerializeField, Range( 0f, 100f )] float _initialMood = 100f;      //시작 분위기

    [Header( "----- 회복 -----" )]
    [SerializeField, Range( 4f, 8f )] float _missionRecovery = 4f;      //미션 진행 시 회복량
    [SerializeField, Min( 0f )] float _resolvedRecovery;        //사건 해결 회복량
    [SerializeField, Min( 0f )] float _rescueRecovery;      //NPC 구조 회복량

    [Header( "----- 감소 -----" )]
    [SerializeField, Min( 0f )] float _failedDecrease;      //사건 실패 감소량
    [SerializeField, Min( 0f )] float _departureDecrease;		//NPC 이탈 감소량
    [SerializeField, Min( 0f )] float _decrease;        //대기 이후 초당 감소량
    [SerializeField, Min( 0f )] float _decreaseDelay;       //마지막 미션 진행 후 감소 대기시간


    /// <summary>
    /// 시작 분위기
    /// </summary>
    public float InitialMood => _initialMood;

    /// <summary>
    /// 미션 진행 시 회복량
    /// </summary>
    public float MissionRecovery => _missionRecovery;

    /// <summary>
    /// 자연 감소 대기시간
    /// </summary>
    public float DecreaseDelay => _decreaseDelay;

    /// <summary>
    /// 초당 자연 감소량
    /// </summary>
    public float Decrease => _decrease;

    /// <summary>
    /// 사건 해결 회복량
    /// </summary>
    public float ResolvedRecovery => _resolvedRecovery;

    /// <summary>
    /// 사건 실패 감소량
    /// </summary>
    public float FailedDecrease => _failedDecrease;

    /// <summary>
    /// NPC 구조 회복량
    /// </summary>
    public float RescueRecovery => _rescueRecovery;

    /// <summary>
    /// NPC 이탈 감소량
    /// </summary>
    public float DepartureDecrease => _departureDecrease;
}