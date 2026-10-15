using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 일반 아이템 배치 미션 설정 목록
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/GameFlow/ItemPlacementMissionDataMap" )]
public class ItemPlacementMissionDataMap : ScriptableObject
{
    /// <summary>
    /// 아이템 종류별 배치 미션 설정
    /// </summary>
    [SerializeField] ItemPlacementMissionData [ ] _data;

    /// <summary>
    /// 등록된 일반 아이템 배치 미션 설정
    /// </summary>
    public IReadOnlyList<ItemPlacementMissionData> Data => _data;
}