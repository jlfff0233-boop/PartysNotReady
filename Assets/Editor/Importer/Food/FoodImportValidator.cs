using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

/// <summary>
/// 음식과 레시피 원본 검증 및 enum / 에셋 참조 변환
/// </summary>
public static class FoodImportValidator
{
    /// <summary>
    /// 음식 원본 검증
    /// </summary>
    /// <param name="rows">음식 원본</param>
    /// <param name="report">검사 결과</param>
    /// <returns>변환한 음식 입력</returns>
    public static List<ParsedFoodImport> ValidateFoods (
        List<FoodImportRow> rows, FoodImportReport report )
    {
        List<ParsedFoodImport> parsed = new( );
        HashSet<string> ids = new( StringComparer.Ordinal );
        Dictionary<string, string> sortNumbers = new( StringComparer.Ordinal );

        for ( int i = 0; i < rows.Count; i++ )
        {
            FoodImportRow row = rows [ i ];
            string label = $"{i + 1}번째 음식";
            if ( row == null )
            {
                report.AddError( $"{label} 데이터가 비어 있습니다." );
                continue;
            }

            row.foodId = row.foodId?.Trim( );
            row.foodName = row.foodName?.Trim( );
            row.category = row.category?.Trim( );
            row.size = row.size?.Trim( );
            if ( string.IsNullOrWhiteSpace( row.foodId ) )
                report.AddError( $"{label}의 foodId가 비어 있습니다." );
            else if ( ids.Add( row.foodId ) == false )
                report.AddError( $"foodId '{row.foodId}'가 중복되었습니다." );
            else if ( IsValidAssetName( row.foodId ) == false )
                report.AddError( $"foodId '{row.foodId}'를 에셋 파일명으로 사용할 수 없습니다." );
            if ( string.IsNullOrWhiteSpace( row.foodName ) )
                report.AddError( $"{label}의 foodName이 비어 있습니다." );

            bool categoryValid = Enum.TryParse( row.category, false, out FoodCategory category ) &&
                Enum.IsDefined( typeof( FoodCategory ), category );
            if ( categoryValid == false )
                report.AddError( $"{label}의 category '{row.category}'를 인식할 수 없습니다." );
            bool sizeValid = Enum.TryParse( row.size, false, out FoodSize size ) &&
                Enum.IsDefined( typeof( FoodSize ), size );
            if ( sizeValid == false )
                report.AddError( $"{label}의 size '{row.size}'를 인식할 수 없습니다." );
            if ( float.IsNaN( row.satisfaction ) || float.IsInfinity( row.satisfaction ) ||
                 row.satisfaction < 0f )
                report.AddError( $"{label}의 satisfaction은 유한한 0 이상 값이어야 합니다." );

            FoodPlacementType placement = FoodPlacementType.None;
            if ( row.placementType == null || row.placementType.Length == 0 )
                report.AddError( $"{label}의 placementType이 비어 있습니다." );
            else
            {
                for ( int typeIndex = 0; typeIndex < row.placementType.Length; typeIndex++ )
                {
                    string value = row.placementType [ typeIndex ]?.Trim( );
                    if ( Enum.TryParse( value, false, out FoodPlacementType type ) == false ||
                         type == FoodPlacementType.None ||
                         ( type & ~( FoodPlacementType.Table | FoodPlacementType.Bowl ) ) != 0 )
                        report.AddError( $"{label}의 placementType '{value}'를 인식할 수 없습니다." );
                    else
                        placement |= type;
                }
            }

            if ( categoryValid )
            {
                string prefix = category == FoodCategory.Ingredient ? "Ingredient_" : "Food_";
                if ( row.foodId?.StartsWith( prefix, StringComparison.Ordinal ) == false )
                    report.AddError( $"'{row.foodId}'는 {category} ID 접두사 '{prefix}'로 시작해야 합니다." );
            }

            string sortKey = GetSortNumberKey( row.foodId );
            if ( sortKey != null && sortNumbers.TryGetValue( sortKey, out string existing ) )
                report.AddWarning( $"정렬 번호가 겹칩니다: '{existing}', '{row.foodId}'. 번호는 정렬용이므로 임포트는 가능합니다." );
            else if ( sortKey != null )
                sortNumbers.Add( sortKey, row.foodId );

            parsed.Add( new ParsedFoodImport
            {
                Source = row,
                Category = category,
                Size = size,
                PlacementType = placement
            } );
        }
        return parsed;
    }

    /// <summary>
    /// 레시피 원본 검증과 FoodData 참조 연결
    /// </summary>
    public static List<ParsedRecipeImport> ValidateRecipes (
        List<RecipeImportRow> rows, FoodDataMap foodDataMap, FoodImportReport report )
    {
        List<ParsedRecipeImport> parsed = new( );
        Dictionary<string, FoodData> foods = BuildFoodLookup( foodDataMap, report );
        HashSet<string> recipeIds = new( StringComparer.Ordinal );

        for ( int i = 0; i < rows.Count; i++ )
        {
            RecipeImportRow row = rows [ i ];
            string label = $"{i + 1}번째 레시피";
            if ( row == null )
            {
                report.AddError( $"{label} 데이터가 비어 있습니다." );
                continue;
            }

            row.recipeId = row.recipeId?.Trim( );
            row.resultFoodId = row.resultFoodId?.Trim( );
            if ( string.IsNullOrWhiteSpace( row.recipeId ) )
                report.AddError( $"{label}의 recipeId가 비어 있습니다." );
            else if ( recipeIds.Add( row.recipeId ) == false )
                report.AddError( $"recipeId '{row.recipeId}'가 중복되었습니다." );
            else if ( IsValidAssetName( row.recipeId ) == false )
                report.AddError( $"recipeId '{row.recipeId}'를 에셋 파일명으로 사용할 수 없습니다." );

            ParsedRecipeImport item = new ParsedRecipeImport { Source = row };
            if ( foods.TryGetValue( row.resultFoodId ?? string.Empty, out FoodData result ) == false )
                report.AddError(
                    $"'{row.recipeId}'의 결과 FoodData '{row.resultFoodId}'가 FoodDataMap에 없습니다." );
            else
            {
                item.ResultFood = result;
                if ( result.Category != FoodCategory.Prepared )
                    report.AddError(
                        $"'{row.recipeId}'의 결과 '{row.resultFoodId}'가 Prepared가 아닙니다." );
            }

            if ( row.ingredients == null || row.ingredients.Length == 0 )
                report.AddError( $"'{row.recipeId}'의 재료 목록이 비어 있습니다." );
            else
                ValidateIngredients( row, foods, item, report );
            parsed.Add( item );
        }
        return parsed;
    }

    static void ValidateIngredients ( RecipeImportRow recipe,
        Dictionary<string, FoodData> foods, ParsedRecipeImport parsed, FoodImportReport report )
    {
        HashSet<string> ingredientIds = new( StringComparer.Ordinal );
        for ( int i = 0; i < recipe.ingredients.Length; i++ )
        {
            RecipeIngredientImportRow ingredient = recipe.ingredients [ i ];
            if ( ingredient == null || string.IsNullOrWhiteSpace( ingredient.foodId ) )
            {
                report.AddError(
                    $"'{recipe.recipeId}'의 {i + 1}번째 재료 ID가 비어 있습니다." );
                parsed.IngredientFoods.Add( null );
                continue;
            }

            ingredient.foodId = ingredient.foodId.Trim( );
            if ( ingredientIds.Add( ingredient.foodId ) == false )
                report.AddError(
                    $"'{recipe.recipeId}'에 재료 '{ingredient.foodId}'가 중복되었습니다." );
            if ( ingredient.requiredCount <= 0 )
                report.AddError(
                    $"'{recipe.recipeId}'의 재료 '{ingredient.foodId}' 수량은 1 이상이어야 합니다." );

            if ( foods.TryGetValue( ingredient.foodId, out FoodData food ) == false )
            {
                report.AddError(
                    $"'{recipe.recipeId}'의 재료 FoodData '{ingredient.foodId}'가 FoodDataMap에 없습니다." );
                parsed.IngredientFoods.Add( null );
            }
            else
            {
                parsed.IngredientFoods.Add( food );
                if ( food.Category != FoodCategory.Ingredient )
                    report.AddError(
                        $"'{recipe.recipeId}'의 재료 '{ingredient.foodId}'가 Ingredient가 아닙니다." );
                if ( food == parsed.ResultFood )
                    report.AddError(
                        $"'{recipe.recipeId}'의 결과 음식이 자기 재료로 등록되었습니다." );
            }
        }
    }

    static Dictionary<string, FoodData> BuildFoodLookup (
        FoodDataMap map, FoodImportReport report )
    {
        Dictionary<string, FoodData> foods = new( StringComparer.Ordinal );
        if ( map == null )
        {
            report.AddError( "FoodDataMap을 지정해 주세요." );
            return foods;
        }

        SerializedObject serializedMap = new SerializedObject( map );
        SerializedProperty values = serializedMap.FindProperty( "_foodDatas" );
        for ( int i = 0; i < values.arraySize; i++ )
        {
            FoodData food = values.GetArrayElementAtIndex( i ).objectReferenceValue as FoodData;
            if ( food == null )
            {
                report.AddWarning( $"FoodDataMap의 {i}번 항목이 비어 있습니다." );
                continue;
            }
            if ( string.IsNullOrWhiteSpace( food.FoodId ) )
                report.AddError( $"FoodData '{food.name}'의 FoodId가 비어 있습니다." );
            else if ( foods.ContainsKey( food.FoodId ) )
                report.AddError( $"FoodDataMap에 FoodId '{food.FoodId}'가 중복되었습니다." );
            else
                foods.Add( food.FoodId, food );
        }
        return foods;
    }

    /// <summary>
    /// 출력 폴더 경로와 기존 상위 폴더 검사
    /// </summary>
    /// <param name="folder">출력 폴더</param>
    /// <returns>구분자를 통일한 경로</returns>
    public static string ValidateOutputFolder ( string folder )
    {
        folder = folder?.Replace( '\\', '/' ).TrimEnd( '/' );
        if ( string.IsNullOrWhiteSpace( folder ) ||
             ( folder != "Assets" && folder.StartsWith( "Assets/", StringComparison.Ordinal ) == false ) )
            throw new ArgumentException( "출력 폴더는 Assets 내부 경로여야 합니다." );

        string current = string.Empty;
        foreach ( string part in folder.Split( '/' ) )
        {
            if ( IsValidAssetName( part ) == false || part.EndsWith( "." ) || part.EndsWith( " " ) )
                throw new ArgumentException( $"출력 폴더 이름 '{part}'가 올바르지 않습니다." );
            current = current.Length == 0 ? part : $"{current}/{part}";
            if ( File.Exists( current ) ||
                 ( AssetDatabase.LoadMainAssetAtPath( current ) != null &&
                   AssetDatabase.IsValidFolder( current ) == false ) )
                throw new InvalidOperationException( $"'{current}'는 폴더가 아닙니다." );
        }
        return folder;
    }

    /// <summary>
    /// 예정 에셋 경로 중복과 기존 에셋 충돌 검사
    /// </summary>
    /// <param name="path">예정 경로</param>
    /// <param name="target">재사용할 에셋</param>
    /// <param name="paths">이번 입력의 예정 경로</param>
    public static void ValidateAssetPath (
        string path, UnityEngine.Object target, HashSet<string> paths )
    {
        ValidateOutputFolder( Path.GetDirectoryName( path ) );
        if ( paths.Add( path ) == false )
            throw new InvalidOperationException( $"예정 에셋 경로가 중복됩니다: '{path}'." );

        UnityEngine.Object occupant = AssetDatabase.LoadMainAssetAtPath( path );
        if ( occupant != null && occupant != target || Directory.Exists( path ) ||
             ( occupant == null && File.Exists( path ) ) )
            throw new InvalidOperationException( $"'{path}'에 다른 파일 또는 에셋이 있습니다." );
    }

    static string GetSortNumberKey ( string foodId )
    {
        if ( string.IsNullOrWhiteSpace( foodId ) )
            return null;
        string [ ] parts = foodId.Split( '_' );
        if ( parts.Length < 3 || int.TryParse( parts [ 1 ], out int number ) == false )
            return null;
        return $"{parts [ 0 ]}_{number}";
    }

    /// <summary>
    /// 에셋 이름에 사용할 수 있는 문자열 검사
    /// </summary>
    /// <param name="value">이름 후보</param>
    /// <returns>사용 가능 여부</returns>
    static bool IsValidAssetName ( string value )
    {
        if ( string.IsNullOrWhiteSpace( value ) || value == "." || value == ".." ||
             value.Contains( "/" ) || value.Contains( "\\" ) )
            return false;
        string name = value.Split( '.' ) [ 0 ].ToUpperInvariant( );
        if ( name == "CON" || name == "PRN" || name == "AUX" || name == "NUL" ||
             ( name.Length == 4 && ( name.StartsWith( "COM" ) || name.StartsWith( "LPT" ) ) &&
               name [ 3 ] >= '1' && name [ 3 ] <= '9' ) )
            return false;
        return value.IndexOfAny( Path.GetInvalidFileNameChars( ) ) < 0;
    }
}
