using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 활성 아이템 등록 목록
/// </summary>
public static class ItemRegister
{
    /// <summary>
    /// 현재 활성 아이템
    /// </summary>
    static HashSet<ItemCarry> _items = new( );

    /// <summary>
    /// 활성 아이템 신규 등록 알림(등록한 아이템)
    /// </summary>
    public static event Action<ItemCarry> OnRegistered;

    /// <summary>
    /// 게임 시작 시 등록 상태 초기화
    /// </summary>
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration )]
    static void Reset ()
    {
        _items.Clear( );
        OnRegistered = null;
    }

    /// <summary>
    /// 활성 아이템 등록
    /// </summary>
    /// <param name="item">등록할 아이템</param>
    public static void Register ( ItemCarry item )
    {
        if ( item == null || _items.Add( item ) == false )
            return;

        //실제 신규 등록된 아이템만 전달
        OnRegistered?.Invoke( item );
    }

    /// <summary>
    /// 비활성 아이템 등록 해제
    /// </summary>
    /// <param name="item">해제할 아이템</param>
    public static void Unregister ( ItemCarry item )
    {
        _items.Remove( item );
    }

    /// <summary>
    /// 현재 활성 아이템 조회
    /// </summary>
    /// <param name="results">조회 결과를 받을 목록</param>
    public static void GetActive ( List<ItemCarry> results )
    {
        results.Clear( );

        foreach ( ItemCarry item in _items )
        {
            if ( item != null && item.isActiveAndEnabled == true )
                results.Add( item );
        }
    }
}
