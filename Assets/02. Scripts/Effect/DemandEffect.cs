using UnityEngine;

/// <summary>
/// 곰 연출 - 먹기와 분노 파티클 재생, 정리
/// </summary>
[DisallowMultipleComponent]
public class DemandEffect : MonoBehaviour
{
    [SerializeField] ParticleSystem _madParticle;		//분노 효과

    ParticleSystem _eatParticle;		//기존 먹기 효과

    /// <summary>
    /// 기존 먹기 효과 연결
    /// </summary>
    /// <param name="eatParticle">곰에 할당된 부스러기 파티클</param>
    public void Init ( ParticleSystem eatParticle )
    {
        _eatParticle = eatParticle;
        StopAll( );
    }

    private void OnEnable ()
    {
        StopAll( );
    }

    private void OnDisable ()
    {
        StopAll( );
    }


    /// <summary>
    /// 파티클 재생 중단과 입자 제거
    /// </summary>
    /// <param name="particle">정리할 파티클</param>
    void StopParticle ( ParticleSystem particle )
    {
        if ( particle == null )
            return;

        particle.Stop(
            true, ParticleSystemStopBehavior.StopEmittingAndClear );
    }

    /// <summary>
    /// 이전 입자를 제거하고 처음부터 재생
    /// </summary>
    /// <param name="particle">재생할 파티클</param>
    void RestartParticle ( ParticleSystem particle )
    {
        if ( particle == null )
            return;

        StopParticle( particle );
        particle.Play( true );
    }


    /// <summary>
    /// 먹기 효과 시작
    /// </summary>
    public void BeginEat ()
    {
        RestartParticle( _eatParticle );
    }

    /// <summary>
    /// 먹기 효과 종료
    /// </summary>
    public void EndEat ()
    {
        StopParticle( _eatParticle );
    }

    /// <summary>
    /// 분노 순간의 효과 재생
    /// </summary>
    public void PlayMad ()
    {
        RestartParticle( _madParticle );
    }

    /// <summary>
    /// 먹기와 분노 효과 정리
    /// </summary>
    public void StopAll ()
    {
        StopParticle( _eatParticle );
        StopParticle( _madParticle );
    }


}
