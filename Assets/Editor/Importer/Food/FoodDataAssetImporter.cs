using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// FoodData 에셋 임포트 결과
/// </summary>
public class FoodDataImportResult
{
    public int CreatedCount;
    public int UpdatedCount;
    public int MigratedCount;
    public FoodDataMap DataMap;
    public string Error;		//실패 단계와 사유
    public bool MapApplied;		//DataMap 목록 반영 여부
    /// <summary>
    /// 생성, 수정, 이관과 저장 내역
    /// </summary>
    public List<string> Changes = new( );
}

/// <summary>
/// 파싱된 음식 값을 FoodData 에셋과 FoodDataMap에 반영
/// </summary>
public static class FoodDataAssetImporter
{
    static readonly Dictionary<string, string> LegacyIds = new( StringComparer.Ordinal )
    {
        { "Ingredient_6_Apple", "Food_0_Apple" },
        { "Ingredient_1_Carrot", "Food_1_Carrot" }
    };

    /// <summary>
    /// 존재 조회에서도 임포터와 동일한 이전 음식 ID 대응을 사용
    /// </summary>
    /// <param name="id">현재 음식 ID</param>
    /// <returns>이전 ID 또는 현재 ID</returns>
    public static string GetLegacyId ( string id )
    {
        return LegacyIds.TryGetValue( id, out string legacy ) == true ? legacy : id;
    }

    /// <summary>
    /// 음식 에셋 생성 또는 갱신
    /// </summary>
    /// <param name="foods">검증된 음식 입력</param>
    /// <param name="outputFolder">신규 에셋 폴더</param>
    /// <param name="dataMap">교체할 음식 목록</param>
    /// <param name="mapName">신규 Map 파일명</param>
    /// <returns>실제 적용 내역과 실패 사유</returns>
    public static FoodDataImportResult Import (
        List<ParsedFoodImport> foods, string outputFolder, FoodDataMap dataMap,
        string mapName = "Food_Data_Map" )
    {
        FoodDataImportResult result = new FoodDataImportResult( );
        string step = "사전 검사";
        try
        {
            if ( EditorApplication.isPlayingOrWillChangePlaymode )
                throw new InvalidOperationException( "플레이 중이거나 전환 중에는 임포트할 수 없습니다." );
            outputFolder = FoodImportValidator.ValidateOutputFolder( outputFolder );
            Preflight( foods, outputFolder, dataMap, new FoodImportReport( ), mapName );
            Dictionary<string, FoodData> assets = FindFoodAssets( );
            step = $"폴더 생성: {outputFolder}";
            EnsureFolder( outputFolder );

            List<FoodData> imported = new( foods.Count );
            for ( int i = 0; i < foods.Count; i++ )
            {
                ParsedFoodImport item = foods [ i ];
                string id = item.Source.foodId;
                assets.TryGetValue( id, out FoodData asset );
                bool migrated = asset == null && LegacyIds.TryGetValue( id, out string legacyId ) &&
                    assets.TryGetValue( legacyId, out asset );
                bool created = asset == null;
                step = $"생성: {id}";
                if ( created )
                {
                    asset = CreateFoodAsset( id, outputFolder );
                    result.CreatedCount++;
                    result.Changes.Add( $"생성: {AssetDatabase.GetAssetPath( asset )}" );
                }

                step = $"값 반영: {id}";
                ApplyFoodValues( asset, item, created );
                if ( created == false )
                    result.UpdatedCount++;
                result.Changes.Add( $"값 반영: {AssetDatabase.GetAssetPath( asset )}" );
                if ( migrated )
                {
                    result.MigratedCount++;
                    result.Changes.Add( $"Legacy ID 반영: {id}" );
                }
                step = $"이름 변경: {id}";
                EnsureAssetName( asset, id );
                step = $"저장: {AssetDatabase.GetAssetPath( asset )}";
                AssetDatabase.SaveAssetIfDirty( asset );
                if ( EditorUtility.IsDirty( asset ) )
                    throw new IOException( "변경 내용이 저장되지 않았습니다." );
                result.Changes.Add( step );
                imported.Add( asset );
            }

            step = "DataMap 반영";
            result.DataMap = UpdateDataMap( dataMap, outputFolder, imported, result, mapName );
            result.Changes.Add( $"DataMap 반영: {AssetDatabase.GetAssetPath( result.DataMap )}" );
            step = "DataMap 저장";
            AssetDatabase.SaveAssetIfDirty( result.DataMap );
            if ( EditorUtility.IsDirty( result.DataMap ) )
                throw new IOException( "DataMap 변경 내용이 저장되지 않았습니다." );
            result.Changes.Add( "DataMap 저장 완료" );
        }
        catch ( Exception exception )
        {
            result.Error = $"{step} 실패: {exception.Message}\n" +
                $"Legacy ID 반영 {result.MigratedCount}개 / DataMap 반영: {result.MapApplied}. " +
                "앞선 변경은 자동 복원되지 않습니다.";
        }
        return result;
    }

    /// <summary>
    /// 쓰기 전 경로 검사와 목록 교체 내역 확인
    /// </summary>
    /// <param name="foods">검증된 음식 입력</param>
    /// <param name="outputFolder">신규 에셋 폴더</param>
    /// <param name="dataMap">교체할 음식 목록</param>
    /// <param name="report">검사 내역</param>
    /// <param name="mapName">신규 Map 파일명</param>
    public static void Preflight ( List<ParsedFoodImport> foods,
        string outputFolder, FoodDataMap dataMap, FoodImportReport report,
        string mapName = "Food_Data_Map" )
    {
        outputFolder = FoodImportValidator.ValidateOutputFolder( outputFolder );
        Dictionary<string, FoodData> assets = FindFoodAssets( );
        HashSet<string> paths = new( StringComparer.OrdinalIgnoreCase );
        HashSet<FoodData> targets = ValidatePaths( foods, assets, outputFolder, paths );
        string mapPath = DataImportAssetUtility.GetMapPath( outputFolder, dataMap, mapName );
        if ( string.IsNullOrEmpty( mapPath ) )
            throw new InvalidOperationException( "저장된 FoodDataMap을 지정해 주세요." );
        if ( dataMap == null )
            dataMap = AssetDatabase.LoadAssetAtPath<FoodDataMap>( mapPath );
        FoodImportValidator.ValidateAssetPath( mapPath, dataMap, paths );
        report.AddDetail( $"DataMap 전체 교체: {mapPath} / 입력 {foods.Count}개. 음식 에셋은 삭제하지 않습니다." );
        if ( dataMap == null )
            return;

        SerializedObject serialized = new( dataMap );
        SerializedProperty values = serialized.FindProperty( "_foodDatas" );
        for ( int i = 0; i < values.arraySize; i++ )
        {
            FoodData food = values.GetArrayElementAtIndex( i ).objectReferenceValue as FoodData;
            if ( food == null || targets.Contains( food ) == false )
                report.AddDetail( food == null ? $"Map 제외: 빈 항목 {i}" :
                    $"Map 제외: {food.FoodId} ({AssetDatabase.GetAssetPath( food )})" );
        }
    }

    static Dictionary<string, FoodData> FindFoodAssets ()
    {
        Dictionary<string, FoodData> assets = new( StringComparer.Ordinal );
        string [ ] guids = AssetDatabase.FindAssets( "t:FoodData" );
        for ( int i = 0; i < guids.Length; i++ )
        {
            string path = AssetDatabase.GUIDToAssetPath( guids [ i ] );
            FoodData asset = AssetDatabase.LoadAssetAtPath<FoodData>( path );
            if ( asset == null || string.IsNullOrWhiteSpace( asset.FoodId ) )
                continue;
            if ( assets.ContainsKey( asset.FoodId ) )
                throw new InvalidOperationException( $"FoodId '{asset.FoodId}'를 사용하는 에셋이 둘 이상입니다." );
            assets.Add( asset.FoodId, asset );
        }
        return assets;
    }

    /// <summary>
    /// 음식 출력 경로와 Legacy 대상 중복 검사
    /// </summary>
    /// <param name="foods">음식 입력</param>
    /// <param name="assets">ID별 기존 에셋</param>
    /// <param name="outputFolder">신규 에셋 폴더</param>
    /// <param name="paths">예정 출력 경로</param>
    /// <returns>재사용할 기존 에셋</returns>
    static HashSet<FoodData> ValidatePaths ( List<ParsedFoodImport> foods,
        Dictionary<string, FoodData> assets, string outputFolder, HashSet<string> paths )
    {
        HashSet<FoodData> targets = new( );
        for ( int i = 0; i < foods.Count; i++ )
        {
            string foodId = foods [ i ].Source.foodId;
            FoodData target = null;
            if ( assets.TryGetValue( foodId, out FoodData existing ) )
                target = existing;
            else if ( LegacyIds.TryGetValue( foodId, out string legacyId ) )
                assets.TryGetValue( legacyId, out target );

            string targetPath;
            if ( target != null )
            {
                if ( targets.Add( target ) == false )
                    throw new InvalidOperationException( $"'{foodId}'와 다른 입력이 같은 Legacy 또는 기존 에셋을 사용합니다." );
                string currentPath = AssetDatabase.GetAssetPath( target );
                string folder = Path.GetDirectoryName( currentPath )?.Replace( '\\', '/' );
                targetPath = $"{folder}/{foodId}.asset";
            }
            else
                targetPath = $"{outputFolder}/{foodId}.asset";

            FoodImportValidator.ValidateAssetPath( targetPath, target, paths );
        }
        return targets;
    }

    /// <summary>
    /// 음식 에셋 생성
    /// </summary>
    /// <param name="foodId">음식 식별자</param>
    /// <param name="outputFolder">생성 폴더</param>
    /// <returns>생성한 에셋</returns>
    static FoodData CreateFoodAsset ( string foodId, string outputFolder )
    {
        string path = $"{outputFolder}/{foodId}.asset";
        if ( AssetDatabase.LoadMainAssetAtPath( path ) != null )
            throw new InvalidOperationException( $"'{path}'에 다른 에셋이 이미 있습니다." );

        FoodData asset = ScriptableObject.CreateInstance<FoodData>( );
        asset.name = foodId;
        AssetDatabase.CreateAsset( asset, path );
        if ( string.Equals( AssetDatabase.GetAssetPath( asset ), path, StringComparison.OrdinalIgnoreCase ) == false )
            throw new IOException( $"'{path}' 생성에 실패했습니다." );
        return asset;
    }

    static void ApplyFoodValues ( FoodData asset, ParsedFoodImport item, bool created )
    {
        SerializedObject serialized = new SerializedObject( asset );
        serialized.FindProperty( "_itemName" ).stringValue = item.Source.foodName;
        if ( created )
            serialized.FindProperty( "_capacityCost" ).intValue = 1;
        serialized.FindProperty( "_foodId" ).stringValue = item.Source.foodId;
        serialized.FindProperty( "_category" ).enumValueIndex = ( int )item.Category;
        serialized.FindProperty( "_size" ).enumValueIndex = ( int )item.Size;
        serialized.FindProperty( "_satisfaction" ).floatValue = item.Source.satisfaction;
        serialized.FindProperty( "_placementType" ).intValue = ( int )item.PlacementType;
        serialized.ApplyModifiedPropertiesWithoutUndo( );
        EditorUtility.SetDirty( asset );
    }

    static void EnsureAssetName ( FoodData asset, string foodId )
    {
        asset.name = foodId;
        string path = AssetDatabase.GetAssetPath( asset );
        string fileName = Path.GetFileNameWithoutExtension( path );
        if ( fileName == foodId )
            return;

        string error = AssetDatabase.RenameAsset( path, foodId );
        if ( string.IsNullOrEmpty( error ) == false )
            throw new InvalidOperationException( $"'{path}' 이름 변경 실패: {error}" );
    }

    /// <summary>
    /// 음식 목록 전체 교체
    /// </summary>
    /// <param name="dataMap">기존 음식 목록</param>
    /// <param name="outputFolder">신규 에셋 폴더</param>
    /// <param name="foods">교체할 음식</param>
    /// <param name="result">생성 내역</param>
    /// <param name="mapName">신규 Map 파일명</param>
    /// <returns>변경한 음식 목록</returns>
    static FoodDataMap UpdateDataMap ( FoodDataMap dataMap,
        string outputFolder, List<FoodData> foods, FoodDataImportResult result, string mapName )
    {
        if ( dataMap == null )
        {
            string path = DataImportAssetUtility.GetMapPath( outputFolder, null, mapName );
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath( path );
            if ( existing is FoodDataMap existingMap )
                dataMap = existingMap;
            else if ( existing != null )
                throw new InvalidOperationException( $"'{path}'에 FoodDataMap이 아닌 에셋이 있습니다." );
            else
            {
                dataMap = ScriptableObject.CreateInstance<FoodDataMap>( );
                dataMap.name = Path.GetFileNameWithoutExtension( path );
                AssetDatabase.CreateAsset( dataMap, path );
                if ( string.Equals( AssetDatabase.GetAssetPath( dataMap ), path, StringComparison.OrdinalIgnoreCase ) == false )
                    throw new IOException( $"'{path}' 생성에 실패했습니다." );
                result.Changes.Add( $"DataMap 생성: {path}" );
            }
        }

        SerializedObject serialized = new SerializedObject( dataMap );
        SerializedProperty values = serialized.FindProperty( "_foodDatas" );
        values.arraySize = foods.Count;
        for ( int i = 0; i < foods.Count; i++ )
            values.GetArrayElementAtIndex( i ).objectReferenceValue = foods [ i ];
        serialized.ApplyModifiedPropertiesWithoutUndo( );
        result.MapApplied = true;
        EditorUtility.SetDirty( dataMap );
        return dataMap;
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
        string name = Path.GetFileName( folder );
        EnsureFolder( parent );
        if ( string.IsNullOrEmpty( AssetDatabase.CreateFolder( parent, name ) ) )
            throw new IOException( $"'{folder}' 폴더 생성에 실패했습니다." );
    }
}
