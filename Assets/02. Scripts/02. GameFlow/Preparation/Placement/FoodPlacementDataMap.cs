using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 음식 배치 설정 목록
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/GameFlow/FoodPlacementDataMap" )]
public class FoodPlacementDataMap : ScriptableObject
{
    /// <summary>
    /// 레시피별 음식 배치 설정
    /// </summary>
    [SerializeField] FoodPlacementData [ ] _data;

    /// <summary>
    /// 등록된 음식 배치 설정
    /// </summary>
    public IReadOnlyList<FoodPlacementData> Data => _data;
}
