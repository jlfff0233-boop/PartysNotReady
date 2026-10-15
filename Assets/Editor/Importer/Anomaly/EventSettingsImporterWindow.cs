using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// JSON / CSV 사건 시간 설정 임포터 창
/// </summary>
public class EventSettingsImporterWindow : DataImporterWindow
{
    protected override string DataType => "AnomalyEventSettings";
    protected override string DisplayName => "변칙 사건 설정 임포터";
    protected override string DefaultOutputFolder => "Assets/03. Configs/!_Settings/!_GameFlow/AnomalyEvent";
    protected override Type MapType => typeof( EventSettingsDataMap );
    protected override string DefaultMapName => "Event_Settings_Map";


    /// <summary>
    /// 사건 설정 임포터 열기
    /// </summary>
    [MenuItem( "Tools/Importer/Anomaly/Event Settings" )]
    public static void Open ()
    {
        EventSettingsImporterWindow window = GetWindow<EventSettingsImporterWindow>( "Event Settings Importer" );
        window.minSize = new Vector2( 560f, 360f );
    }

    /// <summary>
    /// 사건 시간과 종류별 Map 적용 준비
    /// </summary>
    /// <param name="rows">원본 행</param>
    /// <param name="report">검사 결과</param>
    /// <returns>적용 계획</returns>
    protected override DataImportPlan Prepare ( List<DataImportRow> rows, DataImportReport report )
    {
        return EventSettingsAssetImporter.Prepare( rows, OutputFolder, DataMap as EventSettingsDataMap, Bindings, report, MapName );
    }
}
