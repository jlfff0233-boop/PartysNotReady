using UnityEngine;

/// <summary>
/// 곰 연출 설정 - 먹기, 만족, 분노, 공격과 포획 음원
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/Feedback/DemandFeedbackProfile" )]
public class DemandFeedbackProfile : ScriptableObject
{
    [SerializeField] SoundData _eatSound;		//먹기 반복음
    [SerializeField] SoundData _satisfiedSound;		//만족음
    [SerializeField] SoundData _madSound;		//분노음
    [SerializeField] SoundData _attackSound;		//공격음
    [SerializeField] SoundData _captureSound;		//포획 성공음


    /// <summary>
    /// 먹기 반복음
    /// </summary>
    public SoundData EatSound => _eatSound;

    /// <summary>
    /// 만족음
    /// </summary>
    public SoundData SatisfiedSound => _satisfiedSound;

    /// <summary>
    /// 분노음
    /// </summary>
    public SoundData MadSound => _madSound;

    /// <summary>
    /// 공격음
    /// </summary>
    public SoundData AttackSound => _attackSound;

    /// <summary>
    /// 포획 성공음
    /// </summary>
    public SoundData CaptureSound => _captureSound;
}
