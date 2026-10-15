using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상점 데이터맵 - 판매 목록
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/Shop/ShopDataMap" )]
public class ShopDataMap : ScriptableObject
{
    [SerializeField] SupportItemData [ ] _data;        //판매 순서별 아이템

    /// <summary>
    /// 판매 아이템 목록
    /// </summary>
    public IReadOnlyList<SupportItemData> Data => _data;


    /// <summary>
    /// 판매 목록 설정 확인
    /// </summary>
    /// <returns>설정 유효 여부</returns>
    public bool Validate ()
    {
        if ( _data == null )
        {
            Debug.LogError( "[상점 설정 오류] 판매 목록을 연결해 주세요.", this );
            return false;
        }

        HashSet<string> ids = new( );
        HashSet<SupportItemType> types = new( );

        for ( int i = 0; i < _data.Length; i++ )
        {
            SupportItemData data = _data [ i ];

            if ( data == null || data.IsValid == false ||
                 string.IsNullOrWhiteSpace( data.Id ) == true )
            {
                Debug.LogError(
                    $"[상점 설정 오류] 판매 목록 " +
                    $"{i + 1}번의 아이템 설정과 ID를 확인해 주세요.", this );
                return false;
            }

            //판매 가격 확인
            if ( data.Cost <= 0 )
            {
                Debug.LogError(
                    $"[상점 설정 오류] 가격은 " +
                    $"1 이상이어야 합니다: {data.ItemName}", this );
                return false;
            }

            //판매 아이템의 ID와 종류 중복 확인
            if ( ids.Add( data.Id ) == false ||
                 types.Add( data.Type ) == false )
            {
                Debug.LogError(
                    $"[상점 설정 오류] " +
                    $"ID 또는 종류가 중복되었습니다: {data.ItemName}",
                    this );
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// ID에 해당하는 판매 아이템 조회
    /// </summary>
    /// <param name="id">아이템 ID</param>
    /// <param name="data">판매 아이템</param>
    /// <returns>조회 성공 여부</returns>
    public bool TryGetData ( string id, out SupportItemData data )
    {
        foreach ( SupportItemData item in _data )
        {
            if ( item.Id != id )
                continue;

            data = item;
            return true;
        }

        data = null;
        return false;
    }
}