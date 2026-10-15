using UnityEngine;

/// <summary>
/// 음식 데이터 - 아이템 기본 설정과 음식 분류, 만족도, 배치 조건
/// </summary>
[CreateAssetMenu ( menuName = "GameSettings/Food/FoodData" )]
public class FoodData : ItemData
{
    [SerializeField] string _foodId;        //음식 ID
    [SerializeField] FoodCategory _category;        //음식 분류
    [SerializeField] FoodSize _size;        //음식 크기
    [SerializeField, Min( 0f )] float _satisfaction;        //기본 만족도
    [SerializeField] FoodPlacementType _placementType;      //배치 허용 대상

    /// <summary>
    /// 음식 ID
    /// </summary>
    public string FoodId => _foodId;
    /// <summary>
    /// 음식 이름
    /// </summary>
    public string FoodName => ItemName;
    /// <summary>
    /// 음식 분류
    /// </summary>
    public FoodCategory Category => _category;
    /// <summary>
    /// 음식 크기
    /// </summary>
    public FoodSize Size => _size;
    /// <summary>
    /// 만족도
    /// </summary>
    public float Satisfaction => _satisfaction;
    /// <summary>
    /// 배치 허용 대상
    /// </summary>
    public FoodPlacementType PlacementType => _placementType;


    /// <summary>
    /// 음식 배치 가능 여부 확인
    /// </summary>
    /// <param name="targetType">배치 대상 타입</param>
    /// <returns>배치 가능 여부</returns>
    public bool CanPlace ( FoodPlacementType targetType )
    {
        //허용 타입과 대상 타입에 공통 비트가 있으면 배치 가능
        return ( _placementType & targetType ) != FoodPlacementType.None;
    }
}
