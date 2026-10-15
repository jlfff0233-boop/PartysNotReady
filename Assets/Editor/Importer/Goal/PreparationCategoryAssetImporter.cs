using System;
using System.Collections.Generic;

/// <summary>
/// 준비 카테고리의 생성 / 가중치 갱신 계획
/// </summary>
public static class PreparationCategoryAssetImporter
{
    /// <summary>
    /// 직접 연결 또는 유일한 이름으로 기존 카테고리 조회
    /// </summary>
    /// <param name="id">입력 ID</param>
    /// <param name="bindings">직접 연결</param>
    /// <param name="candidates">프로젝트 카테고리</param>
    /// <returns>기존 카테고리, 없으면 null</returns>
    static PreparationCategoryData FindExisting ( string id, List<DataImportReference> bindings,
        List<PreparationCategoryData> candidates )
    {
        if ( bindings.Exists( binding => binding.Id == id ) == true )
            return DataImportAssetUtility.Resolve( id, bindings, candidates, value => value.name );
        PreparationCategoryData found = null;
        foreach ( PreparationCategoryData candidate in candidates )
        {
            if ( candidate == null || candidate.name != id )
                continue;
            if ( found != null )
                throw new InvalidOperationException( $"같은 이름의 카테고리가 둘 이상입니다: {id}. 직접 연결을 지정해 주세요." );
            found = candidate;
        }
        return found;
    }

    /// <summary>
    /// 입력 카테고리 검사와 Map 없는 에셋 계획 준비
    /// </summary>
    /// <param name="rows">JSON / CSV 원본</param>
    /// <param name="folder">신규 에셋 폴더</param>
    /// <param name="bindings">직접 연결</param>
    /// <param name="report">검사 내역</param>
    /// <returns>생성 / 갱신 계획</returns>
    public static DataImportPlan Prepare ( List<DataImportRow> rows, string folder,
        List<DataImportReference> bindings, DataImportReport report )
    {
        DataImportPlan plan = new( ) { OutputFolder = FoodImportValidator.ValidateOutputFolder( folder ) };
        List<PreparationCategoryData> categories = DataImportAssetUtility.FindAssets<PreparationCategoryData>( );
        HashSet<string> ids = new( StringComparer.Ordinal );
        foreach ( DataImportRow row in rows )
        {
            try
            {
                row.CheckFields( "id", "weight" );
                string id = DataImportAssetUtility.ReadId( row, ids );
                if ( id.Contains( "/" ) == true || id.Contains( "\\" ) == true )
                    throw row.Error( "id", "카테고리 ID에는 폴더 경로를 사용할 수 없습니다." );
                float weight = row.Number( "weight" );
                if ( weight <= 0f )
                    throw row.Error( "weight", "평가 가중치는 유한한 양수여야 합니다." );
                PreparationCategoryData existing = FindExisting( id, bindings, categories );
                plan.Changes.Add( DataImportAssetUtility.Change<PreparationCategoryData>( plan, id, existing,
                    serialized => serialized.FindProperty( "_weight" ).floatValue = weight ) );
                report.Details.Add( $"{id}: 가중치 {weight}" );
            }
            catch ( Exception exception ) when ( exception is FormatException || exception is ArgumentException || exception is InvalidOperationException )
            {
                report.Errors.Add( $"{row.Location}: {exception.Message}" );
            }
        }
        report.Warnings.Add( "갱신한 가중치는 해당 카테고리를 참조하는 기존 목표에도 적용됩니다. 입력에 없는 카테고리 에셋은 삭제하지 않습니다." );
        if ( report.CanImport == true )
            DataImportAssetUtility.Preflight( plan, null, null, report );
        return plan;
    }
}
