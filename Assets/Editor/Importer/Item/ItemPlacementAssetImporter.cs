using System;
using System.Collections.Generic;

/// <summary>
/// 일반 배치 미션 적용기 - 아이템과 미션 참조 검사, 장소 및 Map 구성
/// </summary>
public static class ItemPlacementAssetImporter
{
    /// <summary>
    /// 직접 연결, 이름 또는 기존 Map의 아이템으로 설정 조회
    /// </summary>
    /// <param name="id">설정 ID</param>
    /// <param name="item">적용 아이템</param>
    /// <param name="bindings">직접 연결</param>
    /// <param name="assets">프로젝트 설정</param>
    /// <param name="entries">기존 Map 설정</param>
    /// <returns>기존 설정, 없으면 null</returns>
    static ItemPlacementMissionData FindExisting ( string id, ItemData item,
        List<DataImportReference> bindings, List<ItemPlacementMissionData> assets,
        List<ItemPlacementMissionData> entries )
    {
        if ( bindings.Exists( binding => binding.Id == id ) == true )
            return DataImportAssetUtility.Resolve( id, bindings, assets, value => value.name );
        ItemPlacementMissionData found = null;
        foreach ( ItemPlacementMissionData asset in assets )
        {
            if ( asset == null || ( asset.name != id && ( entries.Contains( asset ) == false || asset.ItemData != item ) ) )
                continue;
            if ( found != null && found != asset )
                throw new InvalidOperationException( $"대응하는 배치 설정이 둘 이상입니다: {id}. 직접 연결을 지정해 주세요." );
            found = asset;
        }
        return found;
    }

    /// <summary>
    /// 일반 아이템의 배치 미션과 인정 장소 적용 준비
    /// </summary>
    /// <param name="rows">원본 행</param>
    /// <param name="folder">신규 설정 폴더</param>
    /// <param name="map">갱신할 설정 Map</param>
    /// <param name="missionMap">미션 참조 Map</param>
    /// <param name="bindings">직접 연결</param>
    /// <param name="report">검사 결과</param>
    /// <param name="mapName">신규 Map 이름</param>
    /// <returns>설정과 Map 적용 계획</returns>
    public static DataImportPlan PreparePlacement ( List<DataImportRow> rows, string folder,
        ItemPlacementMissionDataMap map, MissionDataMap missionMap,
        List<DataImportReference> bindings, DataImportReport report, string mapName )
    {
        DataImportPlan plan = DataImportAssetUtility.PrepareMap( folder, map, mapName );
        List<ItemData> items = DataImportAssetUtility.FindAssets<ItemData>( );
        List<MissionData> missions = DataImportAssetUtility.GetEntries<MissionData>( missionMap, "_missions" );
        List<ItemPlacementMissionData> assets = DataImportAssetUtility.FindAssets<ItemPlacementMissionData>( );
        List<ItemPlacementMissionData> entries = DataImportAssetUtility.GetEntries<ItemPlacementMissionData>( plan.DataMap, "_data" );
        HashSet<string> ids = new( StringComparer.Ordinal );
        HashSet<ItemData> registered = new( );
        foreach ( DataImportRow row in rows )
        {
            try
            {
                row.CheckFields( "id", "itemData", "placementMission", "locations" );
                string id = DataImportAssetUtility.ReadId( row, ids );
                if ( id.Contains( "/" ) == true || id.Contains( "\\" ) == true )
                    throw row.Error( "id", "설정 ID에는 폴더 경로를 사용할 수 없습니다." );
                ItemData item = DataImportAssetUtility.Resolve( row.Text( "itemData" ), bindings, items, value => value.name );
                if ( item.GetType( ) != typeof( ItemData ) )
                    throw row.Error( "itemData", "일반 ItemData를 지정해 주세요. 음식과 서포트 데이터는 사용할 수 없습니다." );
                MissionData mission = DataImportAssetUtility.Resolve( row.Text( "placementMission" ), bindings, missions, value => value.Id );
                PreparationLocationType [ ] locations = row.Kinds<PreparationLocationType>( "locations" );
                if ( locations.Length == 0 || Array.IndexOf( locations, PreparationLocationType.None ) >= 0 )
                    throw row.Error( "locations", "None을 제외한 인정 장소를 하나 이상 지정해 주세요." );
                if ( registered.Add( item ) == false )
                    throw row.Error( "itemData", "같은 아이템 종류를 여러 번 등록할 수 없습니다." );
                ItemPlacementMissionData existing = FindExisting( id, item, bindings, assets, entries );
                plan.Changes.Add( DataImportAssetUtility.Change<ItemPlacementMissionData>( plan, id, existing, serialized =>
                {
                    serialized.FindProperty( "_itemData" ).objectReferenceValue = item;
                    serialized.FindProperty( "_placementMission" ).objectReferenceValue = mission;
                    DataImportAssetUtility.ApplyKinds( serialized, "_locations", locations );
                } ) );
                report.Details.Add( $"{id}: 아이템 {item.name} / 미션 {mission.Id} / 장소 {string.Join( ", ", locations )}" );
            }
            catch ( Exception exception ) when ( exception is FormatException || exception is ArgumentException || exception is InvalidOperationException )
            {
                report.Errors.Add( $"{row.Location}: {exception.Message}" );
            }
        }
        //입력에 대응한 설정만 Map에 등록하며 제외된 에셋은 삭제하지 않음
        plan.ApplyMap = ( serialized, changes ) => DataImportAssetUtility.ApplyList( serialized, "_data", changes );
        if ( report.CanImport == true )
            DataImportAssetUtility.Preflight( plan, "_data", null, report );
        return plan;
    }
}

