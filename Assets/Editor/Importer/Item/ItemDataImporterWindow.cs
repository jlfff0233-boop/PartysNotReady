using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 일반 아이템 임포터 - 이름과 용량 입력, 검사 및 적용 결과 제공
/// </summary>
public class ItemDataImporterWindow : DataImporterWindow, IDataImporterAssetSource
{
    protected override string DataType => "ItemData";
    protected override string DisplayName => "일반 아이템 임포터";
    protected override string DefaultOutputFolder => "Assets/03. Configs/Item/General";
    protected override Type MapType => null;
    protected override string DefaultMapName => null;

    /// <summary>
    /// 결과 아이템 종류
    /// </summary>
    public Type ResultAssetType => typeof( ItemData );
    /// <summary>
    /// 현재 검사한 생성 및 갱신 대상
    /// </summary>
    public IReadOnlyList<DataImportAssetChange> TargetAssets => PreparedAssets;
    /// <summary>
    /// 마지막 적용의 ID별 아이템
    /// </summary>
    public IReadOnlyList<DataImportReference> ResultAssets => AppliedAssets;

    /// <summary>
    /// 일반 아이템 임포터 열기
    /// </summary>
    [MenuItem( "Tools/Importer/Item/Data" )]
    public static void Open ()
    {
        ItemDataImporterWindow window = GetWindow<ItemDataImporterWindow>( "일반 아이템 임포터" );
        window.minSize = new Vector2( 560f, 360f );
    }

    /// <summary>
    /// 이름과 용량 적용 계획 준비
    /// </summary>
    /// <param name="rows">원본 행</param>
    /// <param name="report">검사 결과</param>
    /// <returns>아이템 생성 및 갱신 계획</returns>
    protected override DataImportPlan Prepare ( List<DataImportRow> rows, DataImportReport report )
    {
        return ItemDataAssetImporter.PrepareItems( rows, OutputFolder, Bindings, report );
    }
}

