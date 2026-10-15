using UnityEngine;

/// <summary>
/// 납치 적 사운드 - 발소리와 포획 성공음
/// </summary>
public class KidnapSound : CharacterSound
{
    [Header( "----- 행동 사운드 -----" )]
    [SerializeField] SoundData _captureSound;       //포획 성공음

    bool _isKidnapping;       //포획 대상 운반 여부


    /// <summary>
    /// 사운드와 포획 기록 초기화
    /// </summary>
    public override void ResetSounds ()
    {
        base.ResetSounds( );
        _isKidnapping = false;
    }

    /// <summary>
    /// 포획 대상 운반 상태와 성공음 적용
    /// </summary>
    /// <param name="isKidnapping">포획 대상 운반 여부</param>
    public void SetKidnap ( bool isKidnapping )
    {
        if ( _isKidnapping == isKidnapping )
            return;

        _isKidnapping = isKidnapping;

        //실제 대상 확보로 운반을 시작한 경우에만 재생
        if ( isKidnapping == true )
            PlaySound( _captureSound );
    }
}
