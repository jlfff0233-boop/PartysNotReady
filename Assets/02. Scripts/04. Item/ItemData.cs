using UnityEngine;

/// <summary>
/// 아이템 데이터 - 이름, 차지 용량
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/Item/ItemData" )]
public class ItemData : ScriptableObject
{
    //기존 FoodData에 저장된 음식 이름을 공통 이름으로 읽음
    [SerializeField] string _itemName;     //아이템 이름
    [SerializeField, Min( 1 )] int _capacityCost = 1;     //차지 용량

    /// <summary>
    /// 아이템 이름
    /// </summary>
    public string ItemName => _itemName;

    /// <summary>
    /// 차지 용량
    /// </summary>
    public int CapacityCost => _capacityCost;
}
