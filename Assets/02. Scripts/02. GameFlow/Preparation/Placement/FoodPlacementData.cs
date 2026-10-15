using UnityEngine;

/// <summary>
/// 음식 결과의 배치 미션 설정
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/GameFlow/FoodPlacementData" )]
public class FoodPlacementData : ScriptableObject
{
    [Header( "----- 제작 결과 -----" )]
    [SerializeField] RecipeData _recipe;      //적용할 레시피
    [SerializeField] MissionData _placementMission;      //배치 완료 미션

    [Header( "----- 유효 표면 -----" )]
    [SerializeField] PreparationLocationType [ ] _locations;		//음식 배치가 인정되는 표면 종류

    /// <summary>
    /// 적용할 레시피
    /// </summary>
    public RecipeData Recipe => _recipe;

    /// <summary>
    /// 배치 완료 미션
    /// </summary>
    public MissionData PlacementMission => _placementMission;

    /// <summary>
    /// 유효 표면 설정 여부
    /// </summary>
    public bool HasLocation => CheckLocations( );

    /// <summary>
    /// 실제 배치 표면의 인정 여부
    /// </summary>
    /// <param name="target">실제 배치 표면</param>
    /// <returns>유효 배치 여부</returns>
    public bool ContainsTarget ( PlacementTarget target )
    {
        if ( target == null || target.PreparationLocation == PreparationLocationType.None ||
             _locations == null )
            return false;

        foreach ( PreparationLocationType location in _locations )
        {
            if ( location == target.PreparationLocation )
                return true;
        }

        return false;
    }

    /// <summary>
    /// 음식 배치 장소 누락 검사
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
