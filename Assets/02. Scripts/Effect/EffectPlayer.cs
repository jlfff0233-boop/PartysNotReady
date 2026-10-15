using DG.Tweening;
using UnityEngine;

/// <summary>
/// 연출 실행 모듈 - Tween 교체와 수명 관리
/// </summary>
public class EffectPlayer
{
    GameObject _owner;		//연출 수명 소유자
    Tween _effect;		//현재 연출

    /// <summary>
    /// 현재 연출
    /// </summary>
    public Tween Current => _effect;

    /// <summary>
    /// 연출 실행 여부
    /// </summary>
    public bool IsPlaying => _effect != null && _effect.IsActive( );


    /// <summary>
    /// 연출 소유자 연결
    /// </summary>
    /// <param name="owner">연출 수명 소유자</param>
    public EffectPlayer ( GameObject owner )
    {
        _owner = owner;
    }

    /// <summary>
    /// 기존 연출을 중단하고 새 연출 실행
    /// </summary>
    /// <param name="effect">실행할 Tween 또는 Sequence</param>
    public void Play ( Tween effect )
    {
        Stop( );

        _effect = effect;

        //결과 화면처럼 시간이 정지된 상태에서도 UI 연출 진행
        effect.SetUpdate( true );

        //Sequence 내부 Tween이 아닌 최상위 연출에 수명 연결
        effect.SetLink( _owner, LinkBehaviour.KillOnDestroy );

        effect.OnKill( () =>
        {
            //이전 연출의 정리가 새 연출 참조를 해제하지 않도록 구분
            if ( object.ReferenceEquals( _effect, effect ) == true )
                _effect = null;
        } );

        effect.Play( );
    }

    /// <summary>
    /// 완료 콜백 없이 현재 연출 중단
    /// </summary>
    public void Stop ()
    {
        Tween effect = _effect;		//정리할 연출

        //Kill의 콜백보다 먼저 참조를 해제
        _effect = null;
        effect?.Kill( false );
    }
}
