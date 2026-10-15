using UnityEngine;

using System;
using System.Collections.Generic;

/// <summary>
/// 캐릭터 공격 - 목표 명중 확인과 중복 명중 방지
/// </summary>
public class CharacterAttacker : Attacker
{
    /// <summary>
    /// 이번 공격에 맞은 대상
    /// </summary>
    HashSet<IAttackTarget> _hitTargets = new( );
    IAttackTarget _target;     //현재 공격 목표


    /// <summary>
    /// 캐릭터 명중 알림
    /// </summary>
    public event Action<IAttackTarget> OnCharacterHit;

    protected override void OnDisable ()
    {
        //공격 기록을 정리한 뒤 목표 참조 해제
        base.OnDisable( );
        _target = null;
    }


    /// <summary>
    /// 캐릭터 명중 기록 초기화
    /// </summary>
    protected override void ClearHits ()
    {
        _hitTargets.Clear( );
    }

    /// <summary>
    /// 공격 목표 설정
    /// </summary>
    /// <param name="target">공격할 캐릭터</param>
    public void SetTarget ( IAttackTarget target )
    {
        //목표 변경 전 진행 중인 공격 종료
        EndAttack( );
        _target = target;
    }

    /// <summary>
    /// 목표 캐릭터 명중 확인과 전달
    /// </summary>
    /// <param name="target">범위에 들어온 콜라이더</param>
    protected override void CheckTarget ( Collider target )
    {
        IAttackTarget character = target.GetComponentInParent<IAttackTarget>( );

        if ( character == null )
        {
            return;
        }

        //선정한 공격 목표만 처리
        if ( character != _target )
        {
            return;
        }

        if ( character.CanTakeHit == false )
        {
            return;
        }

        //복수 Collider에 걸려도 공격당 한 번만 처리
        if ( _hitTargets.Add( character ) == false )
            return;

        //명중 기록을 먼저 남겨 알림 처리 중 같은 대상이 다시 들어와도 중복 차단
        OnCharacterHit?.Invoke( character );
    }

}
