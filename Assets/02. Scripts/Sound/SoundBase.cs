using UnityEngine;

/// <summary>
/// 사운드 공통 기반 - 재생 모듈 연결과 단발음, 반복음 재생 및 정리
/// </summary>
public abstract class SoundBase : MonoBehaviour
{
    SoundPlayer _soundPlayer;       //전용 사운드 재생 모듈

    /// <summary>
    /// 사운드 재생 모듈 연결
    /// </summary>
    /// <param name="soundPlayer">전용 사운드 재생 모듈</param>
    public virtual void Init ( SoundPlayer soundPlayer )
    {
        ResetSounds( );
        _soundPlayer = soundPlayer;
    }

    protected virtual void OnDisable ()
    {
        ResetSounds( );
    }


    /// <summary>
    /// 재생 사운드 정리
    /// </summary>
    public virtual void ResetSounds ()
    {
        //최초 활성화에서는 재생 모듈이 아직 연결되지 않을 수 있음
        if ( _soundPlayer != null )
            _soundPlayer.StopAll( );
    }


    /// <summary>
    /// 단발음 재생
    /// </summary>
    /// <param name="sound">사운드 설정</param>
    protected void PlaySound ( SoundData sound )
    {
        if ( isActiveAndEnabled == false || _soundPlayer == null )
            return;

        _soundPlayer.PlayOneShot( sound );
    }

    /// <summary>
    /// 반복음 재생
    /// </summary>
    /// <param name="sound">사운드 설정</param>
    protected void StartLoop ( SoundData sound )
    {
        if ( isActiveAndEnabled == false || _soundPlayer == null )
            return;

        _soundPlayer.StartLoop( sound );
    }

    /// <summary>
    /// 반복음 정지
    /// </summary>
    protected void StopLoop ()
    {
        //비활성화 경계에서도 기존 반복음 정리
        if ( _soundPlayer != null )
            _soundPlayer.StopLoop( );
    }
}
