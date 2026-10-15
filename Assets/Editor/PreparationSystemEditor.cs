using UnityEditor;
using UnityEngine;

/// <summary>
/// 파티 준비 디버그 인스펙터 - 결과 등록과 현재 배치 상태 조회
/// </summary>
[CustomEditor ( typeof ( PreparationSystem ) )]
public class PreparationSystemEditor : Editor
{

    public override void OnInspectorGUI ( )
    {
        DrawDefaultInspector ( );
        EditorGUILayout.Space ( );
        EditorGUILayout.LabelField ( "파티 준비 디버그" , EditorStyles.boldLabel );

        if ( Application.isPlaying == false )
        {
            EditorGUILayout.HelpBox ( "플레이 중 결과와 배치 상태를 표시합니다." , MessageType.Info );
            return;
        }

        PreparationSystem preparation = ( PreparationSystem ) target;
        EditorGUILayout.LabelField(
            "음식 등록 결과", preparation.ProducedFoodCount.ToString( ) );

        EditorGUILayout.LabelField(
            "음식 유효 배치", preparation.CurrentFoodCount.ToString( ) );

        EditorGUILayout.LabelField(
            "일반 아이템 기록(비활성 포함)",
            preparation.TrackedItemCount.ToString( ) );

        EditorGUILayout.LabelField(
            "마지막 준비 변경", preparation.LastPreparationDebug );
    }

    public override bool RequiresConstantRepaint ( ) => Application.isPlaying;

}
