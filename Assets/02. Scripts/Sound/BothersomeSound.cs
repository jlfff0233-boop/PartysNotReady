using UnityEngine;

/// <summary>
/// 방해 적 사운드 - 발소리와 요구, 만족 반응
/// </summary>
public class BothersomeSound : CharacterSound
{
    [Header( "----- 행동 사운드 -----" )]
    [SerializeField] SoundData _requestSound;       //요구
    [SerializeField] SoundData _satisfiedSound;       //만족

    /// <summary>
    /// 요구음 재생
    /// </summary>
    public void PlayRequest ()
    {
        PlaySound( _requestSound );
    }

    /// <summary>
    /// 만족음 재생
    /// </summary>
    public void PlaySatisfied ()
    {
        PlaySound( _satisfiedSound );
    }
}
