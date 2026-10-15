using UnityEngine;

/// <summary>
/// 재료 재고 설정 - 재료 종류별 최대 수량과 재보충 시간 보관
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/Item/IngredientStockData" )]
public class IngredientStockData : ScriptableObject
{
    [SerializeField, Min( 1 )] int _maxCount;       //공급원별 최대 수량
    [SerializeField, Min( 0f )] float _refillDuration;      //재보충 시간

    /// <summary>
    /// 공급원별 최대 수량
    /// </summary>
    public int MaxCount => _maxCount;

    /// <summary>
    /// 재보충 시간
    /// </summary>
    public float RefillDuration => _refillDuration;
}