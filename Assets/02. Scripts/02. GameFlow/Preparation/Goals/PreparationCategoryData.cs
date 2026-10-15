using UnityEngine;

/// <summary>
/// 준비 목표 카테고리의 공통 평가 설정
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/GameFlow/PreparationCategoryData" )]
public class PreparationCategoryData : ScriptableObject
{
    [Header( "----- 평가 -----" )]
    [SerializeField, Min( 0f )] float _weight = 1f;      //평가 가중치

    /// <summary>
    /// 카테고리 이름
    /// </summary>
    public string CategoryName => name;

    /// <summary>
    /// 평가 가중치
    /// </summary>
    public float Weight => _weight;

    /// <summary>
    /// 카테고리 설정 유효 여부
    /// </summary>
    public bool IsValid =>
        _weight > 0f &&
        float.IsNaN( _weight ) == false &&
        float.IsInfinity( _weight ) == false;
}
