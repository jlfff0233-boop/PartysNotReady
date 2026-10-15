using UnityEngine;

/// <summary>
/// 제작 연출 - 완성 파티클 재생과 정리
/// </summary>
public class ProductionEffect : MonoBehaviour
{
    [Header( "----- 파티클 -----" )]
    [SerializeField] ParticleSystem _successParticle;       //최종 완성 효과

    private void OnEnable ()
    {
        StopEffect( );
    }

    private void OnDisable ()
    {
        StopEffect( );
    }


    /// <summary>
    /// 제작 완성 연출
    /// </summary>
    public void PlaySuccess ()
    {
        if ( _successParticle == null )
            return;

        //이전 재생의 입자를 지우고 처음부터 재생
        StopEffect( );
        _successParticle.Play( true );
    }

    /// <summary>
    /// 파티클 재생 중단과 남은 입자 제거
    /// </summary>
    public void StopEffect ()
    {
        //선택 효과가 비어 있어도 그릇 활성화와 반환은 유지
        if ( _successParticle == null )
            return;

        _successParticle.Stop(
            true, ParticleSystemStopBehavior.StopEmittingAndClear );
    }


}
