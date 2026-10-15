using UnityEngine;

/// <summary>
/// 액티브 서포트 아이템 설정
/// </summary>
public abstract class ActiveSupportItemData : SupportItemData
{
    [Header( "----- 사용 횟수 -----" )]
    [SerializeField, Min( 1 )] int _useCount;		//사용 횟수

    /// <summary>
    /// 사용 횟수
    /// </summary>
    public int UseCount => _useCount;

    /// <summary>
    /// 설정 유효 여부
    /// </summary>
    public override bool IsValid =>
        base.IsValid == true && _useCount > 0;
}