using UnityEngine;

using System;
using System.Collections.Generic;

/// <summary>
/// 아이템 공격 - 대상 확인과 중복 명중 방지
/// </summary>
public class ItemAttacker : Attacker
{
    HashSet<ItemCarry> _hitItems = new ( );     //이번 공격에 맞은 아이템

    /// <summary>
    /// 아이템 명중 알림(아이템)
    /// </summary>
    public event Action<ItemCarry> OnItemHit;

    /// <summary>
    /// 아이템 명중 기록 초기화
    /// </summary>
    protected override void ClearHits ( )
    {
        _hitItems.Clear ( );
    }

    /// <summary>
    /// 아이템 명중 확인, 전달
    /// </summary>
    /// <param name="target">범위에 들어온 콜라이더</param>
    protected override void CheckTarget ( Collider target )
    {
        ItemCarry item = target.GetComponentInParent<ItemCarry> ( );

        //아이템이 아니거나 비활성/보유 상태면 종료
        if ( item == null || item.isActiveAndEnabled == false || item.IsHold == true )
            return;

        //이미 명중당한 아이템은 공격에서 제외
        if ( _hitItems.Add ( item ) == false )
            return;

        //아이템 명중 이벤트 발행
        OnItemHit?.Invoke ( item );
    }
}
