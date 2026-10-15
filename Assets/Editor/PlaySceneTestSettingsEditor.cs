using UnityEditor;
using UnityEngine;

/// <summary>
/// 플레이 씬 테스트 설정 편집과 적용 상태 표시
/// </summary>
[CustomEditor( typeof( PlaySceneTestSettings ) )]
public class PlaySceneTestSettingsEditor : Editor
{
    /// <summary>
    /// 테스트 옵션과 이번 플레이의 적용 상태 표시
    /// </summary>
    public override void OnInspectorGUI ()
    {
        using ( new EditorGUI.DisabledScope( EditorApplication.isPlayingOrWillChangePlaymode == true ) )
            DrawDefaultInspector( );

        PlaySceneTestSettings settings = ( PlaySceneTestSettings ) target;		//테스트 설정
        if ( settings.GetComponent<PlayScene>( ) == null )
            EditorGUILayout.HelpBox( "같은 GameObject에 PlayScene을 연결해 주세요.", MessageType.Warning );

        if ( Application.isPlaying == true )
            DrawAppliedSettings( settings );
        else
            EditorGUILayout.HelpBox( "테스트 옵션은 다음 플레이 시작 시 적용됩니다.", MessageType.Info );
    }

    /// <summary>
    /// 이번 플레이에 적용된 테스트 상태 표시
    /// </summary>
    /// <param name="settings">플레이 씬의 테스트 설정</param>
    public static void DrawAppliedSettings ( PlaySceneTestSettings settings )
    {
        if ( settings == null )
        {
            EditorGUILayout.LabelField( "테스트 설정", "미부착 / 일반 실행" );
            return;
        }

        EditorGUILayout.LabelField( "이번 플레이의 테스트 설정", EditorStyles.boldLabel );
        using ( new EditorGUI.DisabledScope( true ) )
        {
            EditorGUILayout.Toggle( "콘텐츠 누락 허용", settings.AllowMissingItemContent );
            EditorGUILayout.Toggle( "AI 실행 제외", settings.DisableAi );
        }
    }
}
