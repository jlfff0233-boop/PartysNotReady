using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 음식 원본 한 행
/// </summary>
[Serializable]
public class FoodImportRow
{
    public string foodId;
    public string foodName;
    public string category;
    public string size;
    public float satisfaction;
    public string [ ] placementType;
}

/// <summary>
/// 음식 JSON 최상위 데이터
/// </summary>
[Serializable]
public class FoodImportRoot
{
    public FoodImportRow [ ] foods;
}

/// <summary>
/// 레시피 재료 원본
/// </summary>
[Serializable]
public class RecipeIngredientImportRow
{
    public string foodId;
    public int requiredCount;
}

/// <summary>
/// 레시피 원본 한 건
/// </summary>
[Serializable]
public class RecipeImportRow
{
    public string recipeId;
    public string resultFoodId;
    public string displayName;       //이전 원본 호환용, RecipeData에는 저장하지 않음
    public RecipeIngredientImportRow [ ] ingredients;
}

/// <summary>
/// 레시피 JSON 최상위 데이터
/// </summary>
[Serializable]
public class RecipeImportRoot
{
    public RecipeImportRow [ ] recipes;
}

/// <summary>
/// 임포트 검사 결과
/// </summary>
public class FoodImportReport
{
    readonly List<string> _errors = new( );
    readonly List<string> _warnings = new( );
    /// <summary>
    /// 예정 변경과 실제 적용 내역
    /// </summary>
    List<string> _details = new( );

    public IReadOnlyList<string> Errors => _errors;
    public IReadOnlyList<string> Warnings => _warnings;
    public bool CanImport => _errors.Count == 0;
    /// <summary>
    /// 예정 변경과 실제 적용 내역
    /// </summary>
    public IReadOnlyList<string> Details => _details;

    /// <summary>
    /// 변경 내역 추가
    /// </summary>
    /// <param name="message">변경 내용</param>
    public void AddDetail ( string message )
    {
        _details.Add( message );
    }

    public void AddError ( string message )
    {
        _errors.Add( message );
    }

    public void AddWarning ( string message )
    {
        _warnings.Add( message );
    }
}

/// <summary>
/// 파싱된 음식 값
/// </summary>
public class ParsedFoodImport
{
    public FoodImportRow Source;
    public FoodCategory Category;
    public FoodSize Size;
    public FoodPlacementType PlacementType;
}

/// <summary>
/// FoodData 참조가 연결된 레시피 값
/// </summary>
public class ParsedRecipeImport
{
    public RecipeImportRow Source;
    public FoodData ResultFood;
    public readonly List<FoodData> IngredientFoods = new( );
}
