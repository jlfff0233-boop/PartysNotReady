using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// JSON / CSV 준비 평가 목표 임포터 창
/// </summary>
public class PreparationGoalImporterWindow : DataImporterWindow, IDataImporterAssetReference
{
    /// <summary>
    /// 선행 카테고리 입력 ID와 실제 SO
    /// </summary>
    IReadOnlyList<DataImportReference> _categories;
    bool _categorySourceLocked;      //선행 카테고리 연결 사용 여부

    /// <summary>
    /// 연결할 카테고리 종류
    /// </summary>
    public Type ReferenceAssetType => typeof( PreparationCategoryData );

    protected override string DataType => "PreparationGoalData";

    protected override string DisplayName => "준비 평가 목표 임포터";

    protected override string DefaultOutputFolder => "Assets/03. Configs/!_Settings/!_GameFlow/Goal/Preparation";

    protected override Type MapType => typeof( PreparationGoalDataMap );

    protected override string DefaultMapName => "Preparation_Goal_Map";


    /// <summary>
    /// 선행 카테고리 결과 연결과 이전 검사 해제
    /// </summary>
    /// <param name="assets">ID별 카테고리 또는 미적용 null</param>
    /// <param name="locked">선행 연결 사용 여부</param>
    public void SetReferenceAssets ( IReadOnlyList<DataImportReference> assets, bool locked )
    {
        if ( ReferenceEquals( _categories, assets ) == true && _categorySourceLocked == locked )
            return;
        _categories = assets;
        _categorySourceLocked = locked;
        InvalidatePreview( );
    }


    /// <summary>
    /// 준비 평가 목표 임포터 열기
    /// </summary>
    [MenuItem( "Tools/Importer/GameFlow/Preparation Goal" )]
    public static void Open ()
    {
        PreparationGoalImporterWindow window = GetWindow<PreparationGoalImporterWindow>( "Preparation Goal Importer" );
        window.minSize = new Vector2( 560f, 360f );
    }

    /// <summary>
    /// 준비 평가 목표와 참조 적용 준비
    /// </summary>
    /// <param name="rows">원본 행</param>
    /// <param name="report">검사 결과</param>
    /// <returns>적용 계획</returns>
    protected override DataImportPlan Prepare ( List<DataImportRow> rows, DataImportReport report )
    {
        if ( _categorySourceLocked == true && _categories == null )
            throw new InvalidOperationException( "선행 준비 카테고리 적용을 먼저 완료해 주세요." );
        return GoalDataAssetImporter.PreparePreparation( rows, OutputFolder, DataMap as PreparationGoalDataMap,
            Bindings, report, MapName, _categorySourceLocked == true ? _categories : null );
    }
}
