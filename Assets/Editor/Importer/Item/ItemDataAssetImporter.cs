using System;
using System.Collections.Generic;

/// <summary>
/// 일반 아이템 적용기 - 이름과 용량 검사, 기존 에셋 재사용 계획
/// </summary>
public static class ItemDataAssetImporter
{
    /// <summary>
    /// 직접 연결 또는 고유한 이름으로 기존 일반 아이템 조회
    /// </summary>
    /// <param name="id">입력 ID</param>
    /// <param name="bindings">직접 연결</param>
    /// <param name="items">프로젝트 아이템</param>
    /// <returns>기존 아이템, 없으면 null</returns>
    static ItemData FindExisting ( string id, List<DataImportReference> bindings, List<ItemData> items )
    {
        if ( bindings.Exists( binding => binding.Id == id ) == true )
            return DataImportAssetUtility.Resolve( id, bindings, items, value => value.name );
        ItemData found = null;
        foreach ( ItemData item in items )
        {
            if ( item == null || item.name != id )
                continue;
            if ( found != null )
                throw new InvalidOperationException( $"같은 이름의 아이템이 둘 이상입니다: {id}. 직접 연결을 지정해 주세요." );
            found = item;
        }
        return found;
    }

    /// <summary>
    /// 일반 아이템 값과 생성 및 갱신 대상 검사
    /// </summary>
    /// <param name="rows">JSON 또는 CSV 원본</param>
    /// <param name="folder">신규 에셋 폴더</param>
    /// <param name="bindings">직접 연결</param>
    /// <param name="report">검사 내역</param>
    /// <returns>Map 없는 아이템 적용 계획</returns>
    public static DataImportPlan PrepareItems ( List<DataImportRow> rows, string folder,
        List<DataImportReference> bindings, DataImportReport report )
    {
        DataImportPlan plan = new( ) { OutputFolder = FoodImportValidator.ValidateOutputFolder( folder ) };
        //파생 데이터도 조회하여 잘못된 타입의 기존 에셋을 갱신하지 않음
        List<ItemData> items = DataImportAssetUtility.FindAssets<ItemData>( );
        HashSet<string> ids = new( StringComparer.Ordinal );
        foreach ( DataImportRow row in rows )
        {
            try
            {
                row.CheckFields( "id", "itemName", "capacityCost" );
                string id = DataImportAssetUtility.ReadId( row, ids );
                if ( id.Contains( "/" ) == true || id.Contains( "\\" ) == true )
                    throw row.Error( "id", "아이템 ID에는 폴더 경로를 사용할 수 없습니다." );
                string name = row.Text( "itemName" );
                int capacity = row.Integer( "capacityCost" );
                if ( capacity < 1 )
                    throw row.Error( "capacityCost", "차지 용량은 1 이상이어야 합니다." );
                ItemData existing = FindExisting( id, bindings, items );
                DataImportAssetChange change = DataImportAssetUtility.Change<ItemData>( plan, id, existing, serialized =>
                {
                    serialized.FindProperty( "_itemName" ).stringValue = name;
                    serialized.FindProperty( "_capacityCost" ).intValue = capacity;
                } );
                //직접 연결과 출력 경로 조회 모두 일반 ItemData만 허용
                if ( change.Asset != null && change.Asset.GetType( ) != typeof( ItemData ) )
                    throw row.Error( "id", "음식 또는 서포트 데이터는 일반 아이템으로 갱신할 수 없습니다." );
                plan.Changes.Add( change );
                report.Details.Add( $"{id}: 이름 {name} / 차지 용량 {capacity}" );
            }
            catch ( Exception exception ) when ( exception is FormatException || exception is ArgumentException || exception is InvalidOperationException )
            {
                report.Errors.Add( $"{row.Location}: {exception.Message}" );
            }
        }
        if ( report.CanImport == true )
            DataImportAssetUtility.Preflight( plan, null, null, report );
        return plan;
    }
}

