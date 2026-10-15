using UnityEditor;
using UnityEngine;

/// <summary>
/// 게임 흐름 디버그 인스펙터 - 적용 설정과 진행 상태, 단계별 목표 조회
/// </summary>
[CustomEditor ( typeof ( GameFlowController ) )]
public class GameFlowControllerEditor : Editor
{

    public override void OnInspectorGUI ( )
    {
        DrawDefaultInspector ( );
        EditorGUILayout.Space ( );
        EditorGUILayout.LabelField ( "게임 흐름 디버그" , EditorStyles.boldLabel );

        if ( Application.isPlaying == false )
        {
            EditorGUILayout.HelpBox ( "플레이 중 게임 진행과 미션 수량을 표시합니다." , MessageType.Info );
            return;
        }

        GameFlowController flow = ( GameFlowController ) target;
        DrawSettings ( flow );
        EditorGUILayout.Space ( );

        EditorGUILayout.LabelField ( "진행 상태" , flow.State.ToString ( ) );
        EditorGUILayout.LabelField ( "남은 시간" , flow.RemainingTime.ToString ( "F1" ) );
        string currentStage = flow.StageCount > 0
            ? $"{flow.CurrentStageIndex + 1} / {flow.StageCount}"
            : "없음";
        EditorGUILayout.LabelField ( "현재 단계" , currentStage );

        for ( int stage = 0 ; stage < flow.StageCount ; stage++ )
        {
            EditorGUILayout.LabelField (
                $"{stage + 1}단계: {flow.GetStageName ( stage )}" , EditorStyles.boldLabel );

            for ( int goal = 0 ; goal < flow.GetGoalCount ( stage ) ; goal++ )
            {
                flow.GetGoal ( stage , goal ,
                    out string goalName , out int current , out int required );
                EditorGUILayout.LabelField ( goalName , $"{current} / {required}" );
            }
        }
    }

    public override bool RequiresConstantRepaint ( ) => Application.isPlaying;


    /// <summary>
    /// 실제 선택된 난이도와 시스템 설정 표시
    /// </summary>
    /// <param name="flow">조회할 게임 흐름</param>
    public static void DrawSettings ( GameFlowController flow )
    {
        GameDifficultySettings settings = flow.Settings;		//실제 선택된 설정

        EditorGUILayout.LabelField (
            "적용 난이도 설정" , EditorStyles.boldLabel );

        if ( settings == null )
        {
            EditorGUILayout.LabelField ( "선택 상태" , "설정 준비 전" );
            return;
        }

        EditorGUILayout.LabelField (
            "난이도" , settings.Difficulty.ToString ( ) );

        using ( new EditorGUI.DisabledScope ( true ) )
        {
            EditorGUILayout.ObjectField (
                "FlowData" , settings.Flow ,
                typeof ( GameFlowData ) , false );

            EditorGUILayout.ObjectField (
                "제작" , settings.MinigameMap ,
                typeof ( ProduceMinigameDataMap ) , false );

            EditorGUILayout.ObjectField (
                "압력" , settings.Pressure ,
                typeof ( AnomalyPressureData ) , false );

            EditorGUILayout.ObjectField (
                "발생 목록" , settings.SpawnMap ,
                typeof ( AnomalySpawnDataMap ) , false );

            EditorGUILayout.ObjectField (
                "분위기" , settings.Mood ,
                typeof ( MoodData ) , false );

            EditorGUILayout.ObjectField (
                "평가" , settings.Score ,
                typeof ( ResultScoreData ) , false );
        }
    }
}
