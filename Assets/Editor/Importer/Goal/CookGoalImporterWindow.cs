using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// JSON / CSV 레시피 제작 미션 연결 임포터 창
/// </summary>
public class CookGoalImporterWindow : DataImporterWindow
{
    MissionDataMap _missionMap;		//참조 미션을 조회할 Map

    protected override string DataType => "CookGoalData";

    protected override string DisplayName => "제작 목표 임포터";

    protected override string DefaultOutputFolder => "Assets/03. Configs/!_Settings/!_GameFlow/Goal/Cook";

    protected override Type MapType => typeof( CookGoalDataMap );

    protected override string DefaultMapName => "Cook_Goal_Map";

    /// <summary>
    /// 선행 미션 항목에서 연결할 Map 종류
    /// </summary>
    public override Type ReferenceMapType => typeof( MissionDataMap );


    /// <summary>
    /// 제작 목표 임포터 열기
    /// </summary>
    [MenuItem( "Tools/Importer/GameFlow/Cook Goal" )]
    public static void Open ()
    {
        CookGoalImporterWindow window = GetWindow<CookGoalImporterWindow>( "Cook Goal Importer" );
        window.minSize = new Vector2( 560f, 360f );
    }


    /// <summary>
    /// 선행 미션 결과 Map 연결
    /// </summary>
    /// <param name="map">미션 Map 또는 연결 해제 null</param>
    /// <param name="locked">선행 연결 중 수동 변경 차단 여부</param>
    public override void SetReferenceMap ( ScriptableObject map, bool locked = false )
    {
        if ( map != null && map is not MissionDataMap )
            throw new ArgumentException( "MissionDataMap을 연결해 주세요." );
        ReferenceMapLocked = locked;
        if ( _missionMap == map )
            return;
        _missionMap = map as MissionDataMap;
        InvalidatePreview( );
    }

    /// <summary>
    /// 미션 참조 Map 선택 표시
    /// </summary>
    protected override void DrawOptions ()
    {
        using ( new EditorGUI.DisabledScope( ReferenceMapLocked == true ) )
            _missionMap = ( MissionDataMap )EditorGUILayout.ObjectField( "참조 미션 Map", _missionMap, typeof( MissionDataMap ), false );
    }

    /// <summary>
    /// 제작 목표와 참조 적용 준비
    /// </summary>
    /// <param name="rows">원본 행</param>
    /// <param name="report">검사 결과</param>
    /// <returns>적용 계획</returns>
    protected override DataImportPlan Prepare ( List<DataImportRow> rows, DataImportReport report )
    {
        return GoalDataAssetImporter.PrepareCook( rows, OutputFolder, DataMap as CookGoalDataMap, _missionMap, Bindings, report, MapName );
    }
}
