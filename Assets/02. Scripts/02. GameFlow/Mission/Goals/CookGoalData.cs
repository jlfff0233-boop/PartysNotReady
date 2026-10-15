using System;
using UnityEngine;

/// <summary>
/// 제작 목표 - 레시피와 완료 미션 보관
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/Production/CookGoalData" )]
public class CookGoalData : ScriptableObject
{
    [SerializeField] RecipeData _recipe;      //제작 레시피
    [SerializeField] MissionData _mission;      //완료를 반영할 미션

    /// <summary>
    /// 제작 레시피
    /// </summary>
    public RecipeData Recipe => _recipe;

    /// <summary>
    /// 미션 설정
    /// </summary>
    public MissionData Mission => _mission;
}
