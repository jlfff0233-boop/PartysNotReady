using System;
using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// JSON / CSV 준비 카테고리 임포터 화면과 ID별 결과 제공
/// </summary>
public class PreparationCategoryImporterWindow : DataImporterWindow, IDataImporterAssetSource
{
    protected override string DataType => "PreparationCategoryData";
    protected override string DisplayName => "준비 카테고리 임포터";
    protected override string DefaultOutputFolder => "Assets/03. Configs/!_Settings/!_GameFlow/Goal/Preparation/Category";
    protected override Type MapType => null;
    protected override string DefaultMapName => null;

    /// <summary>
    /// 결과 카테고리 종류
    /// </summary>
    public Type ResultAssetType => typeof( PreparationCategoryData );
    /// <summary>
    /// 현재 검사한 카테고리 생성 / 갱신 대상
    /// </summary>
    public IReadOnlyList<DataImportAssetChange> TargetAssets => PreparedAssets;
    /// <summary>
    /// 마지막 적용의 ID별 실제 카테고리
    /// </summary>
    public IReadOnlyList<DataImportReference> ResultAssets => AppliedAssets;

    /// <summary>
    /// 준비 카테고리 임포터 열기
    /// </summary>
    [MenuItem( "Tools/Importer/GameFlow/Preparation Category" )]
    public static void Open ()
    {
        PreparationCategoryImporterWindow window = GetWindow<PreparationCategoryImporterWindow>( "Preparation Category Importer" );
        window.minSize = new UnityEngine.Vector2( 560f, 360f );
    }

    /// <summary>
    /// 카테고리 입력과 가중치 적용 계획 준비
    /// </summary>
    /// <param name="rows">원본 데이터</param>
    /// <param name="report">검사 결과</param>
    /// <returns>Map 없는 카테고리 계획</returns>
    protected override DataImportPlan Prepare ( List<DataImportRow> rows, DataImportReport report )
    {
        return PreparationCategoryAssetImporter.Prepare( rows, OutputFolder, Bindings, report );
    }
}
