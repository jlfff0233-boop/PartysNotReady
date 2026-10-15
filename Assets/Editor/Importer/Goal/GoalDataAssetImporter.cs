using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 제작 / 음식 배치 / 준비 평가 목표의 참조 검사와 임포트 준비
/// </summary>
public static class GoalDataAssetImporter
{
    /// <summary>
    /// 레시피와 완료 미션을 제작 목표에 연결
    /// </summary>
    /// <param name="rows">원본 행</param>
    /// <param name="folder">신규 에셋 폴더</param>
    /// <param name="map">갱신할 제작 목표 Map</param>
    /// <param name="missionMap">미션 ID를 조회할 후보 Map</param>
    /// <param name="bindings">직접 지정한 참조</param>
    /// <param name="report">검사 결과</param>
    /// <param name="mapName">신규 Map 파일명</param>
    /// <returns>제작 목표와 Map 반영 계획</returns>
    public static DataImportPlan PrepareCook ( List<DataImportRow> rows, string folder,
        CookGoalDataMap map, MissionDataMap missionMap, List<DataImportReference> bindings, DataImportReport report, string mapName = "Cook_Goal_Map" )
    {
        DataImportPlan plan = DataImportAssetUtility.PrepareMap( folder, map, mapName );
        List<RecipeData> recipes = DataImportAssetUtility.FindAssets<RecipeData>( );
        List<MissionData> missions = DataImportAssetUtility.GetEntries<MissionData>( missionMap, "_missions" );
        List<CookGoalData> existing = DataImportAssetUtility.GetEntries<CookGoalData>( plan.DataMap, "_data" );
        HashSet<RecipeData> registered = new( );
        PrepareRows( rows, plan, report, ( row, id ) =>
        {
            row.CheckFields( "id", "recipe", "mission" );
            RecipeData recipe = DataImportAssetUtility.Resolve( row.Text( "recipe" ), bindings, recipes, value => value.RecipeId );
            MissionData mission = DataImportAssetUtility.Resolve( row.Text( "mission" ), bindings, missions, value => value.Id );
            if ( registered.Add( recipe ) == false )
                throw row.Error( "recipe", "같은 레시피를 여러 번 등록할 수 없습니다." );
            CookGoalData asset = FindExisting( id, bindings, existing, value => value.Recipe == recipe );
            return DataImportAssetUtility.Change( plan, id, asset, serialized =>
            {
                serialized.FindProperty( "_recipe" ).objectReferenceValue = recipe;
                serialized.FindProperty( "_mission" ).objectReferenceValue = mission;
            } );
        } );
        return plan;
    }

    /// <summary>
    /// 레시피와 배치 미션 / 인정 표면을 음식 배치 목표에 연결
    /// </summary>
    /// <param name="rows">원본 행</param>
    /// <param name="folder">신규 에셋 폴더</param>
    /// <param name="map">갱신할 음식 배치 Map</param>
    /// <param name="missionMap">미션 ID를 조회할 후보 Map</param>
    /// <param name="bindings">직접 지정한 참조</param>
    /// <param name="report">검사 결과</param>
    /// <param name="mapName">신규 Map 파일명</param>
    /// <returns>음식 배치 목표와 Map 반영 계획</returns>
    public static DataImportPlan PreparePlacement ( List<DataImportRow> rows, string folder,
        FoodPlacementDataMap map, MissionDataMap missionMap, List<DataImportReference> bindings, DataImportReport report, string mapName = "Food_Placement_Map" )
    {
        DataImportPlan plan = DataImportAssetUtility.PrepareMap( folder, map, mapName );
        List<RecipeData> recipes = DataImportAssetUtility.FindAssets<RecipeData>( );
        List<MissionData> missions = DataImportAssetUtility.GetEntries<MissionData>( missionMap, "_missions" );
        List<FoodPlacementData> existing = DataImportAssetUtility.GetEntries<FoodPlacementData>( plan.DataMap, "_data" );
        HashSet<RecipeData> registered = new( );
        PrepareRows( rows, plan, report, ( row, id ) =>
        {
            row.CheckFields( "id", "recipe", "placementMission", "locations" );
            RecipeData recipe = DataImportAssetUtility.Resolve( row.Text( "recipe" ), bindings, recipes, value => value.RecipeId );
            MissionData mission = DataImportAssetUtility.Resolve( row.Text( "placementMission" ), bindings, missions, value => value.Id );
            PreparationLocationType [ ] locations = ReadLocations( row );
            if ( registered.Add( recipe ) == false )
                throw row.Error( "recipe", "같은 레시피를 여러 번 등록할 수 없습니다." );
            FoodPlacementData asset = FindExisting( id, bindings, existing, value => value.Recipe == recipe );
            return DataImportAssetUtility.Change( plan, id, asset, serialized =>
            {
                serialized.FindProperty( "_recipe" ).objectReferenceValue = recipe;
                serialized.FindProperty( "_placementMission" ).objectReferenceValue = mission;
                DataImportAssetUtility.ApplyKinds( serialized, "_locations", locations );
            } );
        } );
        return plan;
    }

    /// <summary>
    /// 카테고리 참조를 ID별로 검사하고 같은 오류의 영향 행을 묶음
    /// </summary>
    /// <param name="rows">목표 원본</param>
    /// <param name="bindings">직접 연결</param>
    /// <param name="categories">프로젝트 카테고리</param>
    /// <param name="results">선행 ID별 결과 또는 null</param>
    /// <param name="resolved">조회한 카테고리</param>
    /// <param name="report">검사 결과</param>
    /// <returns>카테고리 참조가 유효한 행</returns>
    static List<DataImportRow> ResolveCategories ( List<DataImportRow> rows, List<DataImportReference> bindings,
        List<PreparationCategoryData> categories, IReadOnlyList<DataImportReference> results,
        Dictionary<string, PreparationCategoryData> resolved, DataImportReport report )
    {
        List<DataImportRow> validRows = new( );
        Dictionary<string, string> errors = new( StringComparer.Ordinal );
        Dictionary<string, List<string>> locations = new( StringComparer.Ordinal );
        foreach ( DataImportRow row in rows )
        {
            string id;
            try
            {
                id = row.Text( "category" );
            }
            catch ( FormatException exception )
            {
                report.Errors.Add( exception.Message );
                continue;
            }
            if ( resolved.ContainsKey( id ) == false && errors.ContainsKey( id ) == false )
            {
                try
                {
                    PreparationCategoryData category = null;
                    if ( results != null )
                    {
                        foreach ( DataImportReference result in results )
                        {
                            if ( result.Id != id )
                                continue;
                            if ( category != null )
                                throw new InvalidOperationException( "선행 카테고리 ID가 중복됐습니다." );
                            category = result.Asset as PreparationCategoryData;
                            if ( category == null )
                                throw new InvalidOperationException( "선행 결과가 유효한 준비 카테고리가 아닙니다." );
                        }
                        if ( category == null )
                            throw new InvalidOperationException( "선행 적용 결과에 해당 ID가 없습니다." );
                    }
                    else
                    {
                        category = DataImportAssetUtility.Resolve( id, bindings, categories, value => value.name );
                    }
                    resolved.Add( id, category );
                }
                catch ( InvalidOperationException exception )
                {
                    errors.Add( id, exception.Message );
                    locations.Add( id, new List<string>( ) );
                }
            }
            if ( errors.ContainsKey( id ) == true )
                locations [ id ].Add( row.Location );
            else
                validRows.Add( row );
        }
        foreach ( KeyValuePair<string, string> error in errors )
            report.Errors.Add( $"카테고리 '{error.Key}': {error.Value}\n영향 행: {string.Join( ", ", locations [ error.Key ] )}\n" +
                ( results == null ? "준비 카테고리를 먼저 적용하거나 기존 카테고리 SO를 직접 연결해 주세요." :
                    "연결한 선행 카테고리 파일에 해당 ID를 포함하고 다시 적용해 주세요." ) );
        return validRows;
    }

    /// <summary>
    /// 준비 평가 목표의 카테고리 / 아이템 / 수량 연결
    /// </summary>
    /// <param name="rows">원본 행</param>
    /// <param name="folder">신규 에셋 폴더</param>
    /// <param name="map">갱신할 준비 목표 Map</param>
    /// <param name="bindings">직접 지정한 참조</param>
    /// <param name="report">검사 결과</param>
    /// <param name="mapName">신규 Map 파일명</param>
    /// <param name="categoryResults">선행 적용 ID별 카테고리, null이면 기존 참조 검색</param>
    /// <returns>준비 목표와 Map 반영 계획</returns>
    public static DataImportPlan PreparePreparation ( List<DataImportRow> rows, string folder,
        PreparationGoalDataMap map, List<DataImportReference> bindings, DataImportReport report, string mapName = "Preparation_Goal_Map",
        IReadOnlyList<DataImportReference> categoryResults = null )
    {
        DataImportPlan plan = DataImportAssetUtility.PrepareMap( folder, map, mapName );
        List<PreparationCategoryData> categories = DataImportAssetUtility.FindAssets<PreparationCategoryData>( );
        List<ItemData> items = DataImportAssetUtility.FindAssets<ItemData>( );
        List<PreparationGoalData> existing = DataImportAssetUtility.GetEntries<PreparationGoalData>( plan.DataMap, "_data" );
        report.Details.Add( "기존 카테고리 가중치 / 아이템 설정은 보존합니다." );
        Dictionary<string, PreparationCategoryData> resolved = new( StringComparer.Ordinal );
        List<DataImportRow> validRows = ResolveCategories( rows, bindings, categories, categoryResults, resolved, report );
        PrepareRows( validRows, plan, report, ( row, id ) =>
        {
            row.CheckFields( "id", "category", "itemData", "locations", "requiredCount" );
            PreparationCategoryData category = resolved [ row.Text( "category" ) ];
            ItemData item = DataImportAssetUtility.Resolve( row.Text( "itemData" ), bindings,
                items, value => value is FoodData food ? food.FoodId : value.name );
            if ( category.IsValid == false )
                throw row.Error( "category", "카테고리 가중치는 유한한 양수여야 합니다." );
            int count = row.Integer( "requiredCount" );
            if ( count < 1 )
                throw row.Error( "requiredCount", "필요 수량은 1 이상이어야 합니다." );
            bool foodGoal = item is FoodData foodItem && foodItem.Category == FoodCategory.Prepared;
            PreparationLocationType [ ] locations = foodGoal == true ?
                row.Kinds<PreparationLocationType>( "locations" ) : ReadLocations( row );
            if ( Array.IndexOf( locations, PreparationLocationType.None ) >= 0 )
                throw row.Error( "locations", "None은 인정 표면으로 사용할 수 없습니다." );
            PreparationGoalData asset = FindExisting( id, bindings, existing, value => value.GoalId == id );
            return DataImportAssetUtility.Change( plan, id, asset, serialized =>
            {
                serialized.FindProperty( "_goalId" ).stringValue = id;
                serialized.FindProperty( "_category" ).objectReferenceValue = category;
                serialized.FindProperty( "_itemData" ).objectReferenceValue = item;
                serialized.FindProperty( "_requiredCount" ).intValue = count;
                DataImportAssetUtility.ApplyKinds( serialized, "_locations", locations );
            } );
        } );
        return plan;
    }

    /// <summary>
    /// 목표 원본을 검사하고 일반 목록 Map 적용 준비
    /// </summary>
    /// <param name="rows">원본 행</param>
    /// <param name="plan">Map과 출력 경로</param>
    /// <param name="report">검사 결과</param>
    /// <param name="prepare">목표별 값과 참조 검사</param>
    static void PrepareRows ( List<DataImportRow> rows, DataImportPlan plan, DataImportReport report,
        Func<DataImportRow, string, DataImportAssetChange> prepare )
    {
        HashSet<string> ids = new( StringComparer.Ordinal );
        foreach ( DataImportRow row in rows )
        {
            try
            {
                string id = DataImportAssetUtility.ReadId( row, ids );
                plan.Changes.Add( prepare( row, id ) );
            }
            catch ( Exception exception ) when ( exception is FormatException || exception is ArgumentException || exception is InvalidOperationException )
            {
                report.Errors.Add( $"{row.Location}: {exception.Message}" );
            }
        }
        plan.ApplyMap = ( serialized, changes ) => DataImportAssetUtility.ApplyList( serialized, "_data", changes );
        if ( report.CanImport == true )
            DataImportAssetUtility.Preflight( plan, "_data", null, report );
    }

    /// <summary>
    /// 직접 연결 또는 대상 Map의 기존 목표 조회
    /// </summary>
    /// <param name="id">원본 식별자</param>
    /// <param name="bindings">직접 연결</param>
    /// <param name="candidates">기존 목표 목록</param>
    /// <param name="matches">같은 목표 판정</param>
    /// <returns>기존 목표, 없으면 null</returns>
    static T FindExisting<T> ( string id, List<DataImportReference> bindings, List<T> candidates,
        Func<T, bool> matches ) where T : ScriptableObject
    {
        if ( bindings.Exists( binding => binding.Id == id ) == true )
            return DataImportAssetUtility.Resolve( id, bindings, candidates, value => value.name );
        T found = null;
        foreach ( T asset in candidates )
        {
            if ( asset == null || matches( asset ) == false )
                continue;
            if ( found != null )
                throw new InvalidOperationException( $"기존 Map의 대응 에셋이 둘 이상입니다: {id}" );
            found = asset;
        }
        return found;
    }

    /// <summary>
    /// 비어 있지 않은 유효 배치 표면 목록 읽기
    /// </summary>
    /// <param name="row">원본 행</param>
    /// <returns>인정 표면 목록</returns>
    static PreparationLocationType [ ] ReadLocations ( DataImportRow row )
    {
        PreparationLocationType [ ] locations = row.Kinds<PreparationLocationType>( "locations" );
        if ( locations.Length == 0 || Array.IndexOf( locations, PreparationLocationType.None ) >= 0 )
            throw row.Error( "locations", "None 이외의 인정 표면을 하나 이상 지정해 주세요." );
        return locations;
    }
}
