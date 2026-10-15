using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 서포트 아이템 인벤토리 - 조회, 수량 변경
/// </summary>
[Serializable]
public class SupportItemInventory
{
    public const int MaxCount = 999;        //종류별 최대 보유량

    /// <summary>
    /// 보유 중 아이템 목록
    /// </summary>
    [SerializeField] List<SupportItemCount> _items = new( );

    /// <summary>
    /// 보유 아이템 존재 여부
    /// </summary>
    public bool HasItems => _items.Count > 0;


    /// <summary>
    /// 아이템 저장 위치 조회
    /// </summary>
    /// <param name="id">아이템 ID</param>
    /// <returns>저장 위치, 없으면 -1</returns>
    int FindIndex ( string id )
    {
        for ( int i = 0; i < _items.Count; i++ )
        {
            if ( _items [ i ].Id == id )
                return i;
        }

        return -1;
    }

    /// <summary>
    /// 아이템 보유 수량 조회
    /// </summary>
    /// <param name="id">아이템 ID</param>
    /// <returns>보유 수량</returns>
    public int GetCount ( string id )
    {
        int index = FindIndex( id );
        return index < 0 ? 0 : _items [ index ].Count;
    }

    /// <summary>
    /// 아이템 보유 수량 증가/차감
    /// </summary>
    /// <param name="id">아이템 ID</param>
    /// <param name="amount">변화량</param>
    /// <returns>수량 변경 성공 여부</returns>
    public bool TryChangeCount ( string id, int amount )
    {
        int index = FindIndex( id );
        int count = index < 0 ? 0 : _items [ index ].Count;

        //변경 전 범위를 확인해 실패 시 기존 수량 유지
        if ( amount == 0 || amount < -count ||
             amount > MaxCount - count )
            return false;

        int nextCount = count + amount;

        //수량이 0이면 제거
        if ( nextCount == 0 )
        {
            _items.RemoveAt( index );
        }
        //아이템이 없으면 새로 추가
        else if ( index < 0 )
        {
            _items.Add( new SupportItemCount( id , nextCount ) );
        }
        //있으면 수량 변경
        else
        {
            _items [ index ].Count = nextCount;
        }

        return true;
    }
}
