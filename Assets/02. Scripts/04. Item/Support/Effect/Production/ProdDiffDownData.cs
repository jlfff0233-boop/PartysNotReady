using UnityEngine;

/// <summary>
/// 제작 난이도 완화 설정
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/SupportItem/ProdDiffDownData" )]
public class ProdDiffDownData : ActiveSupportItemData
{
    [Header( "----- 제작 효과 -----" )]
    [SerializeField, Min( 1f )] float _timingMultiplier;		//타이밍 성공 구간 배율
    [SerializeField, Min( 1f )] float _targetLifetimeMultiplier = 1.5f;		//클릭 대상 유지시간 배율

    /// <summary>
    /// 아이템 종류
    /// </summary>
    public override SupportItemType Type => SupportItemType.Spatula;

    /// <summary>
    /// 타이밍 성공 구간 배율
    /// </summary>
    public float TimingMultiplier => _timingMultiplier;

    /// <summary>
    /// 클릭 대상 유지시간 배율
    /// </summary>
    public float TargetLifetimeMultiplier => _targetLifetimeMultiplier;

    /// <summary>
    /// 설정 유효 여부
    /// </summary>
    public override bool IsValid =>
        base.IsValid == true &&
        IsValidMultiplier( _timingMultiplier ) &&
        IsValidMultiplier( _targetLifetimeMultiplier );

    /// <summary>
    /// 효과 배율 유효 여부
    /// </summary>
    /// <param name="multiplier">검사할 배율</param>
    /// <returns>유효 여부</returns>
    static bool IsValidMultiplier ( float multiplier )
    {
        return multiplier >= 1f && float.IsInfinity( multiplier ) == false;
    }
}
