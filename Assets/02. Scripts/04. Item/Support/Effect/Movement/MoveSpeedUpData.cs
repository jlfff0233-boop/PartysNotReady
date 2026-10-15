using UnityEngine;

/// <summary>
/// 이동 속도 증가 설정
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/SupportItem/MoveSpeedUpData" )]
public class MoveSpeedUpData : ActiveSupportItemData
{
    [Header( "----- 이동 효과 -----" )]
    [SerializeField, Min( 0f )] float _duration;		//효과 시간
    [SerializeField, Min( 1f )] float _multiplier;		//이동 배율

    /// <summary>
    /// 아이템 종류
    /// </summary>
    public override SupportItemType Type => SupportItemType.EnergyDrink;

    /// <summary>
    /// 효과 시간
    /// </summary>
    public float Duration => _duration;

    /// <summary>
    /// 이동 배율
    /// </summary>
    public float Multiplier => _multiplier;

    /// <summary>
    /// 설정 유효 여부
    /// </summary>
    public override bool IsValid => CheckSettings( );

    /// <summary>
    /// 효과 설정 검사
    /// </summary>
    /// <returns>설정 유효 여부</returns>
    bool CheckSettings ()
    {
        return base.IsValid == true &&
               _duration > 0f && float.IsInfinity( _duration ) == false &&
               _multiplier >= 1f && float.IsInfinity( _multiplier ) == false;
    }
}