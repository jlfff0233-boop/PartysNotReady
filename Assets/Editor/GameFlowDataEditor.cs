using UnityEditor;
using UnityEngine;

/// <summary>
/// 게임 흐름 데이터 인스펙터 - 미션 설정 검사와 결과 표시
/// </summary>
[CustomEditor ( typeof ( GameFlowData ) )]
public class GameFlowDataEditor : Editor
{
    bool _hasChecked;		//검사 결과 표시 여부
    bool _isValid;		//설정 유효 여부
    string _reason = string.Empty;		//설정 오류

    /// <summary>
    /// 설정 필드와 검사 기능 표시
    /// </summary>
    public override void OnInspectorGUI ( )
    {
        if ( DrawDefaultInspector ( ) == true )
            ClearCheckResult ( );

        EditorGUILayout.Space ( );
        EditorGUILayout.LabelField ( "미션 설정 검사" , EditorStyles.boldLabel );

        if ( GUILayout.Button ( "미션 설정 검사" ) == true )
            CheckSettings ( );

        DrawCheckResult ( );
    }

    /// <summary>
    /// 미션 설정 검사와 결과 저장
    /// </summary>
    void CheckSettings ( )
    {
        GameFlowData data = ( GameFlowData ) target;		//검사할 흐름 데이터
        _isValid = data.CheckSettings ( out _reason );
        _hasChecked = true;
    }

    /// <summary>
    /// 검사 결과 표시
    /// </summary>
    void DrawCheckResult ( )
    {
        if ( _hasChecked == false )
            return;

        if ( _isValid == true )
        {
            EditorGUILayout.HelpBox ( "미션 설정 검사 통과" , MessageType.Info );
        }
        else
        {
            EditorGUILayout.HelpBox ( _reason , MessageType.Error );
        }
    }

    /// <summary>
    /// 이전 검사 결과 초기화
    /// </summary>
    void ClearCheckResult ( )
    {
        _hasChecked = false;
        _isValid = false;
        _reason = string.Empty;
    }
}
