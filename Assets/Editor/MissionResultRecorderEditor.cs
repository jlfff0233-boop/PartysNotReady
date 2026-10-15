using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 미션 디버그 인스펙터 - 마지막 미션 반영 결과 조회
/// </summary>
[CustomEditor ( typeof ( MissionResultRecorder ) )]
public class MissionResultRecorderEditor : Editor
{
    /// <summary>
    /// 미션 입력 종류 조회 딕셔너리(미션 ID, 입력 종류)
    /// </summary>
    Dictionary<string, string> _inputTypes = new( );

    public override void OnInspectorGUI ( )
    {
        DrawDefaultInspector ( );
        EditorGUILayout.Space ( );
        EditorGUILayout.LabelField ( "미션 디버그" , EditorStyles.boldLabel );
        DrawDuplicateGoals ( );

        if ( Application.isPlaying == false )
        {
            EditorGUILayout.HelpBox ( "플레이 중 마지막 미션 반영 결과를 표시합니다." , MessageType.Info );
            return;
        }

        MissionResultRecorder mission = ( MissionResultRecorder ) target;
        EditorGUILayout.LabelField ( "마지막 반영" , mission.LastMissionDebug );
    }


    public override bool RequiresConstantRepaint ( ) => Application.isPlaying;


    /// <summary>
    /// 서로 다른 입력 종류의 미션 ID 충돌 표시
    /// </summary>
    /// <param name="mission">검사할 미션</param>
    /// <param name="inputType">미션 입력 종류</param>
    void CheckDuplicateGoal ( MissionData mission, string inputType )
    {
        if ( mission == null ||
             string.IsNullOrWhiteSpace( mission.Id ) == true )
            return;

        if ( _inputTypes.TryGetValue(
            mission.Id, out string previousType ) == false )
        {
            _inputTypes.Add( mission.Id, inputType );
            return;
        }

        //같은 종류의 여러 설정이 하나의 미션에 연결되는 구성 허용
        if ( previousType == inputType )
            return;

        EditorGUILayout.HelpBox(
            $"미션 ID가 입력 종류 사이에서 중복됩니다: {mission.Id}\n" +
            $"{previousType} / {inputType}. 서로 다른 ID를 연결해 주세요.",
            MessageType.Warning );
    }

    /// <summary>
    /// 제작과 음식/일반 아이템 배치의 미션 ID 충돌 확인
    /// </summary>
    void DrawDuplicateGoals ( )
    {
        _inputTypes.Clear( );

        //제작 미션 설정
        CookGoalDataMap cookMap = serializedObject
            .FindProperty ( "_cookGoalMap" )?.objectReferenceValue as CookGoalDataMap;

        if ( cookMap != null && cookMap.Data != null )
        {
            foreach ( CookGoalData cooking in cookMap.Data )
            {
                if ( cooking != null )
                    CheckDuplicateGoal( cooking.Mission, "제작" );
            }
        }

        //음식과 일반 아이템 배치 설정
        PreparationSystem preparation = serializedObject
            .FindProperty ( "_preparation" )?.objectReferenceValue as PreparationSystem;

        if ( preparation == null )
            return;

        SerializedObject preparationObject = new ( preparation );
        FoodPlacementDataMap foodMap = preparationObject
            .FindProperty ( "_foodMap" )?.objectReferenceValue as FoodPlacementDataMap;

        ItemPlacementMissionDataMap itemMap = preparationObject
            .FindProperty( "_itemMap" )?.objectReferenceValue as ItemPlacementMissionDataMap;

        if ( foodMap != null && foodMap.Data != null )
        {
            foreach ( FoodPlacementData placement in foodMap.Data )
            {
                if ( placement != null )
                    CheckDuplicateGoal( placement.PlacementMission, "음식 배치" );
            }
        }

        if ( itemMap != null && itemMap.Data != null )
        {
            foreach ( ItemPlacementMissionData placement in itemMap.Data )
            {
                if ( placement != null )
                    CheckDuplicateGoal( placement.PlacementMission, "일반 아이템 배치" );
            }
        }
    }
}
