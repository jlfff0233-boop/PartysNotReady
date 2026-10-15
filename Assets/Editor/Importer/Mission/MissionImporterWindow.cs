using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// JSON / CSV 단계 후보 미션 임포터 창
/// </summary>
public class MissionImporterWindow : DataImporterWindow
{
    string _stageName = string.Empty;		//새 단계 이름, 비어 있으면 기존 이름 보존

    protected override string DataType => "MissionData";
    protected override string DisplayName => "미션 데이터 임포터";
    protected override string DefaultOutputFolder => "Assets/03. Configs/!_Settings/!_GameFlow/Mission";
    protected override Type MapType => typeof( MissionDataMap );
    protected override string DefaultMapName => "Mission_Data_Map";


    /// <summary>
    /// 미션 임포터 열기
    /// </summary>
    [MenuItem( "Tools/Importer/GameFlow/Mission" )]
    public static void Open ()
    {
        MissionImporterWindow window = GetWindow<MissionImporterWindow>( "Mission Data Importer" );
        window.minSize = new Vector2( 560f, 360f );
    }

    /// <summary>
    /// 단계 이름 입력 표시
    /// </summary>
    protected override void DrawOptions ()
    {
        _stageName = EditorGUILayout.TextField( "단계 이름 (빈 값은 보존)", _stageName );
    }

    /// <summary>
    /// 미션 후보와 Map 적용 준비
    /// </summary>
    /// <param name="rows">원본 행</param>
    /// <param name="report">검사 결과</param>
    /// <returns>적용 계획</returns>
    protected override DataImportPlan Prepare ( List<DataImportRow> rows, DataImportReport report )
    {
        return MissionAssetImporter.Prepare( rows, OutputFolder, DataMap as MissionDataMap, _stageName, Bindings, report, MapName );
    }
}
