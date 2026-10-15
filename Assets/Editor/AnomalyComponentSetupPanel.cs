using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 선택 대상의 기능 구성 미리보기와 적용 화면
/// </summary>
public class AnomalyComponentSetupPanel
{
    AnomalySetupKind _kind;		//추가할 기능
    readonly string[] _kindNames = AnomalyComponentSetupUtility.GetKindNames( );		//열거 순서의 화면 이름
    /// <summary>
    /// 미리보기한 대상 목록
    /// </summary>
    readonly List<GameObject> _targets = new( );
    /// <summary>
    /// 미리보기와 적용 결과
    /// </summary>
    readonly List<string> _results = new( );

    /// <summary>
    /// 선택 대상과 기능이 바뀌면 미리보기 해제
    /// </summary>
    public void Clear ()
    {
        _targets.Clear( );
        _results.Clear( );
    }

    /// <summary>
    /// 프리팹 원본을 열고 기능 구성 대상으로 선택
    /// </summary>
    /// <param name="path">열 원본 에셋 경로</param>
    /// <returns>편집 모드 열기 성공 여부</returns>
    public bool OpenPrefab ( string path )
    {
        PrefabStage stage = PrefabStageUtility.OpenPrefab( path );
        //다른 원본의 미저장 변경으로 전환이 취소되면 현재 선택 유지
        if ( stage == null )
            return false;
        Selection.activeGameObject = stage.prefabContentsRoot;
        Clear( );
        return true;
    }

    /// <summary>
    /// 구성할 대상 미리보기
    /// </summary>
    void Preview ()
    {
        Clear( );
        _targets.AddRange( Selection.gameObjects );
        foreach ( GameObject target in _targets )
            AnomalyComponentSetupUtility.Process( target, _kind, false, _results );
    }

    /// <summary>
    /// 미리보기한 대상에 선택 기능 적용
    /// </summary>
    void Apply ()
    {
        _results.Clear( );
        Undo.IncrementCurrentGroup( );
        int group = Undo.GetCurrentGroup( );
        Undo.SetCurrentGroupName( "변칙 기능 구성" );
        foreach ( GameObject target in _targets )
        {
            if ( target != null )
                AnomalyComponentSetupUtility.Process( target, _kind, true, _results );
        }
        Undo.CollapseUndoOperations( group );
        _targets.Clear( );
    }

    /// <summary>
    /// 구성 화면 표시
    /// </summary>
    public void Draw ()
    {
        EditorGUILayout.HelpBox( "Hierarchy 또는 Prefab Mode에서 오브젝트를 선택하세요. AnomalyTarget이 없는 대상도 구성합니다. 기존 참조는 유지하고 유일한 후보만 연결합니다. 결과의 수동 설정까지 끝나야 기능을 사용할 수 있습니다. 저장은 Unity에서 직접 진행합니다.", MessageType.Info );
        EditorGUI.BeginChangeCheck( );
        _kind = ( AnomalySetupKind ) EditorGUILayout.Popup( "기능 묶음", (int)_kind, _kindNames );
        if ( EditorGUI.EndChangeCheck( ) )
            Clear( );
        GameObject selected = Selection.activeGameObject;
        if ( selected != null && EditorUtility.IsPersistent( selected ) &&
             PrefabUtility.GetPrefabAssetType( selected ) != PrefabAssetType.Model )
        {
            using ( new EditorGUI.DisabledScope( EditorApplication.isPlayingOrWillChangePlaymode ) )
            {
                if ( GUILayout.Button( "선택 프리팹을 편집 모드에서 열기" ) )
                {
                    OpenPrefab( AssetDatabase.GetAssetPath( selected ) );
                    GUIUtility.ExitGUI( );
                }
            }
        }
        using ( new EditorGUI.DisabledScope( EditorApplication.isPlayingOrWillChangePlaymode ) )
        {
            if ( GUILayout.Button( "선택 대상 구성 미리보기" ) )
                Preview( );
            using ( new EditorGUI.DisabledScope( _targets.Count == 0 ) )
            {
                if ( GUILayout.Button( $"미리보기한 {_targets.Count}개 대상에 적용" ) )
                    Apply( );
            }
        }
        foreach ( string result in _results )
            EditorGUILayout.LabelField( result, EditorStyles.wordWrappedLabel );
    }
}
