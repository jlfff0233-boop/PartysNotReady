using UnityEngine;

/// <summary>
/// 절도 대상 정보 - 개별 아이템 또는 공급원 참조
/// </summary>
public struct ItemStealTargetData
{
    /// <summary>
    /// 개별 아이템 대상
    /// </summary>
    public ItemCarry Item { get; }

    /// <summary>
    /// 공급원 대상
    /// </summary>
    public IngredientSource Source { get; }

    /// <summary>
    /// 공급원 대상 여부
    /// </summary>
    public bool IsSource => ReferenceEquals( Source, null ) == false;

    /// <summary>
    /// 접근할 지점
    /// </summary>
    public Transform TargetTransform => IsSource == true ? Source.StealPoint : Item.transform;

    /// <summary>
    /// 확보할 아이템 정보
    /// </summary>
    public ItemData Data => IsSource == true ? Source.SupplyItemData : Item.ItemData;

    /// <summary>
    /// 개별 아이템 대상 생성
    /// </summary>
    /// <param name="item">아이템 대상</param>
    public ItemStealTargetData ( ItemCarry item )
    {
        Item = item;
        Source = null;
    }

    /// <summary>
    /// 공급원 대상 생성
    /// </summary>
    /// <param name="source">공급원 대상</param>
    public ItemStealTargetData ( IngredientSource source )
    {
        Item = null;
        Source = source;
    }
}
