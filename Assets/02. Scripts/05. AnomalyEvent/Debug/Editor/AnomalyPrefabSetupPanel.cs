using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 프리팹 일괄 구성 화면 - 원본과 기능 선택, 미리보기와 일괄 적용
/// </summary>
public class AnomalyPrefabSetupPanel
{
    /// <summary>
    /// 일괄 구성할 원본 에셋 경로
    /// </summary>
    readonly HashSet<string> _selected = new( );
    /// <summary>
    /// 미리보기에서 구성 가능한 원본 에셋 경로
    /// </summary>
    readonly List<string> _preview = new( );
    /// <summary>
    /// 미리보기에서 일괄 적용이 제외된 원본 에셋 경로
    /// </summary>
    readonly List<string> _excluded = new( );
    /// <summary>
    /// 원본 경로별 미리보기 제외 사유
    /// </summary>
    readonly Dictionary<string, string> _excludeReasons = new( );
    /// <summary>
    /// 원본별 부착 예정과 적용 결과
    /// </summary>
    readonly List<string> _results = new( );
    /// <summary>
    /// 원본 경로별 미리보기 시점의 내용 확인값
    /// </summary>
    readonly Dictionary<string, string> _previewHashes = new( );
    /// <summary>
    /// 외형 구성 미리보기의 원본과 연관 에셋 내용 확인값
    /// </summary>
    readonly Dictionary<string, string> _dependencyHashes = new( );
    readonly AnomalyPrefabOptions _options = new( );		//선택할 공통 설정
    readonly AnomalyPrefabBackupPanel _backup = new( );		//원본 복구 화면
    readonly AnomalyPrefabReferencePanel _references = new( );		//원본별 참조 선택 화면
    readonly string[] _names = AnomalyComponentSetupUtility.GetKindNames( );		//기능 표시 이름
    readonly AnomalySetupKind[] _kinds = (AnomalySetupKind[])Enum.GetValues( typeof( AnomalySetupKind ) );		//기능 선택 순서
    readonly bool[] _checked = new bool[Enum.GetValues( typeof( AnomalySetupKind ) ).Length];		//기능별 선택 여부
    bool _showResults = true;		//일괄 구성 결과 펼침 여부
    bool _changeLayer;		//선택한 원본의 레이어 변경 여부
    int _layer;		//적용할 일반 오브젝트 레이어
    bool _includeChildren = true;		//비활성 자식까지 레이어 변경
    DefaultAsset _moveFolder;		//원본 이동 폴더

    /// <summary>
    /// 창 활성화 시 기본 레이어와 이동 폴더 연결
    /// </summary>
    public void Init ()
    {
        //Unity API는 창 생성자가 아닌 OnEnable에서 호출
        _layer = LayerMask.NameToLayer( "InteractableItem" );
        _moveFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>( "Assets/04. Prefabs" );
        _options.Init( );
        _backup.Reload( );
    }

    /// <summary>
    /// 이전 미리보기와 결과 해제
    /// </summary>
    void ClearPreview ()
    {
        _preview.Clear( );
        _excluded.Clear( );
        _excludeReasons.Clear( );
        _results.Clear( );
        _previewHashes.Clear( );
        _dependencyHashes.Clear( );
    }

    /// <summary>
    /// 선택 원본 전체 해제
    /// </summary>
    public void ClearSelection ()
    {
        _selected.Clear( );
        _references.Clear( );
        ClearPreview( );
    }


    /// <summary>
    /// 원본 선택 변경과 이전 미리보기 해제
    /// </summary>
    /// <param name="path">선택할 원본 에셋 경로</param>
    /// <param name="selected">선택 여부</param>
    public void SetSelected ( string path, bool selected )
    {
        bool changed = selected == true ? _selected.Add( path ) : _selected.Remove( path );
        if ( changed == true )
        {
            _references.Retain( _selected );
            ClearPreview( );
        }
    }

    /// <summary>
    /// 갱신된 검색 범위에 없는 선택 원본 정리
    /// </summary>
    /// <param name="entries">현재 검색 범위의 원본 목록</param>
    public void RetainSelection ( List<AnomalyPrefabEntry> entries )
    {
        if ( _selected.RemoveWhere( path => entries.Exists( entry => entry.Path == path ) == false ) > 0 )
        {
            _references.Retain( _selected );
            ClearPreview( );
        }
    }

    /// <summary>
    /// 선택한 기능 묶음 조회
    /// </summary>
    /// <returns>열거 순서의 선택 기능 목록</returns>
    AnomalySetupKind[] GetSelectedKinds ()
    {
        List<AnomalySetupKind> kinds = new( );
        for ( int i = 0; i < _kinds.Length; i++ )
        {
            if ( _checked[i] == true )
                kinds.Add( _kinds[i] );
        }
        return kinds.ToArray( );
    }

    /// <summary>
    /// 선택 원본의 기능 구성 미리보기
    /// </summary>
    void Preview ()
    {
        ClearPreview( );
        AnomalyPrefabReferenceChecker.Begin( );
        List<string> paths = new( _selected );
        paths.Sort( StringComparer.OrdinalIgnoreCase );
        AnomalySetupKind[] kinds = GetSelectedKinds( );
        if ( _options.BuildVisual == true )
        {
            AnomalyPrefabMigrationUtility.Process( paths, kinds, _changeLayer == true ? _layer : -1, _includeChildren,
                _options, _references, false, _dependencyHashes, _preview, _excludeReasons, _results );
            _excluded.AddRange( _excludeReasons.Keys );
            _excluded.Sort( StringComparer.OrdinalIgnoreCase );
            _results.Add( $"미리보기 대상 {paths.Count}개 / 적용 가능 {_preview.Count}개 / 제외 {_excluded.Count}개" );
            return;
        }
        foreach ( string path in paths )
        {
            int start = _results.Count;
            if ( AnomalyPrefabSetupUtility.Process(
                path, kinds, false, _results, _changeLayer == true ? _layer : -1, _includeChildren, _options, null,
                _references.Get( path ) ) == true )
            {
                _preview.Add( path );
                _previewHashes.Add( path, AnomalyPrefabBackupUtility.GetHash( path ) );
            }
            else
            {
                _excluded.Add( path );
                //이번 원본에서 기록한 제외 사유를 경로와 함께 표시
                List<string> reasons = _results.GetRange( start, _results.Count - start )
                    .FindAll( result => result.Contains( "제외:" ) );
                _excludeReasons.Add( path, string.Join( "\n", reasons ) );
            }
        }
        _results.Add( $"미리보기 대상 {paths.Count}개 / 적용 가능 {_preview.Count}개 / 제외 {_excluded.Count}개" );
    }

    /// <summary>
    /// 미리보기한 원본들에 선택 기능 부착과 저장
    /// </summary>
    void Apply ()
    {
        string[] paths = _preview.ToArray( );
        Dictionary<string, string> hashes = new( _previewHashes );
        Dictionary<string, string> dependencies = new( _dependencyHashes );
        AnomalySetupKind[] kinds = GetSelectedKinds( );
        ClearPreview( );
        AnomalyPrefabReferenceChecker.Begin( );
        int applied = 0;
        AnomalyPrefabBackupBatch batch = AnomalyPrefabBackupUtility.CreateBatch( );
        try
        {
            if ( _options.BuildVisual == true )
            {
                bool success = AnomalyPrefabMigrationUtility.Process( new List<string>( paths ), kinds,
                    _changeLayer == true ? _layer : -1, _includeChildren, _options, _references, true,
                    dependencies, new List<string>( ), _excludeReasons, _results );
                _results.Add( success == true ? $"원본 {paths.Length}개와 연관 에셋 일괄 저장 완료" : "일괄 저장을 완료하지 못했습니다. 처리 및 복구 결과를 확인하세요." );
                return;
            }
            foreach ( string path in paths )
            {
                if ( File.Exists( path ) == false || AnomalyPrefabBackupUtility.GetHash( path ) != hashes[path] )
                {
                    _results.Add( "제외: 미리보기 후 원본이 변경됐습니다. 다시 미리보기하세요. " + path );
                    continue;
                }
                if ( AnomalyPrefabSetupUtility.Process(
                    path, kinds, true, _results, _changeLayer == true ? _layer : -1, _includeChildren, _options, batch,
                    _references.Get( path ) ) == true )
                    applied++;
            }
            _results.Add( $"원본 저장 {applied}개 / 제외 또는 저장 실패 {paths.Length - applied}개" );
        }
        finally
        {
            //예외로 중단되어도 저장한 원본의 복구 기록을 남기고 선택 해제
            _selected.Clear( );
            _references.Clear( );
            Array.Clear( _checked, 0, _checked.Length );
            _changeLayer = false;
            _options.ClearSelection( );
            _backup.Reload( );
        }
    }

    /// <summary>
    /// 선택한 변칙 대상 원본을 지정 폴더로 이동하고 선택 해제
    /// </summary>
    /// <param name="folder">이동할 기존 에셋 폴더 경로</param>
    void MoveSelected ( string folder )
    {
        List<string> paths = new( _selected );
        paths.Sort( StringComparer.OrdinalIgnoreCase );
        ClearPreview( );
        int moved = 0;
        foreach ( string path in paths )
        {
            if ( AnomalyPrefabSetupUtility.Move( path, folder, _results ) == true )
                moved++;
        }
        _results.Add( $"원본 이동 {moved}개 / 제외 또는 이동 실패 {paths.Count - moved}개" );
        _selected.Clear( );
        _references.Clear( );
        Array.Clear( _checked, 0, _checked.Length );
        _changeLayer = false;
        _options.ClearSelection( );
    }

    /// <summary>
    /// 원본 다중 선택과 선택 해제 도구 표시
    /// </summary>
    /// <param name="visible">현재 필터에 표시된 원본 목록</param>
    public void DrawSelectionControls ( List<AnomalyPrefabEntry> visible )
    {
        int visibleSelected = visible.FindAll( entry => IsSelected( entry.Path ) == true ).Count;
        EditorGUILayout.LabelField( $"일괄 구성 원본 선택 {_selected.Count}개 / 필터로 숨겨진 선택 {_selected.Count - visibleSelected}개", EditorStyles.boldLabel );
        using ( new EditorGUILayout.HorizontalScope( ) )
        {
            if ( GUILayout.Button( "표시된 원본 전체 선택" ) )
            {
                ClearSelection( );
                foreach ( AnomalyPrefabEntry entry in visible )
                    _selected.Add( entry.Path );
            }
            if ( GUILayout.Button( "원본 선택 해제" ) )
                ClearSelection( );
        }
    }

    /// <summary>
    /// 기능 부착과 참조 지정, 레이어 변경과 폴더 이동 도구 표시
    /// </summary>
    /// <param name="focusedPath">오른쪽에서 참조를 설정할 원본 경로</param>
    /// <returns>일괄 적용 실행 여부</returns>
    public bool DrawControls ( string focusedPath )
    {
        EditorGUILayout.LabelField( $"일괄 적용 대상 {_selected.Count}개", EditorStyles.boldLabel );
        EditorGUILayout.LabelField( "부착할 기능 묶음 (복수 선택)" );
        EditorGUI.BeginChangeCheck( );
        for ( int i = 0; i < _kinds.Length; i += 3 )
        {
            using ( new EditorGUILayout.HorizontalScope( ) )
            {
                for ( int j = i; j < Math.Min( i + 3, _kinds.Length ); j++ )
                    _checked[j] = EditorGUILayout.ToggleLeft( _names[j], _checked[j] );
            }
        }
        EditorGUILayout.Space( );
        _changeLayer = EditorGUILayout.ToggleLeft( "레이어 일괄 변경", _changeLayer );
        using ( new EditorGUI.DisabledScope( _changeLayer == false ) )
        {
            _layer = EditorGUILayout.LayerField( "대상 레이어", _layer );
            _includeChildren = EditorGUILayout.ToggleLeft( "모든 자식 포함 (비활성 포함)", _includeChildren );
        }
        _options.Draw( );
        if ( EditorGUI.EndChangeCheck( ) )
            ClearPreview( );
        using ( new EditorGUI.DisabledScope( EditorApplication.isPlayingOrWillChangePlaymode ) )
        {
            if ( _references.Draw( _selected, GetSelectedKinds( ), focusedPath ) == true )
                ClearPreview( );
        }
        if ( _changeLayer == true )
            EditorGUILayout.HelpBox( "기능 부착 없이 오브젝트 레이어만 변경할 수 있습니다. 자식 포함을 켜면 비활성 자식도 변경하지만, 연결된 빈자리 조사 영역의 별도 레이어는 유지합니다. 렌더링 레이어는 변경하지 않습니다.", MessageType.Info );
        EditorGUILayout.HelpBox( "선택한 기능은 원본 루트에 부착합니다. 공통 설정은 내부 변칙 대상에도 적용합니다. 외형 구성 시 기존 원본을 복제해 보관하고 맵의 참조와 배치별 설정도 함께 이전합니다. 변형 프리팹은 독립 일반 프리팹으로 전환합니다. 미리보기에서는 저장하지 않으며, 필터로 숨겨진 선택도 포함됩니다.", MessageType.Info );
        using ( new EditorGUI.DisabledScope( EditorApplication.isPlayingOrWillChangePlaymode ) )
        {
            using ( new EditorGUI.DisabledScope(
                _selected.Count == 0 ||
                ( GetSelectedKinds( ).Length == 0 && _changeLayer == false && _options.HasSelection == false && _references.HasSelection == false ) ||
                ( _changeLayer == true && _layer < 0 ) ) )
            {
                if ( GUILayout.Button( "선택 원본 일괄 구성 미리보기" ) )
                    Preview( );
            }
            if ( _preview.Count + _excluded.Count > 0 )
            {
                EditorGUILayout.HelpBox(
                    $"미리보기 대상 {_preview.Count + _excluded.Count}개 / 적용 가능 {_preview.Count}개 / 제외 {_excluded.Count}개",
                    _excluded.Count > 0 ? MessageType.Warning : MessageType.Info );
                foreach ( string path in _excluded )
                {
                    using ( new EditorGUILayout.VerticalScope( EditorStyles.helpBox ) )
                    {
                        EditorGUILayout.LabelField( Path.GetFileNameWithoutExtension( path ), EditorStyles.boldLabel );
                        EditorGUILayout.LabelField( path, EditorStyles.wordWrappedLabel );
                        EditorGUILayout.HelpBox( _excludeReasons[path], MessageType.Warning );
                    }
                }
            }
            using ( new EditorGUI.DisabledScope( _preview.Count == 0 ) )
            {
                if ( GUILayout.Button( $"적용 가능한 {_preview.Count}개 원본에 일괄 적용 및 저장" ) )
                {
                    Apply( );
                    return true;
                }
            }
            EditorGUILayout.Space( );
            EditorGUILayout.LabelField( "선택한 변칙 대상 원본 이동", EditorStyles.boldLabel );
            _moveFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                "이동 폴더", _moveFolder, typeof( DefaultAsset ), false );
            string folder = AssetDatabase.GetAssetPath( _moveFolder );
            bool validFolder = folder.StartsWith( "Assets/", StringComparison.Ordinal ) &&
                AssetDatabase.IsValidFolder( folder );
            EditorGUILayout.HelpBox( "원본 이름과 참조를 유지하며 지정 폴더로 이동합니다. 변칙 대상이 없는 원본과 이름이 충돌하는 원본은 제외합니다. 이동 후 기존 검색 폴더에서는 목록에서 빠질 수 있습니다.", MessageType.Info );
            using ( new EditorGUI.DisabledScope( _selected.Count == 0 || validFolder == false ) )
            {
                if ( GUILayout.Button( $"선택 원본 {_selected.Count}개 지정 폴더로 이동" ) )
                {
                    MoveSelected( folder );
                    return true;
                }
            }
        }
        _backup.Draw( );
        return false;
    }

    /// <summary>
    /// 미리보기와 원본별 적용 결과 표시
    /// </summary>
    public void DrawResults ()
    {
        if ( _results.Count == 0 )
            return;
        _showResults = EditorGUILayout.Foldout( _showResults, "일괄 구성 미리보기 / 적용 결과", true );
        if ( _showResults == false )
            return;
        if ( GUILayout.Button( "전체 결과 복사" ) )
            EditorGUIUtility.systemCopyBuffer = string.Join( "\n", _results );
        //오른쪽 설정 화면의 스크롤을 사용하여 결과 영역이 눌리지 않게 표시
        foreach ( string result in _results )
            EditorGUILayout.LabelField( result, EditorStyles.wordWrappedLabel );
    }

    /// <summary>
    /// 원본의 일괄 구성 선택 여부 조회
    /// </summary>
    /// <param name="path">조회할 원본 에셋 경로</param>
    /// <returns>선택 여부</returns>
    public bool IsSelected ( string path )
    {
        return _selected.Contains( path );
    }

}
