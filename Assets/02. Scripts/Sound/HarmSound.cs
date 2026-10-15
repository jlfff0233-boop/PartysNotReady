using UnityEngine;

/// <summary>
/// 공격 적 사운드 - 발소리와 수집, 공격음
/// </summary>
public class HarmSound : CharacterSound
{
    [Header( "----- 행동 사운드 -----" )]
    [SerializeField] SoundData _collectSound;       //수집 반복음
    [SerializeField] SoundData _attackSound;       //공격음

    /// <summary>
    /// 공격음 재생
    /// </summary>
    public void PlayAttack ()
    {
        PlaySound( _attackSound );
    }

    /// <summary>
    /// 수집 반복음 재생 상태 적용
    /// </summary>
    /// <param name="isCollecting">수집 진행 여부</param>
    public void SetCollecting ( bool isCollecting )
    {
        if ( isCollecting == true )
            StartLoop( _collectSound );
        else
            StopLoop( );
    }
}
