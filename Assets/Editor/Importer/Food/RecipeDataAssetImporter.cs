using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// RecipeData 에셋 임포트 결과
/// </summary>
public class RecipeDataImportResult
{
    public int CreatedCount;
    public int UpdatedCount;
    public RecipeDataMap DataMap;		//반영한 레시피 Map
    public bool MapApplied;		//Map 목록 반영 여부
    public string Error;		//실패 단계와 사유
    /// <summary>
    /// 생성, 수정과 저장 내역
    /// </summary>
    public List<string> Changes = new( );
}

/// <summary>
/// 파싱된 레시피를 RecipeData 에셋에 반영
/// </summary>
public static class RecipeDataAssetImporter
{
    /// <summary>
    /// 레시피 Map 목록 전체 교체와 저장
    /// </summary>
    /// <param name="dataMap">기존 레시피 Map</param>
    /// <param name="outputFolder">신규 Map 폴더</param>
    /// <param name="mapName">신규 Map 파일명</param>
    /// <param name="recipes">반영한 레시피</param>
    /// <param name="result">실제 적용 결과</param>
    static void UpdateDataMap ( RecipeDataMap dataMap, string outputFolder, string mapName,
        List<RecipeData> recipes, RecipeDataImportResult result )
    {
        DataImportPlan plan = DataImportAssetUtility.PrepareMap( outputFolder, dataMap, mapName );
        plan.ApplyMap = ( serialized, changes ) =>
        {
            SerializedProperty values = serialized.FindProperty( "_data" );
            values.arraySize = recipes.Count;
            for ( int i = 0; i < recipes.Count; i++ )
                values.GetArrayElementAtIndex( i ).objectReferenceValue = recipes [ i ];
        };
        DataImportResult mapResult = DataImportAssetUtility.Import( plan );
        result.DataMap = mapResult.DataMap as RecipeDataMap;
        result.MapApplied = mapResult.MapApplied;
        result.Changes.AddRange( mapResult.Changes );
        if ( mapResult.Error != null )
            throw new IOException( mapResult.Error );
    }

    /// <summary>
    /// 레시피 에셋 생성 또는 갱신
    /// </summary>
    /// <param name="recipes">검증된 레시피 입력</param>
    /// <param name="outputFolder">신규 에셋 폴더</param>
    /// <param name="dataMap">교체할 레시피 Map</param>
    /// <param name="mapName">신규 Map 파일명</param>
    /// <returns>실제 적용 내역과 실패 사유</returns>
    public static RecipeDataImportResult Import (
        List<ParsedRecipeImport> recipes, string outputFolder, RecipeDataMap dataMap = null,
        string mapName = "Recipe_Data_Map" )
    {
        RecipeDataImportResult result = new RecipeDataImportResult( );
        string step = "사전 검사";
        try
        {
            if ( EditorApplication.isPlayingOrWillChangePlaymode )
                throw new InvalidOperationException( "플레이 중이거나 전환 중에는 임포트할 수 없습니다." );
            outputFolder = FoodImportValidator.ValidateOutputFolder( outputFolder );
            Preflight( recipes, outputFolder, dataMap, null, mapName );
            Dictionary<string, RecipeData> assets = FindRecipeAssets( );
            step = $"폴더 생성: {outputFolder}";
            EnsureFolder( outputFolder );

            List<RecipeData> imported = new( recipes.Count );
            for ( int i = 0; i < recipes.Count; i++ )
            {
                ParsedRecipeImport item = recipes [ i ];
                string id = item.Source.recipeId;
                bool created = assets.TryGetValue( id, out RecipeData asset ) == false;
                step = $"생성: {id}";
                if ( created )
                {
                    asset = CreateRecipeAsset( id, outputFolder );
                    result.CreatedCount++;
                    result.Changes.Add( $"생성: {AssetDatabase.GetAssetPath( asset )}" );
                }

                step = $"값 반영: {id}";
                ApplyRecipeValues( asset, item );
                if ( created == false )
                    result.UpdatedCount++;
                result.Changes.Add( $"값 반영: {AssetDatabase.GetAssetPath( asset )}" );
                step = $"이름 변경: {id}";
                EnsureAssetName( asset, id );
                step = $"저장: {AssetDatabase.GetAssetPath( asset )}";
                AssetDatabase.SaveAssetIfDirty( asset );
                if ( EditorUtility.IsDirty( asset ) )
                    throw new IOException( "변경 내용이 저장되지 않았습니다." );
                result.Changes.Add( step );
                imported.Add( asset );
            }

            step = "RecipeDataMap 반영 / 저장";
            UpdateDataMap( dataMap, outputFolder, mapName, imported, result );
        }
        catch ( Exception exception )
        {
            result.Error = $"{step} 실패: {exception.Message}\nMap 반영: {result.MapApplied}. 앞선 변경은 자동 복원되지 않습니다.";
        }
        return result;
    }

    /// <summary>
    /// 쓰기 전 레시피 참조와 출력 경로 검사
    /// </summary>
    /// <param name="recipes">검증된 레시피 입력</param>
    /// <param name="outputFolder">신규 에셋 폴더</param>
    /// <param name="dataMap">교체할 레시피 Map</param>
    /// <param name="report">교체 검사 내역</param>
    /// <param name="mapName">신규 Map 파일명</param>
    public static void Preflight ( List<ParsedRecipeImport> recipes, string outputFolder,
        RecipeDataMap dataMap = null, FoodImportReport report = null, string mapName = "Recipe_Data_Map" )
    {
        outputFolder = FoodImportValidator.ValidateOutputFolder( outputFolder );
        Dictionary<string, RecipeData> assets = FindRecipeAssets( );
        HashSet<string> paths = ValidatePaths( recipes, assets, outputFolder );
        DataImportPlan mapPlan = DataImportAssetUtility.PrepareMap( outputFolder, dataMap, mapName );
        FoodImportValidator.ValidateAssetPath( mapPlan.MapPath, mapPlan.DataMap, paths );
        if ( report != null )
        {
            HashSet<RecipeData> targets = new( );
            foreach ( ParsedRecipeImport recipe in recipes )
            {
                assets.TryGetValue( recipe.Source.recipeId, out RecipeData existing );
                if ( existing != null )
                    targets.Add( existing );
                report.AddDetail( $"{( existing == null ? "생성" : "갱신 / 참조 보존" )}: {recipe.Source.recipeId}" );
            }
            report.AddDetail( $"RecipeDataMap 전체 교체: {mapPlan.MapPath} / 입력 {recipes.Count}개. 제외된 에셋은 삭제하지 않습니다." );
            if ( mapPlan.DataMap is RecipeDataMap currentMap && currentMap.Data != null )
            {
                foreach ( RecipeData existing in currentMap.Data )
                {
                    if ( existing == null || targets.Contains( existing ) == false )
                        report.AddDetail( $"Map 제외: {( existing == null ? "빈 항목" : AssetDatabase.GetAssetPath( existing ) )}" );
                }
            }
        }
        foreach ( ParsedRecipeImport recipe in recipes )
        {
            if ( recipe.ResultFood == null ||
                 recipe.IngredientFoods.Count != recipe.Source.ingredients.Length )
                throw new InvalidOperationException( $"'{recipe.Source.recipeId}'의 음식 참조가 유효하지 않습니다." );
            foreach ( FoodData ingredient in recipe.IngredientFoods )
            {
                if ( ingredient == null )
                    throw new InvalidOperationException( $"'{recipe.Source.recipeId}'의 재료 참조가 비어 있습니다." );
            }
        }
    }

    static Dictionary<string, RecipeData> FindRecipeAssets ()
    {
        Dictionary<string, RecipeData> assets = new( StringComparer.Ordinal );
        string [ ] guids = AssetDatabase.FindAssets( "t:RecipeData" );
        for ( int i = 0; i < guids.Length; i++ )
        {
            RecipeData asset = AssetDatabase.LoadAssetAtPath<RecipeData>(
                AssetDatabase.GUIDToAssetPath( guids [ i ] ) );
            if ( asset == null || string.IsNullOrWhiteSpace( asset.RecipeId ) )
                continue;
            if ( assets.ContainsKey( asset.RecipeId ) )
                throw new InvalidOperationException(
                    $"RecipeId '{asset.RecipeId}'를 사용하는 에셋이 둘 이상입니다." );
            assets.Add( asset.RecipeId, asset );
        }
        return assets;
    }

    /// <summary>
    /// 레시피 출력 경로 중복과 기존 에셋 충돌 검사
    /// </summary>
    /// <param name="recipes">레시피 입력</param>
    /// <param name="assets">ID별 기존 에셋</param>
    /// <param name="outputFolder">신규 에셋 폴더</param>
    /// <returns>검사한 출력 경로</returns>
    static HashSet<string> ValidatePaths ( List<ParsedRecipeImport> recipes,
        Dictionary<string, RecipeData> assets, string outputFolder )
    {
        HashSet<string> paths = new( StringComparer.OrdinalIgnoreCase );
        for ( int i = 0; i < recipes.Count; i++ )
        {
            string recipeId = recipes [ i ].Source.recipeId;
            if ( assets.TryGetValue( recipeId, out RecipeData existing ) )
            {
                string path = AssetDatabase.GetAssetPath( existing );
                string target = $"{Path.GetDirectoryName( path )?.Replace( '\\', '/' )}/{recipeId}.asset";
                FoodImportValidator.ValidateAssetPath( target, existing, paths );
                continue;
            }

            string createPath = $"{outputFolder}/{recipeId}.asset";
            FoodImportValidator.ValidateAssetPath( createPath, null, paths );
        }
        return paths;
    }

    /// <summary>
    /// 레시피 에셋 생성
    /// </summary>
    /// <param name="recipeId">레시피 식별자</param>
    /// <param name="outputFolder">생성 폴더</param>
    /// <returns>생성한 에셋</returns>
    static RecipeData CreateRecipeAsset ( string recipeId, string outputFolder )
    {
        RecipeData asset = ScriptableObject.CreateInstance<RecipeData>( );
        asset.name = recipeId;
        string path = $"{outputFolder}/{recipeId}.asset";
        AssetDatabase.CreateAsset( asset, path );
        if ( string.Equals( AssetDatabase.GetAssetPath( asset ), path, StringComparison.OrdinalIgnoreCase ) == false )
            throw new IOException( $"'{path}' 생성에 실패했습니다." );
        return asset;
    }

    static void ApplyRecipeValues ( RecipeData asset, ParsedRecipeImport item )
    {
        SerializedObject serialized = new SerializedObject( asset );
        serialized.FindProperty( "_recipeId" ).stringValue = item.Source.recipeId;
        serialized.FindProperty( "_resultItem" ).objectReferenceValue = item.ResultFood;

        SerializedProperty ingredients = serialized.FindProperty( "_ingredients" );
        ingredients.arraySize = item.Source.ingredients.Length;
        for ( int i = 0; i < item.Source.ingredients.Length; i++ )
        {
            SerializedProperty element = ingredients.GetArrayElementAtIndex( i );
            element.FindPropertyRelative( "_item" ).objectReferenceValue =
                item.IngredientFoods [ i ];
            element.FindPropertyRelative( "_requiredCount" ).intValue =
                item.Source.ingredients [ i ].requiredCount;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo( );
        EditorUtility.SetDirty( asset );
    }

    static void EnsureAssetName ( RecipeData asset, string recipeId )
    {
        asset.name = recipeId;
        string path = AssetDatabase.GetAssetPath( asset );
        if ( Path.GetFileNameWithoutExtension( path ) == recipeId )
            return;

        string error = AssetDatabase.RenameAsset( path, recipeId );
        if ( string.IsNullOrEmpty( error ) == false )
            throw new InvalidOperationException( $"'{path}' 이름 변경 실패: {error}" );
    }

    /// <summary>
    /// 없는 출력 폴더 생성
    /// </summary>
    /// <param name="folder">출력 폴더</param>
    static void EnsureFolder ( string folder )
    {
        folder = folder?.Replace( '\\', '/' ).TrimEnd( '/' );
        if ( string.IsNullOrWhiteSpace( folder ) ||
             ( folder != "Assets" && folder.StartsWith( "Assets/", StringComparison.Ordinal ) == false ) )
            throw new ArgumentException( "출력 폴더는 Assets 내부 경로여야 합니다." );
        if ( AssetDatabase.IsValidFolder( folder ) )
            return;

        string parent = Path.GetDirectoryName( folder )?.Replace( '\\', '/' );
        EnsureFolder( parent );
        if ( string.IsNullOrEmpty( AssetDatabase.CreateFolder( parent, Path.GetFileName( folder ) ) ) )
            throw new IOException( $"'{folder}' 폴더 생성에 실패했습니다." );
    }
}
