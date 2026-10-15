using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// JSON / CSV 발생 설정 임포터 창
/// </summary>
public class AnomalySpawnImporterWindow : DataImporterWindow
{
    protected override string DataType => "AnomalySpawnData";
    protected override string DisplayName => "변칙 발생 설정 임포터";
    protected override string DefaultOutputFolder => "Assets/03. Configs/!_Settings/!_GameFlow/AnomalyEvent/Spawn";
    protected override Type MapType => typeof( AnomalySpawnDataMap );
    protected override string DefaultMapName => "Anomaly_Spawn_Map";


    /// <summary>
    /// 발생 설정 임포터 열기
    /// </summary>
    [MenuItem( "Tools/Importer/Anomaly/Spawn" )]
    public static void Open ()
    {
        AnomalySpawnImporterWindow window = GetWindow<AnomalySpawnImporterWindow>( "Spawn Data Importer" );
        window.minSize = new Vector2( 560f, 360f );
    }

    /// <summary>
    /// 발생 종류와 Map 적용 준비
    /// </summary>
    /// <param name="rows">원본 행</param>
    /// <param name="report">검사 결과</param>
    /// <returns>적용 계획</returns>
    protected override DataImportPlan Prepare ( List<DataImportRow> rows, DataImportReport report )
    {
        return AnomalySpawnAssetImporter.Prepare( rows, OutputFolder, DataMap as AnomalySpawnDataMap, Bindings, report, MapName );
    }
}
