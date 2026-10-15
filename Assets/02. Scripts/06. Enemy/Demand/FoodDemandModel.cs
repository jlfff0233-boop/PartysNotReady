using UnityEngine;

/// <summary>
/// 음식 섭취 판정
/// </summary>
public enum DemandResult
{
    Request,      //음식 재요구
    Satisfied,    //만족
    Mad           //분노
}

/// <summary>
/// 음식 요구 모델 - 취향, 만족도와 분노 행동 판정
/// </summary>
public class FoodDemandModel
{
    float _curSatisfaction;		//현재 만족도
    float _maxSatisfaction;		//최대 만족도

    FoodDataMap _likedFoods;		//좋아하는 음식 맵
    FoodDataMap _dislikedFoods;		//싫어하는 음식 맵

    /// <summary>
    /// 현재 만족도
    /// </summary>
    public float CurrentSatisfaction => _curSatisfaction;

    /// <summary>
    /// 최대 만족도
    /// </summary>
    public float MaxSatisfaction => _maxSatisfaction;

    /// <summary>
    /// 만족도 비율
    /// </summary>
    public float SatisfactionRatio => _curSatisfaction / _maxSatisfaction;


    /// <summary>
    /// 음식 취향과 최대 만족도 설정
    /// </summary>
    /// <param name="likedFoods">좋아하는 음식 맵</param>
    /// <param name="dislikedFoods">싫어하는 음식 맵</param>
    /// <param name="maxSatisfaction">최대 만족도</param>
    public FoodDemandModel (
        FoodDataMap likedFoods,
        FoodDataMap dislikedFoods,
        float maxSatisfaction )
    {
        _likedFoods = likedFoods;
        _dislikedFoods = dislikedFoods;
        _maxSatisfaction = maxSatisfaction;
        _curSatisfaction = 0f;
    }

    /// <summary>
    /// 새 등장 만족도 초기화
    /// </summary>
    public void Reset ()
    {
        _curSatisfaction = 0f;
    }

    /// <summary>
    /// 음식 만족도 반영과 섭취 판정
    /// </summary>
    /// <param name="food">섭취한 음식</param>
    /// <returns>재요구/만족/분노</returns>
    public DemandResult ApplyFood ( FoodData food )
    {
        //두 취향 목록에 모두 있으면 싫어하는 음식 판정을 우선
        if ( ContainsFood( _dislikedFoods, food ) == true )
            return DemandResult.Mad;

        //좋아하는 음식은 기본 만족도와 무관하게 즉시 만족
        if ( ContainsFood( _likedFoods, food ) == true )
        {
            _curSatisfaction = _maxSatisfaction;
            return DemandResult.Satisfied;
        }

        //취향에 없는 음식은 기본 만족도 누적
        _curSatisfaction = Mathf.Min(
            _curSatisfaction + food.Satisfaction, _maxSatisfaction );

        if ( _curSatisfaction >= _maxSatisfaction )
            return DemandResult.Satisfied;

        return DemandResult.Request;
    }

    /// <summary>
    /// 남은 NPC 수와 확률에 따른 납치 선택
    /// </summary>
    /// <param name="remainingCount">포획 중 대상을 포함한 남은 NPC 수</param>
    /// <param name="kidnapChance">납치 확률</param>
    /// <param name="randomValue">0부터 1 사이의 난수</param>
    /// <returns>납치 선택 여부</returns>
    public bool ShouldKidnap (
        int remainingCount, float kidnapChance, float randomValue )
    {
        //납치 중인 대상을 포함해 남은 인원이 4명 이상일 때만 납치 선택
        if ( remainingCount < 4 )
            return false;

        float chance = Mathf.Clamp01( kidnapChance );

        return chance >= 1f || randomValue < chance;
    }

    /// <summary>
    /// 음식 맵 포함 여부
    /// </summary>
    /// <param name="foods">음식 맵</param>
    /// <param name="food">검사할 음식</param>
    /// <returns>포함 여부</returns>
    bool ContainsFood ( FoodDataMap foods, FoodData food )
    {
        return foods.TryGetData( food.FoodId, out _ );
    }

}
