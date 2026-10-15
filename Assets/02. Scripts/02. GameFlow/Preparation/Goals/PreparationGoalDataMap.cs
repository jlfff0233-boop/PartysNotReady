using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 평가할 준비 목표 목록
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/GameFlow/PreparationGoalDataMap" )]
public class PreparationGoalDataMap : ScriptableObject
{
    /// <summary>
    /// 준비 목표 데이터
    /// </summary>
    [Header( "----- 준비 목표 -----" )]
    [SerializeField] PreparationGoalData [ ] _data;

    /// <summary>
    /// 준비 목표 데이터
    /// </summary>
    public IReadOnlyList<PreparationGoalData> Data => _data;
}
