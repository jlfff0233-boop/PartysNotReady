using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 일반 배치 미션 임포터 - 아이템과 미션, 인정 장소 및 설정 Map 연결
/// </summary>
public class ItemPlacementImporterWindow : DataImporterWindow
{
    MissionDataMap _missionMap;      //미션 참조 Map

    protected override string DataType => "ItemPlacementMissionData";
    protected override string DisplayName => "일반 아이템 배치 미션 임포터";
    protected override string DefaultOutputFolder => "Assets/03. Configs/!_GameFlow/Goal/ItemPlacement";
    protected override Type MapType => typeof( ItemPlacementMissionDataMap );
    protected override string DefaultMapName => "Item_Placement_Mission_Map";

    /// <summary>
    /// 선행 미션 항목에서 연결할 Map 종류
    /// </summary>
    public override Type ReferenceMapType => typeof( MissionDataMap );

    /// <summary>
    /// 일반 배치 미션 임포터 열기
    /// </summary>
    [MenuItem( "Tools/Importer/GameFlow/Item Placement" )]
    public static void Open ()
    {
        ItemPlacementImporterWindow window = GetWindow<ItemPlacementImporterWindow>( "일반 배치 미션 임포터" );
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
    /// 참조 미션 Map 선택 표시
    /// </summary>
    protected override void DrawOptions ()
    {
        using ( new EditorGUI.DisabledScope( ReferenceMapLocked == true ) )
            _missionMap = ( MissionDataMap )EditorGUILayout.ObjectField( "참조 미션 Map", _missionMap, typeof( MissionDataMap ), false );
    }

    /// <summary>
    /// 아이템과 배치 미션 적용 계획 준비
    /// </summary>
    /// <param name="rows">원본 행</param>
    /// <param name="report">검사 결과</param>
    /// <returns>설정과 Map 적용 계획</returns>
    protected override DataImportPlan Prepare ( List<DataImportRow> rows, DataImportReport report )
    {
        return ItemPlacementAssetImporter.PreparePlacement( rows, OutputFolder,
            DataMap as ItemPlacementMissionDataMap, _missionMap, Bindings, report, MapName );
    }
}

