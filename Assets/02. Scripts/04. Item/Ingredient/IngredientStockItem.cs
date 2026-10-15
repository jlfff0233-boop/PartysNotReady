using UnityEngine;

/// <summary>
/// 대여 재료와 공급원 연결
/// </summary>
public struct IngredientStockItem
{
    ItemCarry _item;        //대여 재료
    Object _source;        //원래 공급원

    /// <summary>
    /// 대여 재료
    /// </summary>
    public ItemCarry Item => _item;

    /// <summary>
    /// 원래 공급원
    /// </summary>
    public Object Source => _source;


    /// <summary>
    /// 대여 재료와 공급원 연결 정보 생성
    /// </summary>
    /// <param name="item">대여 재료</param>
    /// <param name="source">원래 공급원</param>
    public IngredientStockItem ( ItemCarry item , Object source )
    {
        _item = item;
        _source = source;
    }
}
