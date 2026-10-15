using UnityEditor;
using UnityEngine;

/// <summary>
/// 플레이 씬 검증 버튼과 진행 상태 표시
/// </summary>
[CustomEditor( typeof( PlayScene ) )]
public class PlaySceneEditor : Editor
{

    /// <summary>
    /// 플레이 검증 기능 표시
    /// </summary>
    public override void OnInspectorGUI ()
    {
        DrawDefaultInspector( );
        if ( GUILayout.Button( "플레이 준비 점검 열기" ) )
            PlayReadinessWindow.Open( ( PlayScene ) target );
        if ( GUILayout.Button( "플레이 디버그 열기" ) )
            PlayDebugWindow.Open( ( PlayScene ) target );
        EditorGUILayout.Space( );
        EditorGUILayout.LabelField( "플레이 검증", EditorStyles.boldLabel );

        if ( Application.isPlaying == false )
        {
            EditorGUILayout.HelpBox( "플레이 중 검증 버튼을 사용할 수 있습니다.", MessageType.Info );
            return;
        }

        PlayScene scene = ( PlayScene ) target;
        PlaySceneTestSettingsEditor.DrawAppliedSettings(
            scene.GetComponent<PlaySceneTestSettings>( ) );
        serializedObject.Update( );
        GameFlowController flow = serializedObject.FindProperty(
            "_flow" ).objectReferenceValue as GameFlowController;

        if ( flow == null )
        {
            EditorGUILayout.HelpBox( "GameFlowController 연결을 확인해 주세요.", MessageType.Warning );
        }
        else
        {
            EditorGUILayout.LabelField( "진행 상태", flow.State.ToString( ) );
            EditorGUILayout.LabelField( "남은 시간", flow.RemainingTime.ToString( "F1" ) );

            if ( flow.Result != null )
                EditorGUILayout.LabelField( "종료 판정", flow.Result.Outcome.ToString( ) );
        }

        bool canProgress = flow != null &&
            ( flow.State == GameFlowState.Playing ||
              flow.State == GameFlowState.FinalRush );

        //진행 중에만 시간과 미션 조작 허용
        using ( new EditorGUI.DisabledScope( canProgress == false ) )
        {
            if ( GUILayout.Button( "Final Rush 진입" ) )
                scene.DebugFinalRush( );

            if ( GUILayout.Button( "현재 미션 단계 완료" ) )
                scene.DebugMissionStage( );

            if ( GUILayout.Button( "전체 미션 완료" ) )
                scene.DebugAllMissions( );

            if ( GUILayout.Button( "시간 만료" ) )
                scene.DebugTimeExpired( );

            if ( GUILayout.Button( new GUIContent( "NPC 전원 이탈 조건",
                "종료 조건만 주입합니다. 실제 NPC 상태와 Safety 평가는 변경하지 않습니다." ) ) )
                scene.DebugNpcDeparted( );
        }

        //결과 확인 뒤에도 같은 인스펙터에서 재시작
        if ( GUILayout.Button( "재시작" ) )
            scene.DebugRestart( );
    }

    /// <summary>
    /// 플레이 중 상태 갱신
    /// </summary>
    public override bool RequiresConstantRepaint () => Application.isPlaying;

}
