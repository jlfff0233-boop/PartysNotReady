using UnityEngine;

/// <summary>
/// 준비 목표의 수량과 평가 설정
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/GameFlow/PreparationGoalData" )]
public class PreparationGoalData : ScriptableObject
{
    [Header( "----- 목표 -----" )]
    [SerializeField] string _goalId;		//목표 아이디
    [SerializeField] PreparationCategoryData _category;      //평가 카테고리

    [Header( "----- 인정 대상 -----" )]
    [SerializeField] ItemData _itemData;		//평가할 아이템 종류
    [SerializeField] PreparationLocationType [ ] _locations;        //비음식 목표의 유효 표면 종류

    [Header( "----- 평가 -----" )]
    [SerializeField, Min( 1 )] int _requiredCount = 1;		//필요 수량

    /// <summary>
    /// 목표 아이디
    /// </summary>
    public string GoalId => _goalId;

    /// <summary>
    /// 평가 카테고리
    /// </summary>
    public PreparationCategoryData Category => _category;

    /// <summary>
    /// 필요 수량
    /// </summary>
    public int RequiredCount => _requiredCount;

    /// <summary>
    /// 평가할 아이템 종류
    /// </summary>
    public ItemData ItemData => _itemData;

    /// <summary>
    /// 제작 음식 목표 여부
    /// </summary>
    public bool IsFoodGoal =>
        _itemData is FoodData food && food.Category == FoodCategory.Prepared;

    /// <summary>
    /// 목표 설정 유효 여부
    /// </summary>
    public bool IsValid =>
        string.IsNullOrWhiteSpace( _goalId ) == false &&
        _category != null &&
        _category.IsValid == true &&
        _itemData != null &&
        ( IsFoodGoal == true || CheckLocations( ) ) &&
        _requiredCount > 0;

    /// <summary>
    /// 목표의 유효 배치 표면 확인
    /// </summary>
    /// <param name="location">정착한 표면 종류</param>
    /// <returns>포함 여부</returns>
    public bool ContainsLocation ( PreparationLocationType location )
    {
        if ( location == PreparationLocationType.None || _locations == null )
            return false;

        foreach ( PreparationLocationType candidate in _locations )
        {
            if ( candidate == location )
                return true;
        }

        return false;
    }

    /// <summary>
    /// 비음식 목표의 배치 장소 누락 검사
    /// </summary>
    /// <returns>장소가 하나 이상이고 미설정 항목이 없는지 여부</returns>
    bool CheckLocations ()
    {
        if ( _locations == null || _locations.Length == 0 )
            return false;

        foreach ( PreparationLocationType location in _locations )
        {
            if ( location == PreparationLocationType.None )
                return false;
        }

        return true;
    }
}
