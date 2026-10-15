using UnityEditor;
using UnityEngine;

/// <summary>
/// 제작 관리 인스펙터 - 레시피와 그릇 상태 조회
/// </summary>
[CustomEditor( typeof( ProductionManager ) )]
public class ProductionManagerEditor : Editor
{

    /// <summary>
    /// 제작 설정과 런타임 상태 표시
    /// </summary>
    public override void OnInspectorGUI ()
    {
        DrawDefaultInspector( );

        ProductionManager presenter = target as ProductionManager;
        if ( presenter == null || Application.isPlaying == false )
            return;

        EditorGUILayout.Space( );
        EditorGUILayout.LabelField( "제작 관리 상태", EditorStyles.boldLabel );
        EditorGUILayout.LabelField( "초기화", presenter.IsReady.ToString( ) );
        EditorGUILayout.LabelField( "레시피 수",
            ( presenter.Recipes == null ? 0 : presenter.Recipes.Count ).ToString( ) );
        EditorGUILayout.LabelField( "그릇 수", presenter.Products.Count.ToString( ) );
    }

    public override bool RequiresConstantRepaint () => Application.isPlaying;

}
