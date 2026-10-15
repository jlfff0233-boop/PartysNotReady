using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전체 제작 레시피 목록
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/Production/RecipeDataMap" )]
public class RecipeDataMap : ScriptableObject
{
    /// <summary>
    /// 선택 가능한 레시피
    /// </summary>
    [SerializeField] RecipeData [ ] _data;

    /// <summary>
    /// 등록된 레시피
    /// </summary>
    public IReadOnlyList<RecipeData> Data => _data;
}
