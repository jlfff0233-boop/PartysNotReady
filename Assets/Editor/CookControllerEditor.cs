using UnityEditor;
using UnityEngine;

/// <summary>
/// 제작 그릇 디버그 인스펙터 - 현재 제작 상태와 결과 조회
/// </summary>
[CustomEditor ( typeof ( CookController ) )]
public class CookControllerEditor : Editor
{

    /// <summary>
    /// 제작 대상 Inspector
    /// </summary>
    public override void OnInspectorGUI ( )
    {
        DrawDefaultInspector ( );
        EditorGUILayout.Space ( );
        EditorGUILayout.LabelField ( "제작 디버그" , EditorStyles.boldLabel );

        if ( Application.isPlaying == false )
        {
            EditorGUILayout.HelpBox ( "플레이 중 현재 제작 상태를 표시합니다." , MessageType.Info );
            return;
        }

        CookController cook = ( CookController ) target;
        EditorGUILayout.LabelField ( "초기화" , cook.Session.IsReady.ToString ( ) );
        EditorGUILayout.LabelField ( "상태" , cook.Session.State.ToString ( ) );
        EditorGUILayout.LabelField ( "그릇 보유" , cook.IsBowlHeld.ToString ( ) );
        EditorGUILayout.LabelField ( "등록 재료" , cook.Session.RegisteredIngredientCount.ToString ( ) );
        EditorGUILayout.LabelField ( "성공" , $"{cook.Session.SuccessCount} / {cook.Session.RequiredSuccessCount}" );
        EditorGUILayout.LabelField ( "미니게임 가능" , cook.Session.Minigame.CanPlayMinigame.ToString ( ) );
        EditorGUILayout.LabelField ( "폐기할 내용물" , cook.HasDiscardable.ToString ( ) );
        EditorGUILayout.LabelField ( "완료 결과 ID" , cook.Session.CompletedResult?.Id ?? "없음" );
        EditorGUILayout.LabelField ( "마지막 변경" , cook.LastDebugEvent );
    }

    /// <summary>
    /// 플레이 중 Inspector 갱신 여부
    /// </summary>
    /// <returns>갱신 여부</returns>
    public override bool RequiresConstantRepaint ( )
    {
        return Application.isPlaying;
    }

}
