using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 공통 연출 재생 - 행동별 사운드와 파티클의 재생, 정리
/// </summary>
[DisallowMultipleComponent]
public class FeedbackPlayer : MonoBehaviour
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] SoundPlayer _soundPlayer;		//사운드 재생기

    FeedbackCue _continuousCue;		//현재 요청된 지속 연출
    ParticleSystem _continuousParticle;		//현재 지속 파티클

    /// <summary>
    /// 재생한 파티클 목록, 전체 정리 시 입자를 제거할 대상
    /// </summary>
    List<ParticleSystem> _playedParticles = new( );


    void OnDisable ()
    {
        ResetFeedback( );
    }

    /// <summary>
    /// 파티클 재생 중단과 입자 제거
    /// </summary>
    /// <param name="particle">정리할 파티클</param>
    void StopParticle ( ParticleSystem particle )
    {
        //파티클이 없는 사운드 전용 행동도 허용
        if ( particle == null )
            return;

        particle.Stop(
            true, ParticleSystemStopBehavior.StopEmittingAndClear );
    }

    /// <summary>
    /// 파티클을 기록하고 처음부터 재생
    /// </summary>
    /// <param name="particle">재생할 파티클</param>
    void PlayParticle ( ParticleSystem particle )
    {
        if ( particle == null )
            return;

        if ( _playedParticles.Contains( particle ) == false )
            _playedParticles.Add( particle );

        StopParticle( particle );
        particle.Play( true );
    }


    /// <summary>
    /// 단발 사운드와 선택 파티클 재생
    /// </summary>
    /// <param name="sound">단발 사운드</param>
    /// <param name="particle">선택 파티클</param>
    public void Play ( SoundData sound, ParticleSystem particle = null )
    {
        _soundPlayer.PlayOneShot( sound );
        PlayParticle( particle );
    }

    /// <summary>
    /// 해당 지속 연출 종료
    /// </summary>
    /// <param name="cue">종료할 행동 종류</param>
    public void End ( FeedbackCue cue )
    {
        //다른 행동의 종료 요청은 현재 반복음을 끊지 않음
        if ( _continuousCue == FeedbackCue.None || _continuousCue != cue )
            return;

        _continuousCue = FeedbackCue.None;
        _soundPlayer.StopLoop( );
        StopParticle( _continuousParticle );
        _continuousParticle = null;
    }

    /// <summary>
    /// 이전 지속 행동을 종료하고 새 사운드와 파티클 시작
    /// </summary>
    /// <param name="cue">행동 종류</param>
    /// <param name="sound">반복 사운드</param>
    /// <param name="particle">선택 파티클</param>
    public void Begin (
        FeedbackCue cue, SoundData sound, ParticleSystem particle = null )
    {
        //같은 요청은 사운드와 파티클을 재시작하지 않음
        if ( _continuousCue == cue )
            return;

        End( _continuousCue );

        _continuousCue = cue;
        _continuousParticle = particle;
        _soundPlayer.StartLoop( sound );
        PlayParticle( particle );
    }

    /// <summary>
    /// 소유 사운드와 파티클, 지속 요청 기록 정리
    /// </summary>
    public void ResetFeedback ()
    {
        _continuousCue = FeedbackCue.None;
        _continuousParticle = null;
        _soundPlayer.StopAll( );

        for ( int i = 0; i < _playedParticles.Count; i++ )
            StopParticle( _playedParticles [ i ] );

        _playedParticles.Clear( );
    }

}
