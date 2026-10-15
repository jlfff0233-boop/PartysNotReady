using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 제작 미션 설정 목록
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/Production/CookGoalDataMap" )]
public class CookGoalDataMap : ScriptableObject
{
    /// <summary>
    /// 레시피별 제작 미션
    /// </summary>
    [SerializeField] CookGoalData [ ] _data;

    /// <summary>
    /// 등록된 제작 미션
    /// </summary>
    public IReadOnlyList<CookGoalData> Data => _data;
}
