using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 서포트 아이템 종류
/// </summary>
public enum SupportItemType
{
    BigHands,       //큰손
    EnergyDrink,        //에너지 드링크
    QuickSupply,        //급속 보급권
    Spatula,       //뒤집개
    RescueGloves        //구조용 장갑
}

/// <summary>
/// 서포트 아이템 공통 설정
/// </summary>
public abstract class SupportItemData : ScriptableObject
{
    [Header ( "----- 표시 -----" )]
    [SerializeField] string _itemName;		//표시 이름
    [SerializeField] string _id;       //아이템 아이디
    [SerializeField] Sprite _icon;		//표시 아이콘
    [SerializeField] int _cost;     //가격
    [SerializeField, TextArea ( 5 , 10 )] string _description;     //설명

    /// <summary>
    /// 아이템 종류
    /// </summary>
    public abstract SupportItemType Type { get; }

    /// <summary>
    /// 표시 이름
    /// </summary>
    public string ItemName => _itemName;

    /// <summary>
    /// 아이템 아이디
    /// </summary>
    public string Id => _id;

    /// <summary>
    /// 표시 아이콘
    /// </summary>
    public Sprite Icon => _icon;

    /// <summary>
    /// 아이템 가격
    /// </summary>
    public int Cost => _cost;

    /// <summary>
    /// 아이템 설명
    /// </summary>
    public string Description => _description;

    /// <summary>
    /// 설정 유효 여부
    /// </summary>
    public virtual bool IsValid =>
        string.IsNullOrWhiteSpace ( _itemName ) == false;
}
