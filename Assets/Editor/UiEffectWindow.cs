using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// UI 연출 관리 창 - 대상 조회, 일괄 편집과 미리보기
/// </summary>
public class UiEffectWindow : EditorWindow
{
    /// <summary>
    /// 현재 범위의 UI 오브젝트 목록
    /// </summary>
    List<GameObject> _targets = new( );

    /// <summary>
    /// 일괄 편집할 UI 오브젝트 집합
    /// </summary>
    HashSet<GameObject> _selected = new( );

    /// <summary>
    /// 계층 표시의 최상위 노드 목록
    /// </summary>
    List<TargetNode> _roots = new( );

    /// <summary>
    /// 접힌 부모 오브젝트 집합
    /// </summary>
    HashSet<GameObject> _collapsed = new( );

    /// <summary>
    /// 부모 경로와 편집 대상을 함께 표시할 계층 정보
    /// </summary>
    class TargetNode
    {
        public GameObject Target;		//표시할 오브젝트
        public bool IsTarget;		//UI 편집 대상 여부
        public bool IsMatch;		//자신의 필터 일치 여부
        public bool IsVisible;		//자식을 포함한 표시 여부
        public int EffectCount;		//현재 종류의 부착 개수

        /// <summary>
        /// 하위 계층의 표시 노드 목록
        /// </summary>
        public List<TargetNode> Children = new( );
    }

    UiEffectKind _kind;		//편집할 연출 종류
    string _search = string.Empty;		//이름 검색
    bool _onlyMissing;		//미부착 대상만 표시
    Vector2 _scroll;		//목록 스크롤
    Editor _settingsEditor;		//혼합 값 편집기

    private void OnEnable ()
    {
        Undo.undoRedoPerformed += RefreshTargets;
        RefreshTargets( );
    }

    private void OnDisable ()
    {
        Undo.undoRedoPerformed -= RefreshTargets;
        UiEffectSetupUtility.StopPreview( );
        ClearEditor( );
    }

    /// <summary>
    /// 편집 화면 표시
    /// </summary>
    private void OnGUI ()
    {
        EditorGUILayout.HelpBox(
            "열린 씬 또는 현재 Prefab Stage의 비활성 UI까지 조회합니다.\n" +
            "추가 후 시각 대상 참조를 확인해 주세요. 저장은 Unity에서 진행합니다.\n" +
            "미리보기는 편집 모드의 활성 UI에서 2초간 실행하고 복구합니다.",
            MessageType.Info );

        //조회 종류와 목록 필터
        EditorGUI.BeginChangeCheck( );

        _kind = ( UiEffectKind ) EditorGUILayout.EnumPopup( "연출 종류", _kind );

        if ( EditorGUI.EndChangeCheck( ) == true )
        {
            UiEffectSetupUtility.StopPreview( );
            RefreshEditor( );
        }

        _search = EditorGUILayout.TextField( "이름 검색", _search );
        _onlyMissing = EditorGUILayout.Toggle( "미부착만", _onlyMissing );

        if ( GUILayout.Button( "목록 새로고침" ) == true )
            RefreshTargets( );

        //부착 상태와 일괄 편집 대상 선택
        DrawTargets( );

        using ( new EditorGUI.DisabledScope(
            EditorApplication.isPlayingOrWillChangePlaymode == true ) )
        {
            EditorGUILayout.BeginHorizontal( );

            //컴포넌트 구성 변경은 편집 모드에서만 허용
            if ( GUILayout.Button( "선택 대상 추가" ) == true )
                ApplyComponents( true );

            if ( GUILayout.Button( "선택 대상 삭제" ) == true )
                ApplyComponents( false );

            EditorGUILayout.EndHorizontal( );

            EditorGUILayout.BeginHorizontal( );

            //실제 UI 기능을 호출하지 않고 시각 연출만 실행
            if ( GUILayout.Button( "선택 대상 미리보기" ) == true )
                UiEffectSetupUtility.Preview( _selected, _kind );

            if ( GUILayout.Button( "미리보기 중단 / 복구" ) == true )
                UiEffectSetupUtility.StopPreview( );

            EditorGUILayout.EndHorizontal( );
        }

        if ( _settingsEditor == null )
            return;

        EditorGUILayout.Space( );
        EditorGUILayout.LabelField(
            "선택한 연출 설정", EditorStyles.boldLabel );

        if ( EditorApplication.isPlaying == true )
        {
            EditorGUILayout.HelpBox(
                "플레이 중 수정은 임시 변경입니다.",
                MessageType.Info );
        }

        //설정 편집 전 미리보기값이 저장되지 않도록 복구
        if ( Event.current.type == EventType.MouseDown ||
             Event.current.type == EventType.KeyDown )
            UiEffectSetupUtility.StopPreview( );

        //같은 종류의 선택 컴포넌트를 혼합 값으로 편집
        _settingsEditor.OnInspectorGUI( );
    }


    /// <summary>
    /// 연출 관리 창 열기
    /// </summary>
    [MenuItem( "Tools/UI/연출 관리" )]
    static void Open ()
    {
        GetWindow<UiEffectWindow>( "UI 연출 관리" );
    }

    /// <summary>
    /// 설정 편집기 해제
    /// </summary>
    void ClearEditor ()
    {
        if ( _settingsEditor != null )
            DestroyImmediate( _settingsEditor );

        _settingsEditor = null;
    }

    /// <summary>
    /// 부모 경로를 포함한 노드 조회와 생성
    /// </summary>
    /// <param name="target">표시할 오브젝트</param>
    /// <param name="nodes">오브젝트별 생성 노드</param>
    /// <returns>대상의 계층 노드</returns>
    TargetNode GetTargetNode (
        GameObject target, Dictionary<GameObject, TargetNode> nodes )
    {
        if ( nodes.TryGetValue( target, out TargetNode node ) == true )
            return node;

        node = new TargetNode { Target = target };
        nodes.Add( target, node );

        Transform parent = target.transform.parent;		//표시할 부모 경로

        if ( parent == null )
            _roots.Add( node );
        else
            GetTargetNode( parent.gameObject, nodes ).Children.Add( node );

        return node;
    }

    /// <summary>
    /// 조회한 UI를 실제 부모 계층으로 구성
    /// </summary>
    void BuildHierarchy ()
    {
        _roots.Clear( );

        Dictionary<GameObject, TargetNode> nodes = new( );		//오브젝트별 표시 노드

        foreach ( GameObject target in _targets )
            GetTargetNode( target, nodes ).IsTarget = true;

        //부모가 먼저 만들어진 순서와 무관하게 실제 형제 순서로 표시
        foreach ( TargetNode node in nodes.Values )
            node.Children.Sort( ( left, right ) =>
                left.Target.transform.GetSiblingIndex( ).CompareTo(
                    right.Target.transform.GetSiblingIndex( ) ) );

        _collapsed.RemoveWhere( target => target == null || nodes.ContainsKey( target ) == false );
    }

    /// <summary>
    /// 대상 목록 다시 조회
    /// </summary>
    void RefreshTargets ()
    {
        UiEffectSetupUtility.StopPreview( );
        ClearEditor( );
        _selected.Clear( );
        UiEffectSetupUtility.CollectTargets( _targets );
        BuildHierarchy( );
        Repaint( );
    }

    /// <summary>
    /// 선택한 같은 종류의 컴포넌트로 편집기 생성
    /// </summary>
    void RefreshEditor ()
    {
        ClearEditor( );

        List<UnityEngine.Object> effects = new( );		//혼합 편집 대상
        System.Type type = UiEffectSetupUtility.GetEffectType( _kind );		//편집 타입

        foreach ( GameObject target in _selected )
        {
            if ( target == null )
                continue;

            foreach ( Component effect in target.GetComponents( type ) )
                effects.Add( effect );
        }

        if ( effects.Count > 0 )
            _settingsEditor = Editor.CreateEditor( effects.ToArray( ) );
    }


    /// <summary>
    /// 선택 대상 컴포넌트 일괄 추가 또는 삭제
    /// </summary>
    /// <param name="isAdding">추가 여부</param>
    void ApplyComponents ( bool isAdding )
    {
        //편집 전에 임시 연출값을 복구
        UiEffectSetupUtility.StopPreview( );

        //여러 대상의 변경을 한 번의 Undo로 묶음
        Undo.IncrementCurrentGroup( );

        int group = Undo.GetCurrentGroup( );        //이번 편집 그룹

        Undo.SetCurrentGroupName(
            isAdding == true ? "UI 연출 추가" : "UI 연출 삭제" );

        try
        {
            foreach ( GameObject target in _selected )
            {
                //조회 후 삭제된 선택 대상만 제외
                if ( target == null )
                    continue;

                if ( isAdding == true )
                    UiEffectSetupUtility.AddEffect( target, _kind );
                else
                    UiEffectSetupUtility.RemoveEffect( target, _kind );
            }
        }
        finally
        {
            //중간 실패에서도 반영된 변경의 Undo 그룹과 편집기를 정리
            Undo.CollapseUndoOperations( group );
            RefreshEditor( );
        }
    }


    /// <summary>
    /// 검색에 맞는 대상과 부모 경로의 표시 상태 갱신
    /// </summary>
    /// <param name="node">검사할 계층 노드</param>
    /// <param name="type">조회할 연출 타입</param>
    /// <returns>자식을 포함해 표시할지 여부</returns>
    bool RefreshFilter ( TargetNode node, System.Type type )
    {
        if ( node.Target == null )
        {
            node.IsVisible = false;
            return false;
        }

        bool nameMatches = string.IsNullOrEmpty( _search ) == true ||
            node.Target.name.IndexOf( _search,
                System.StringComparison.OrdinalIgnoreCase ) >= 0;		//자신의 이름만 검색

        node.EffectCount = node.IsTarget == true
            ? node.Target.GetComponents( type ).Length : 0;
        node.IsMatch = MatchesFilter( node, nameMatches );
        node.IsVisible = node.IsMatch;

        //일치하는 자식이 있으면 자신의 필터와 무관하게 경로를 남김
        foreach ( TargetNode child in node.Children )
        {
            if ( RefreshFilter( child, type ) == true )
                node.IsVisible = true;
        }

        return node.IsVisible;
    }

    /// <summary>
    /// 검색 결과를 구분할 씬과 부모 경로 조회
    /// </summary>
    /// <param name="target">검색 결과 오브젝트</param>
    /// <returns>씬 이름을 포함한 부모 경로</returns>
    string GetParentPath ( GameObject target )
    {
        Stack<string> names = new( );		//상위 부모부터 표시할 경로 이름

        for ( Transform parent = target.transform.parent; parent != null; parent = parent.parent )
            names.Push( parent.name );

        names.Push( target.scene.name );
        return string.Join( " / ", names );
    }

    /// <summary>
    /// 계층 노드의 접기, 선택과 부착 상태 표시
    /// </summary>
    /// <param name="node">표시할 계층 노드</param>
    /// <param name="depth">들여쓰기 깊이</param>
    void DrawTargetNode ( TargetNode node, int depth )
    {
        if ( node.IsVisible == false )
            return;

        GameObject target = node.Target;		//현재 행의 오브젝트
        bool isSearching = string.IsNullOrEmpty( _search ) == false;		//검색 결과 평면 표시
        bool hasChildren = isSearching == false &&
            node.Children.Exists( child => child.IsVisible == true );		//표시할 하위 계층
        bool isExpanded = _collapsed.Contains( target ) == false;		//현재 펼침 상태

        EditorGUILayout.BeginHorizontal( );
        GUILayout.Space( depth * 16f );

        if ( hasChildren == true )
        {
            //고정 너비 영역을 확보하고 해당 영역에 접기 화살표를 표시
            Rect foldoutRect = GUILayoutUtility.GetRect(
                16f, EditorGUIUtility.singleLineHeight, GUILayout.Width( 16f ) );		//화살표 표시 영역

            bool expanded = EditorGUI.Foldout(
                foldoutRect, isExpanded, GUIContent.none, false );		//사용자 펼침 요청

            if ( expanded != isExpanded )
            {
                if ( expanded == true )
                    _collapsed.Remove( target );
                else
                    _collapsed.Add( target );

                isExpanded = expanded;
            }
        }
        else
            GUILayout.Space( 16f );

        //필터에 남은 편집 대상만 체크 가능하고 부모 경로는 탐색용으로 표시
        if ( node.IsMatch == true )
        {
            bool wasSelected = _selected.Contains( target );		//이전 선택 여부
            bool isSelected = EditorGUILayout.Toggle(
                wasSelected, GUILayout.Width( 20f ) );		//현재 선택 여부

            if ( wasSelected != isSelected )
            {
                UiEffectSetupUtility.StopPreview( );

                if ( isSelected == true )
                    _selected.Add( target );
                else
                    _selected.Remove( target );

                RefreshEditor( );
            }
        }
        else
            GUILayout.Space( 20f );

        EditorGUILayout.ObjectField( target, typeof( GameObject ), true );

        string status = node.IsMatch == false ? "부모" : node.EffectCount == 0
            ? "없음" : node.EffectCount == 1 ? "부착" : $"중복 {node.EffectCount}";		//부착 상태 또는 경로 역할

        GUILayout.Label( status, GUILayout.Width( 55f ) );

        if ( GUILayout.Button( "선택", GUILayout.Width( 45f ) ) == true )
        {
            Selection.activeGameObject = target;
            EditorGUIUtility.PingObject( target );
        }

        EditorGUILayout.EndHorizontal( );

        if ( isSearching == true )
        {
            //같은 이름의 결과도 구분할 수 있도록 부모 경로만 보조 문구로 표시
            EditorGUILayout.LabelField( GetParentPath( target ), EditorStyles.miniLabel );
            return;
        }

        if ( isExpanded == true )
        {
            foreach ( TargetNode child in node.Children )
                DrawTargetNode( child, depth + 1 );
        }
    }

    /// <summary>
    /// 자신의 이름이 일치하는 UI만 평면 검색 결과로 표시
    /// </summary>
    /// <param name="node">검색할 계층 노드</param>
    void DrawSearchResults ( TargetNode node )
    {
        if ( node.IsVisible == false )
            return;

        if ( node.IsMatch == true )
            DrawTargetNode( node, 0 );

        //부모의 일치 여부나 접힘 상태와 무관하게 자식도 개별 검사
        foreach ( TargetNode child in node.Children )
            DrawSearchResults( child );
    }

    /// <summary>
    /// 필터에 일치하는 하위 대상 선택
    /// </summary>
    /// <param name="node">선택할 계층 노드</param>
    void SelectMatchingTargets ( TargetNode node )
    {
        if ( node.Target == null || node.IsVisible == false )
            return;

        if ( node.IsMatch == true )
            _selected.Add( node.Target );

        foreach ( TargetNode child in node.Children )
            SelectMatchingTargets( child );
    }

    /// <summary>
    /// 대상 계층 또는 검색 결과와 선택 개수 표시
    /// </summary>
    void DrawTargets ()
    {
        System.Type type = UiEffectSetupUtility.GetEffectType( _kind );		//조회 타입

        _selected.RemoveWhere( target => target == null );

        //선택 버튼도 이번 화면의 최신 필터 결과를 사용
        foreach ( TargetNode root in _roots )
            RefreshFilter( root, type );

        EditorGUILayout.BeginHorizontal( );
        GUILayout.Label( $"선택 {_selected.Count}개 (숨겨진 대상 포함)" );

        if ( GUILayout.Button( "전체 선택", GUILayout.Width( 80f ) ) == true )
        {
            UiEffectSetupUtility.StopPreview( );
            _selected.Clear( );

            foreach ( TargetNode root in _roots )
                SelectMatchingTargets( root );

            RefreshEditor( );
        }

        if ( GUILayout.Button( "선택 해제", GUILayout.Width( 80f ) ) == true )
        {
            UiEffectSetupUtility.StopPreview( );
            _selected.Clear( );
            RefreshEditor( );
        }

        EditorGUILayout.EndHorizontal( );

        _scroll = EditorGUILayout.BeginScrollView(
            _scroll, GUILayout.MinHeight( 160f ), GUILayout.MaxHeight( 350f ) );

        foreach ( TargetNode root in _roots )
        {
            if ( string.IsNullOrEmpty( _search ) == false )
                DrawSearchResults( root );
            else
                DrawTargetNode( root, 0 );
        }

        EditorGUILayout.EndScrollView( );
    }


    /// <summary>
    /// 이름과 부착 상태의 필터 일치 판정
    /// </summary>
    /// <param name="node">검사할 계층 노드</param>
    /// <param name="nameMatches">자신의 이름 검색 일치 여부</param>
    /// <returns>개별 편집 대상으로 표시할지 여부</returns>
    bool MatchesFilter ( TargetNode node, bool nameMatches )
    {
        return node.IsTarget == true && nameMatches == true &&
            ( _onlyMissing == false || node.EffectCount == 0 );
    }

}
