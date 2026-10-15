using System;
using System.Collections.Generic;

/// <summary>
/// 레시피 재료 검사 - 요구량 충족과 다른 재료 포함 여부 판정
/// </summary>
public static class RecipeIngredientChecker
{
    /// <summary>
    /// 레시피 설정과 현재 재료 수량으로 조합 판정
    /// </summary>
    /// <param name="recipe">확인할 레시피</param>
    /// <param name="getCount">재료별 현재 수량 조회</param>
    /// <param name="totalCount">현재 전체 재료 수</param>
    /// <returns>재료 조합 판정</returns>
    public static ProductionStartResult Check (
        RecipeData recipe, Func<ItemData, int> getCount, int totalCount )
    {
        if ( recipe == null )
            return ProductionStartResult.Unavailable;

        return Check( GetRequirements( recipe ), getCount, totalCount );
    }

    /// <summary>
    /// 요구량과 현재 수량으로 조합 판정
    /// </summary>
    /// <param name="requirements">중복 없는 재료별 필요 수량</param>
    /// <param name="getCount">재료별 현재 수량 조회</param>
    /// <param name="totalCount">현재 전체 재료 수</param>
    /// <returns>잘못된 재료를 우선한 조합 판정</returns>
    public static ProductionStartResult Check (
        IEnumerable<KeyValuePair<ItemData, int>> requirements,
        Func<ItemData, int> getCount, int totalCount )
    {
        int matchedTotal = 0;       //레시피에 포함된 현재 재료 수
        bool hasMissing = false;       //필요 수량 부족 여부

        foreach ( KeyValuePair<ItemData, int> requirement in requirements )
        {
            int count = requirement.Key == null ? 0 : getCount( requirement.Key );
            matchedTotal += count;
            hasMissing |= requirement.Key == null || count < requirement.Value;
        }

        //부족한 재료가 있어도 잘못된 재료 포함을 우선 판정
        if ( matchedTotal != totalCount )
            return ProductionStartResult.InvalidIngredient;

        return hasMissing == true
            ? ProductionStartResult.MissingIngredient
            : ProductionStartResult.Started;
    }

    /// <summary>
    /// 레시피의 재료 요구량을 순서대로 조회
    /// </summary>
    /// <param name="recipe">조회할 레시피</param>
    /// <returns>재료와 필요 수량, 누락된 설정은 기본값</returns>
    static IEnumerable<KeyValuePair<ItemData, int>> GetRequirements ( RecipeData recipe )
    {
        for ( int i = 0; i < recipe.IngredientCount; i++ )
        {
            IngredientRequirement requirement = recipe.GetIngredient( i );
            yield return requirement == null
                ? default
                : new KeyValuePair<ItemData, int>( requirement.Item, requirement.RequiredCount );
        }
    }
}
