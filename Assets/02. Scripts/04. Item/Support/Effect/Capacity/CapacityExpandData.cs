using UnityEngine;

/// <summary>
/// 아이템 보유 수량 확장 설정
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/SupportItem/CapacityExpandData" )]
public class CapacityExpandData : PassiveSupportItemData
{
    [Header( "----- 보유 수량 -----" )]
    [SerializeField, Range( 2, 3 )] int _capacity = 3;		//보유 한도

    /// <summary>
    /// 아이템 종류
    /// </summary>
    public override SupportItemType Type => SupportItemType.BigHands;

    /// <summary>
    /// 보유 한도
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// 설정 유효 여부
    /// </summary>
    public override bool IsValid =>
        base.IsValid == true && _capacity >= 2 && _capacity <= 3;
}