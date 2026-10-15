using UnityEngine;

/// <summary>
/// 난동 재료 소실 집계 - 공격당 재료 소실 제한과 수량 관리
/// </summary>
public class RampageLossCounter
{
    int _lossLimit;     //공격당 최대 소실 수량
    int _lossCount;     //이번 공격의 소실 수량

    /// <summary>
    /// 공격당 재료 소실 상한 설정
    /// </summary>
    /// <param name="lossLimit">공격당 최대 소실 수량, 음수면 0</param>
    public RampageLossCounter ( int lossLimit )
    {
        _lossLimit = Mathf.Max( 0, lossLimit );
    }

    /// <summary>
    /// 공격의 피해 집계 시작
    /// </summary>
    public void BeginAttack ()
    {
        _lossCount = 0;
    }


    /// <summary>
    /// 실제로 소실된 재료 수 집계
    /// </summary>
    public void RecordIngredientLoss ()
    {
        _lossCount++;
    }

    /// <summary>
    /// 재료 소실 가능 여부 확인
    /// </summary>
    /// <param name="category">명중한 음식의 분류</param>
    /// <returns>재료 소실 가능 여부</returns>
    public bool CanLoseIngredient ( FoodCategory category )
    {
        return category == FoodCategory.Ingredient &&
               _lossCount < _lossLimit;
    }

}
