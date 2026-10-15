using UnityEngine;

/// <summary>
/// 재보충 시간 단축 설정
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/SupportItem/QuickSupplyData" )]
public class QuickSupplyData : ActiveSupportItemData
{
    [Header( "----- 재보충 -----" )]
    [SerializeField, Range( 0f, 1f )] float _reductionRatio;		//남은 시간 감소 비율

    /// <summary>
    /// 아이템 종류
    /// </summary>
    public override SupportItemType Type => SupportItemType.QuickSupply;

    /// <summary>
    /// 남은 시간 감소 비율
    /// </summary>
    public float ReductionRatio => _reductionRatio;

    /// <summary>
    /// 설정 유효 여부
    /// </summary>
    public override bool IsValid =>
        base.IsValid == true && _reductionRatio > 0f && _reductionRatio <= 1f;
}