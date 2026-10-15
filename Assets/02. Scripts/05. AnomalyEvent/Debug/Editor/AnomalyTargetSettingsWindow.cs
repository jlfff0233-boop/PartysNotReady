using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 변칙 대상 설정 창 - 여러 대상의 설정 비교, 개별 편집과 선택 항목 일괄 수정
/// </summary>
public class AnomalyTargetSettingsWindow : EditorWindow
{
    readonly List<AnomalyTarget> _targets = new( );     //현재 조회 범위의 대상
    /// <summary>
    /// 검색 폴더의 일반 프리팹 원본 목록
    /// </summary>
    readonly List<AnomalyPrefabEntry> _prefabs = new( );
    readonly List<Type> _types = new( );     //대상에 연결된 컴포넌트 형식
    readonly List<string> _paths = new( );     //선택 가능한 직렬화 항목 경로
    readonly List<string> _labels = new( );     //직렬화 항목의 화면 이름
    readonly HashSet<int> _selected = new( );     //검색과 새로고침 이후에도 선택 유지
    readonly HashSet<string> _editedAssets = new( );     //이 창에서 편집한 원본만 명시적으로 저장
    int _source;     //조회 범위, 0은 씬 / 1은 프리팹
    int _tab;     //사용 방법을 열어도 마지막 조회 범위는 유지
    int _prefabFilter;		//전체, 설정됨, 미설정 원본 필터
    string _prefabPath;		//오른쪽에서 설정할 원본 경로
    int _prefabPage;		//일괄 구성과 기존 설정 편집 화면
    Vector2 _prefabListScroll;		//왼쪽 원본 목록 스크롤
    Vector2 _prefabSettingsScroll;		//오른쪽 원본 설정 스크롤
    string _typeName;     //선택한 컴포넌트의 전체 형식 이름
    string _propertyPath;     //비교 / 편집할 항목 경로
    string _search = string.Empty;     //대상 이름과 경로 검색어
    DefaultAsset _folder;     //프리팹 검색 폴더
    bool _missingOnly;     //컴포넌트가 없거나 중복인 대상만 조회
    bool _refreshRequested = true;     //다음 Layout에서 목록 갱신
    Vector2 _scroll;     //대상 목록 스크롤
    Vector2 _helpScroll;     //사용 방법 스크롤
    bool _editHelp = true;     //편집 안내 펼침 여부
    bool _saveHelp;     //저장 안내 펼침 여부
    bool _playHelp;     //플레이 중 편집 안내 펼침 여부
    bool _checkHelp;     //조회 문제 안내 펼침 여부

    AnomalyComponentSetupPanel _setup = new( );		//기능 구성 화면
    AnomalyPrefabSetupPanel _prefabSetup;		//원본 다중 선택과 일괄 구성 화면


    private void OnEnable ()
    {
        //레이어 조회와 원본 폴더 연결은 창 활성화 이후 실행
        _prefabSetup = new AnomalyPrefabSetupPanel( );
        _prefabSetup.Init( );
        //대상 구성과 설정이 바뀌면 다음 화면 갱신에서 다시 수집
        EditorApplication.hierarchyChanged += RequestRefresh;
        EditorApplication.projectChanged += RequestRefresh;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        Undo.undoRedoPerformed += RequestRefresh;
        Selection.selectionChanged += ClearSetup;
        _refreshRequested = true;
    }

    private void OnDisable ()
    {
        //창을 닫은 뒤에도 갱신 요청이 남지 않도록 모든 구독 해제
        EditorApplication.hierarchyChanged -= RequestRefresh;
        EditorApplication.projectChanged -= RequestRefresh;
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        Undo.undoRedoPerformed -= RequestRefresh;
        Selection.selectionChanged -= ClearSetup;
    }

    private void OnGUI ()
    {
        if ( AnomalyPrefabBackupUtility.IsRestoring == true )
        {
            EditorGUILayout.HelpBox( "원본 복구 중입니다. 가져오기 완료까지 설정 편집을 중지합니다.", MessageType.Info );
            foreach ( string result in AnomalyPrefabBackupUtility.Results )
                EditorGUILayout.LabelField( result, EditorStyles.wordWrappedLabel );
            Repaint( );
            return;
        }
        DrawTabs( );
        if ( _tab == 3 )
        {
            _scroll = EditorGUILayout.BeginScrollView( _scroll );
            _setup.Draw( );
            EditorGUILayout.EndScrollView( );
            return;
        }
        //설명 화면에서는 갱신 요청만 보관하고 대상 검색은 돌아온 뒤 처리
        if ( _tab == 2 )
        {
            DrawHelp( );
            return;
        }

        //GUI 배치와 그리기가 서로 다른 목록을 사용하지 않도록 Layout에서만 갱신
        if ( _refreshRequested && Event.current.type == EventType.Layout )
            RefreshTargets( );

        if ( _source == 1 )
        {
            DrawPrefabWorkspace( );
            return;
        }

        DrawToolbar( );
        if ( _types.Count > 0 )
            DrawPropertySelection( );
        EditorGUILayout.HelpBox( Application.isPlaying
            ? "실행 중 생성된 대상도 조회합니다. 씬 인스턴스의 대체 가중치만 다음 발생 요청부터 임시 적용하며 원본 저장은 막습니다. 나머지 설정은 초기화 때 복사될 수 있어 읽기 전용입니다."
            : "실제 컴포넌트의 직렬화 설정을 편집합니다. 씬 변경은 씬에, 프리팹 변경은 선택한 원본에 적용됩니다. 참조한 SO의 내용이나 프리팹 원본에 자동 전파하지 않습니다.", MessageType.Info );

        //내용 길이가 창의 최소 크기를 늘리지 않도록 스크롤 영역 축소 허용
        _scroll = EditorGUILayout.BeginScrollView( _scroll, GUILayout.MinWidth( 0f ), GUILayout.MinHeight( 0f ) );
        if ( _types.Count == 0 )
            EditorGUILayout.HelpBox( _source == 1
                ? "구성된 설정 대상이 없습니다. 원본 목록에서 프리팹들을 체크하고 기능을 일괄 구성하세요."
                : "열린 씬에 AnomalyTarget이 없습니다.", MessageType.Info );
        else
        {
            EditorGUILayout.LabelField( "구성된 대상의 설정 편집", EditorStyles.boldLabel );
            DrawTargets( );
            EditorGUILayout.Space( );
            DrawSelectedSettings( );
        }
        EditorGUILayout.EndScrollView( );
    }


    #region ----- 조회와 선택 -----

    /// <summary>
    /// 변칙 대상 통합 설정 창 열기
    /// </summary>
    [MenuItem( "Tools/Anomaly/대상 설정 관리" )]
    public static void Open ()
    {
        AnomalyTargetSettingsWindow window = GetWindow<AnomalyTargetSettingsWindow>( "변칙 대상 설정" );
        window.minSize = new Vector2( 620f, 400f );
    }

    /// <summary>
    /// 다음 레이아웃에서 대상 목록 갱신 요청
    /// </summary>
    void RequestRefresh ()
    {
        _refreshRequested = true;
        Repaint( );
    }

    /// <summary>
    /// 선택 변경 후 이전 구성 미리보기 해제
    /// </summary>
    void ClearSetup ()
    {
        _setup.Clear( );
        Repaint( );
    }

    /// <summary>
    /// 플레이 전환 시 이전 인스턴스 선택 해제
    /// </summary>
    /// <param name="state">변경된 플레이 상태</param>
    void HandlePlayModeChanged ( PlayModeStateChange state )
    {
        _selected.Clear( );
        _setup.Clear( );
        _prefabSetup.ClearSelection( );
        RequestRefresh( );
    }

    /// <summary>
    /// 현재 선택한 형식의 단일 컴포넌트 조회
    /// </summary>
    /// <param name="target">조회할 변칙 대상</param>
    Component GetComponent ( AnomalyTarget target )
    {
        Type type = _types.Find( value => value.FullName == _typeName );
        if ( target == null || type == null )
            return null;
        Component[] components = target.GetComponents( type );
        //같은 형식이 여러 개면 임의의 첫 컴포넌트를 수정하지 않음
        return components.Length == 1 ? components[0] : null;
    }

    /// <summary>
    /// 씬 또는 프리팹 대상과 편집 가능한 형식 갱신
    /// </summary>
    void RefreshTargets ()
    {
        _refreshRequested = false;
        if ( _source == 0 )
        {
            _prefabs.Clear( );
            AnomalyTargetSettingsUtility.CollectSceneTargets( _targets );
        }
        else
        {
            string folder = _folder != null ? AssetDatabase.GetAssetPath( _folder ) : "Assets/04. Prefabs";
            AnomalyTargetSettingsUtility.CollectPrefabTargets( folder, _prefabs, _targets );
            _prefabSetup.RetainSelection( _prefabs );
            if ( _prefabs.Exists( entry => entry.Path == _prefabPath ) == false )
                _prefabPath = null;
        }

        _targets.Sort( ( a, b ) => string.Compare(
            AnomalyTargetSettingsUtility.GetTargetPath( a ),
            AnomalyTargetSettingsUtility.GetTargetPath( b ), StringComparison.OrdinalIgnoreCase ) );
        //검색으로 가려진 선택은 유지하고 조회 범위에서 사라진 대상만 해제
        _selected.RemoveWhere( id => _targets.Exists( value => value != null && value.GetInstanceID( ) == id ) == false );
        AnomalyTargetSettingsUtility.CollectTypes( _targets, _types );
        if ( _types.Exists( value => value.FullName == _typeName ) == false )
        {
            Type preferred = _types.Find( value => value == typeof( ReplaceController ) );
            _typeName = preferred != null ? preferred.FullName : _types.Count > 0 ? _types[0].FullName : null;
        }
        RefreshProperties( );
    }

    /// <summary>
    /// 선택한 컴포넌트의 표시 항목 목록 갱신
    /// </summary>
    void RefreshProperties ()
    {
        Component sample = null;
        foreach ( AnomalyTarget target in _targets )
        {
            sample = GetComponent( target );
            if ( sample != null )
                break;
        }
        AnomalyTargetSettingsUtility.CollectProperties( sample, _paths, _labels );
        if ( _paths.Contains( _propertyPath ) == false )
            _propertyPath = _paths.Count > 0 ? _paths[0] : null;
    }

    /// <summary>
    /// 현재 검색과 누락 필터에 맞는 대상 전체 선택
    /// </summary>
    void SelectVisibleTargets ()
    {
        _selected.Clear( );
        foreach ( AnomalyTarget target in _targets )
        {
            if ( target == null )
                continue;
            if ( IsVisibleTarget( target ) == false )
                continue;
            _selected.Add( target.GetInstanceID( ) );
        }
    }

    #endregion

    #region ----- 설정 적용 -----

    /// <summary>
    /// 직렬화 변경 적용과 씬 또는 프리팹 수정 상태 기록
    /// </summary>
    /// <param name="serialized">사용자가 편집한 직렬화 객체</param>
    void ApplyChanges ( SerializedObject serialized )
    {
        Undo.SetCurrentGroupName( "변칙 대상 설정 변경" );
        //실행 중 임시 편집은 적용만 하고 씬 / 원본 저장 대상으로 기록하지 않음
        if ( serialized.ApplyModifiedProperties( ) == false || Application.isPlaying )
            return;

        foreach ( Object value in serialized.targetObjects )
        {
            Component component = value as Component;
            if ( component == null )
                continue;
            if ( EditorUtility.IsPersistent( component ) )
            {
                string path = AssetDatabase.GetAssetPath( component );
                _editedAssets.Add( path );
                EditorUtility.SetDirty( AssetDatabase.LoadMainAssetAtPath( path ) );
            }
            else
            {
                //씬의 프리팹 인스턴스는 원본 대신 재정의와 씬 변경으로 기록
                if ( PrefabUtility.IsPartOfPrefabInstance( component ) )
                    PrefabUtility.RecordPrefabInstancePropertyModifications( component );
                EditorSceneManager.MarkSceneDirty( component.gameObject.scene );
            }
        }
    }

    /// <summary>
    /// 이 창에서 편집한 프리팹 원본 저장
    /// </summary>
    void SaveEditedPrefabs ()
    {
        if ( EditorApplication.isPlayingOrWillChangePlaymode )
            return;
        foreach ( string path in _editedAssets )
        {
            //항목 하나가 아닌 에셋을 저장하므로 같은 에셋의 다른 미저장 변경도 포함
            Object asset = AssetDatabase.LoadMainAssetAtPath( path );
            if ( asset != null )
                AssetDatabase.SaveAssetIfDirty( asset );
        }
        _editedAssets.Clear( );
    }

    #endregion

    #region ----- 목록 화면 -----


    /// <summary>
    /// 원본 목록에서 프리팹 편집 모드와 기능 구성 화면으로 이동
    /// </summary>
    /// <param name="entry">열 원본 항목</param>
    void OpenPrefab ( AnomalyPrefabEntry entry )
    {
        if ( _setup.OpenPrefab( entry.Path ) == false )
            return;
        _tab = 3;
        RequestRefresh( );
        GUIUtility.ExitGUI( );
    }

    /// <summary>
    /// 왼쪽 원본 목록과 오른쪽 설정 화면 표시
    /// </summary>
    void DrawPrefabWorkspace ()
    {
        using ( new EditorGUILayout.HorizontalScope( ) )
        {
            using ( new EditorGUILayout.VerticalScope( GUILayout.Width( Mathf.Clamp( position.width * 0.35f, 200f, 340f ) ) ) )
            {
                _prefabListScroll = EditorGUILayout.BeginScrollView( _prefabListScroll, GUILayout.MinWidth( 0f ) );
                DrawSearchControls( );
                DrawPrefabs( );
                EditorGUILayout.EndScrollView( );
            }
            using ( new EditorGUILayout.VerticalScope( GUILayout.MinWidth( 0f ) ) )
            {
                int page = GUILayout.Toolbar( _prefabPage, new[] { "일괄 구성", "기존 설정 편집" } );
                if ( page != _prefabPage )
                {
                    _prefabPage = page;
                    GUIUtility.ExitGUI( );
                }
                _prefabSettingsScroll = EditorGUILayout.BeginScrollView( _prefabSettingsScroll, GUILayout.MinWidth( 0f ) );
                if ( _prefabPage == 0 )
                {
                    if ( _prefabSetup.DrawControls( _prefabPath ) == true )
                    {
                        RequestRefresh( );
                        GUIUtility.ExitGUI( );
                    }
                    _prefabSetup.DrawResults( );
                }
                else
                {
                    DrawEditControls( );
                    if ( _types.Count > 0 )
                    {
                        DrawPropertySelection( );
                        DrawTargets( );
                        DrawSelectedSettings( );
                    }
                    else
                        EditorGUILayout.HelpBox( "설정 대상이 없습니다. 일괄 구성 화면에서 기능을 추가하세요.", MessageType.Info );
                    if ( string.IsNullOrEmpty( _prefabPath ) )
                        EditorGUILayout.HelpBox( "왼쪽 목록에서 설정할 원본 이름을 누르세요.", MessageType.Info );
                }
                EditorGUILayout.EndScrollView( );
            }
        }
    }

    /// <summary>
    /// 미설정 원본을 포함한 왼쪽 목록과 일괄 적용 대상 체크 표시
    /// </summary>
    void DrawPrefabs ()
    {
        EditorGUILayout.LabelField( "프리팹 원본 목록", EditorStyles.boldLabel );
        List<AnomalyPrefabEntry> visible = _prefabs.FindAll( entry =>
            entry.Root != null && IsVisiblePrefab( entry ) == true );
        _prefabSetup.DrawSelectionControls( visible );
        foreach ( AnomalyPrefabEntry entry in visible )
        {
            using ( new EditorGUILayout.VerticalScope( EditorStyles.helpBox ) )
            {
                using ( new EditorGUILayout.HorizontalScope( ) )
                {
                    bool selected = EditorGUILayout.Toggle( _prefabSetup.IsSelected( entry.Path ), GUILayout.Width( 20f ) );
                    _prefabSetup.SetSelected( entry.Path, selected );
                    if ( GUILayout.Toggle( _prefabPath == entry.Path, entry.Root.name, "Button" ) == true && _prefabPath != entry.Path )
                    {
                        _prefabPath = entry.Path;
                        GUIUtility.ExitGUI( );
                    }
                }
                EditorGUILayout.LabelField( entry.Path, EditorStyles.wordWrappedMiniLabel );
                EditorGUILayout.LabelField( entry.Targets.Length > 0
                    ? $"설정됨 / 대상 {entry.Targets.Length}개" : "미설정" );
                EditorGUILayout.LabelField( "구성된 기능: " + entry.Features, EditorStyles.wordWrappedLabel );
                foreach ( AnomalyTarget target in entry.Targets )
                {
                    if ( target != null )
                        EditorGUILayout.LabelField( AnomalyTargetSettingsUtility.GetTargetPath( target ) +
                            " — " + AnomalyTargetSettingsUtility.GetFeatures( target.gameObject ), EditorStyles.wordWrappedMiniLabel );
                }
                using ( new EditorGUILayout.HorizontalScope( ) )
                {
                    if ( GUILayout.Button( "원본 찾기" ) )
                        EditorGUIUtility.PingObject( entry.Root );
                    using ( new EditorGUI.DisabledScope( EditorApplication.isPlayingOrWillChangePlaymode ) )
                    {
                        if ( GUILayout.Button( "개별 편집 모드 열기" ) )
                            OpenPrefab( entry );
                    }
                }
            }
        }
        EditorGUILayout.LabelField( $"표시 {visible.Count} / 원본 {_prefabs.Count}개" );
    }

    /// <summary>
    /// 조회 범위와 사용 방법 탭 표시
    /// </summary>
    void DrawTabs ()
    {
        int tab = GUILayout.Toolbar( _tab, new[] { "열린 씬 / 실행 중 대상", "프리팹 원본", "사용 방법", "기능 구성" } );
        if ( tab == _tab )
            return;

        _tab = tab;
        //프리팹 편집 모드에서 돌아오면 저장된 원본을 다시 조회
        if ( _tab == 1 )
            RequestRefresh( );
        //설명 탭 왕복은 선택을 유지하고 실제 조회 범위를 바꿀 때만 해제
        if ( _tab < 2 && _tab != _source )
        {
            _source = _tab;
            _selected.Clear( );
            _prefabSetup.ClearSelection( );
            RequestRefresh( );
        }
        Repaint( );
        GUIUtility.ExitGUI( );
    }

    /// <summary>
    /// 대상 검색과 선택, 원본 저장 도구 표시
    /// </summary>
    void DrawToolbar ()
    {
        DrawSearchControls( );
        DrawEditControls( );
    }

    /// <summary>
    /// 원본 폴더와 이름 검색 조건 표시
    /// </summary>
    void DrawSearchControls ()
    {
        if ( _source == 1 )
        {
            EditorGUI.BeginChangeCheck( );
            _folder = (DefaultAsset)EditorGUILayout.ObjectField( "검색 폴더", _folder, typeof( DefaultAsset ), false );
            if ( EditorGUI.EndChangeCheck( ) )
                RequestRefresh( );
            if ( _folder == null )
                EditorGUILayout.LabelField( "기본 폴더: Assets/04. Prefabs" );
            _prefabFilter = EditorGUILayout.Popup( "원본 구성 상태", _prefabFilter,
                new[] { "전체", "설정됨", "미설정" } );
        }
        _search = EditorGUILayout.TextField( "이름 / 경로 검색", _search );
        if ( _source == 1 && GUILayout.Button( "목록 새로고침" ) )
            RequestRefresh( );
    }

    /// <summary>
    /// 설정 대상 선택과 원본 저장 도구 표시
    /// </summary>
    void DrawEditControls ()
    {
        _missingOnly = EditorGUILayout.Toggle( "설정 대상 중 선택 컴포넌트 없음 / 중복만", _missingOnly );
        using ( new EditorGUILayout.HorizontalScope( ) )
        {
            if ( GUILayout.Button( "새로고침" ) )
                RequestRefresh( );
            if ( GUILayout.Button( "표시된 설정 대상 전체 선택" ) )
                SelectVisibleTargets( );
            if ( GUILayout.Button( "선택 해제" ) )
                _selected.Clear( );
            using ( new EditorGUI.DisabledScope( EditorApplication.isPlayingOrWillChangePlaymode || _editedAssets.Count == 0 ) )
                if ( GUILayout.Button( $"편집한 프리팹 저장 ({_editedAssets.Count})" ) )
                    SaveEditedPrefabs( );
        }
    }

    /// <summary>
    /// 비교할 컴포넌트와 목록 표시 항목 선택
    /// </summary>
    void DrawPropertySelection ()
    {
        string[] names = _types.ConvertAll( value => value.Name ).ToArray( );
        int index = _types.FindIndex( value => value.FullName == _typeName );
        int next = EditorGUILayout.Popup( "설정 컴포넌트", Mathf.Max( 0, index ), names );
        if ( next != index )
        {
            _typeName = _types[next].FullName;
            RefreshProperties( );
        }
        if ( _paths.Count > 0 )
        {
            int propertyIndex = Mathf.Max( 0, _paths.IndexOf( _propertyPath ) );
            propertyIndex = EditorGUILayout.Popup( "비교 / 일괄 편집 항목", propertyIndex, _labels.ToArray( ) );
            _propertyPath = _paths[propertyIndex];
        }
    }

    /// <summary>
    /// 필터에 맞는 대상과 실제 설정값 표시
    /// </summary>
    void DrawTargets ()
    {
        int shown = 0;
        foreach ( AnomalyTarget target in _targets )
        {
            if ( target == null )
                continue;
            string path = AnomalyTargetSettingsUtility.GetTargetPath( target );
            Component component = GetComponent( target );
            if ( IsVisibleTarget( target ) == false )
                continue;
            shown++;
            using ( new EditorGUILayout.VerticalScope( EditorStyles.helpBox ) )
            {
                using ( new EditorGUILayout.HorizontalScope( ) )
                {
                    int id = target.GetInstanceID( );
                    bool selected = EditorGUILayout.Toggle( _selected.Contains( id ), GUILayout.Width( 20f ) );
                    if ( selected ) _selected.Add( id ); else _selected.Remove( id );
                    EditorGUILayout.LabelField( path, EditorStyles.wordWrappedLabel );
                    if ( GUILayout.Button( "찾기", GUILayout.Width( 45f ) ) )
                        EditorGUIUtility.PingObject( target );
                }
                if ( component == null )
                {
                    EditorGUILayout.LabelField( "컴포넌트 없음 또는 중복 / 자동 추가하지 않음" );
                    continue;
                }
                if ( string.IsNullOrEmpty( _propertyPath ) )
                {
                    EditorGUILayout.LabelField( "편집할 직렬화 설정 없음" );
                    continue;
                }
                using ( SerializedObject serialized = new( component ) )
                {
                    serialized.Update( );
                    SerializedProperty property = serialized.FindProperty( _propertyPath );
                    if ( property == null ) continue;
                    string origin = EditorUtility.IsPersistent( component ) ? "프리팹 원본" : "씬 인스턴스";
                    if ( PrefabUtility.IsPartOfPrefabInstance( component ) )
                        origin += property.prefabOverride ? " / 프리팹 재정의" : " / 프리팹 값";
                    EditorGUILayout.LabelField( "설정 출처: " + origin, EditorStyles.wordWrappedMiniLabel );
                    if ( AnomalyTargetSettingsUtility.IsSimple( property ) )
                    {
                        using ( new EditorGUI.DisabledScope( AnomalyTargetSettingsUtility.CanEdit( component, _propertyPath ) == false ) )
                        {
                            EditorGUILayout.PropertyField( property, true );
                            if ( AnomalyTargetSettingsUtility.CanEdit( component, _propertyPath ) )
                                ApplyChanges( serialized );
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField( property.displayName + ": " +
                            AnomalyTargetSettingsUtility.GetSummary( property ), EditorStyles.wordWrappedLabel );
                        EditorGUILayout.LabelField( "선택 후 아래에서 상세 편집", EditorStyles.wordWrappedMiniLabel );
                    }
                }
            }
        }
        EditorGUILayout.LabelField( $"표시 {shown} / 전체 {_targets.Count} / 선택 {_selected.Count}" );
    }


    /// <summary>
    /// 원본이 구성 상태와 이름 검색에 맞는지 조회
    /// </summary>
    /// <param name="entry">조회할 원본 항목</param>
    /// <returns>목록 표시 여부</returns>
    bool IsVisiblePrefab ( AnomalyPrefabEntry entry )
    {
        bool configured = entry.Targets.Length > 0;
        if ( ( _prefabFilter == 1 && configured == false ) ||
             ( _prefabFilter == 2 && configured == true ) )
            return false;
        if ( entry.Path.IndexOf( _search, StringComparison.OrdinalIgnoreCase ) >= 0 ||
             entry.Root.name.IndexOf( _search, StringComparison.OrdinalIgnoreCase ) >= 0 )
            return true;
        foreach ( AnomalyTarget target in entry.Targets )
        {
            if ( target != null && AnomalyTargetSettingsUtility.GetTargetPath( target )
                .IndexOf( _search, StringComparison.OrdinalIgnoreCase ) >= 0 )
                return true;
        }
        return false;
    }

    /// <summary>
    /// 설정 대상이 현재 검색과 구성 필터에 맞는지 조회
    /// </summary>
    /// <param name="target">조회할 설정 대상</param>
    /// <returns>목록 표시 여부</returns>
    bool IsVisibleTarget ( AnomalyTarget target )
    {
        if ( _missingOnly == true && GetComponent( target ) != null )
            return false;
        if ( _source == 1 )
        {
            //중첩 원본의 경로 대신 검색 원본에 수집된 실제 대상을 기준으로 연결
            AnomalyPrefabEntry entry = _prefabs.Find( value => Array.IndexOf( value.Targets, target ) >= 0 );
            if ( entry == null || entry.Path != _prefabPath || IsVisiblePrefab( entry ) == false )
                return false;
            if ( entry.Path.IndexOf( _search, StringComparison.OrdinalIgnoreCase ) >= 0 ||
                 entry.Root.name.IndexOf( _search, StringComparison.OrdinalIgnoreCase ) >= 0 )
                return true;
        }
        return AnomalyTargetSettingsUtility.GetTargetPath( target )
            .IndexOf( _search, StringComparison.OrdinalIgnoreCase ) >= 0;
    }

    #endregion

    #region ----- 사용 방법 -----

    /// <summary>
    /// 대상 설정 창의 편집 순서와 적용 범위 안내
    /// </summary>
    void DrawHelp ()
    {
        //목록과 설명의 스크롤을 분리해 탭을 오가도 읽던 위치를 유지
        _helpScroll = EditorGUILayout.BeginScrollView( _helpScroll );
        EditorGUILayout.LabelField( "대상 설정 관리 사용 방법", EditorStyles.boldLabel );
        EditorGUILayout.HelpBox(
            "1. 열린 씬 또는 프리팹 원본을 선택합니다.\n" +
            "2. 이름 / 경로로 대상을 찾습니다.\n" +
            "3. 설정 컴포넌트와 비교 / 일괄 편집 항목을 선택합니다.\n" +
            "4. 목록에서 값을 수정하거나, 대상을 체크한 뒤 아래 상세에서 편집합니다.\n" +
            "5. 씬 또는 편집한 프리팹을 저장합니다.", MessageType.Info );
        EditorGUILayout.LabelField( "프리팹 원본 목록에서 여러 물건을 체크하고 부착할 기능 묶음을 선택합니다. 일괄 구성 미리보기 후 적용하면 선택 원본들에 기능을 붙이고 저장합니다. 사건 발생과 해결 확인은 변칙 사건 디버그 인스펙터에서 진행합니다.", EditorStyles.wordWrappedLabel );
        EditorGUILayout.Space( );

        _editHelp = EditorGUILayout.Foldout( _editHelp, "개별 편집과 일괄 편집", true );
        if ( _editHelp )
        {
            EditorGUILayout.HelpBox(
                "단일 값은 각 대상의 목록 행에서 바로 수정합니다. 배열과 복합 설정은 대상을 체크하고 아래 '선택 대상의 항목 편집'에서 펼칩니다.\n\n" +
                "여러 대상을 체크하면 선택한 항목을 함께 편집합니다. 서로 다른 값은 혼합 값으로 표시되며, 수정한 값이 선택 대상에 적용됩니다. 컴포넌트가 없거나 중복인 대상은 편집에서 제외됩니다.\n\n" +
                "검색이나 필터로 가려진 선택 대상도 일괄 편집에 포함됩니다. '표시된 설정 대상 전체 선택'은 기존 선택을 현재 표시된 설정 대상으로 바꾸며, '선택 해제'는 모든 선택을 해제합니다.\n\n" +
                "배열 원소는 같은 인덱스끼리 수정됩니다. 대상마다 원소의 의미나 순서가 다르면 개별 편집하세요. 배열 크기를 바꾸면 선택 대상들의 배열 크기도 바뀝니다.", MessageType.Info );
        }

        _saveHelp = EditorGUILayout.Foldout( _saveHelp, "씬 / 프리팹 적용 범위와 저장", true );
        if ( _saveHelp )
        {
            EditorGUILayout.HelpBox(
                "열린 씬: 비활성 오브젝트를 포함한 씬 인스턴스를 편집합니다. 프리팹 인스턴스의 수정은 재정의로 기록되며, 원본에 자동 적용되지 않습니다. 씬은 Unity의 씬 저장 기능으로 저장하세요.\n\n" +
                "프리팹 원본: 검색 폴더와 하위 폴더의 일반 프리팹을 미설정 원본까지 조회합니다. 기본 폴더는 Assets/04. Prefabs이며 모델 프리팹은 제외됩니다. 원본 구성 상태로 전체 / 설정됨 / 미설정을 구분합니다. 내부 대상의 기능과 계층 경로도 표시하며 구성된 기능은 사용 준비 완료를 뜻하지 않습니다.\n\n" +
                "원본 목록에서 여러 프리팹과 부착할 기능, 공통 설정을 선택합니다. 미리보기 후 적용하면 기능은 원본 루트에 부착하고 공통 설정은 내부 변칙 대상에 적용합니다. '전체 외형 자식 구성'과 '빈자리 조사 영역 생성'은 단일 물건의 구조를 구성하고 관련 참조를 연결합니다. 계층 변경에 영향을 받는 외부 참조와 재정의, 특수 구조는 제외 사유를 표시합니다. 새 조사 영역의 레이어는 플레이어의 조준 마스크에도 포함해야 합니다. 적용 전 복구 자료를 보관합니다. '일괄 적용 복구'에서 작업과 원본을 선택하여 되돌릴 수 있으며, 추가 수정되거나 이동한 원본은 자동 복구에서 제외합니다. 폴더 이동은 이 복구에 포함하지 않습니다.\n\n" +
                "왼쪽은 검색과 원본 목록, 오른쪽은 설정 화면입니다. 체크는 일괄 적용 대상을 정하고 원본 이름 버튼은 오른쪽에서 설정할 원본을 정합니다. 오른쪽의 '일괄 구성'과 '기존 설정 편집'을 전환해도 선택과 미리보기는 유지됩니다. '원본별 참조 직접 지정'에서 대표 충돌체, 기존 조사 영역, 개폐 축, 색 변경 대상과 머티리얼 슬롯을 현재 원본 내부 후보에서 선택할 수 있습니다. 다른 원본으로 이동해도 지정값은 유지됩니다. 직접 지정은 원본 루트의 기능에 적용하고 자동 연결로 덮어쓰지 않습니다. 지정 변경 시 다시 미리보기해야 하며 원본 선택 해제와 적용 후에는 지정 항목도 해제합니다. 저장 가능 여부와 남은 참조 설정은 결과에서 별도로 확인하세요.\n\n" +
                "미저장 설정이 있거나 현재 편집 모드에서 열린 원본은 일괄 구성에서 제외합니다. 저장하고 편집 모드를 닫은 뒤 다시 미리보기하세요. 내부의 특정 자식에 구성하려면 '개별 편집 모드 열기'로 해당 원본을 열고 계층 창에서 자식을 선택하세요. 중첩 프리팹에 추가하면 열린 원본의 재정의이므로 공통 원본을 수정하려면 그 원본을 별도로 여세요.\n\n" +
                "'편집한 프리팹 저장'은 목록에서 설정값을 편집한 원본을 저장합니다. 기능 구성 탭에서 한 개별 변경은 프리팹 편집 모드에서 저장합니다. 같은 에셋에 다른 곳에서 수정한 미저장 내용이 있으면 함께 저장됩니다.\n\n" +
                "설정 출처에서 씬 인스턴스 / 프리팹 원본과 재정의 여부를 확인할 수 있습니다. SO 참조를 바꿔도 참조한 SO 내부 내용은 수정하지 않습니다. 값 편집은 Unity의 Undo / Redo로 되돌리거나 다시 적용할 수 있습니다.", MessageType.Info );
        }

        _playHelp = EditorGUILayout.Foldout( _playHelp, "플레이 중 조회와 임시 수정", true );
        if ( _playHelp )
        {
            EditorGUILayout.HelpBox(
                "실행 중 생성된 대상도 열린 씬 목록에 포함됩니다. 목록이 갱신되지 않았다면 '새로고침'을 누르세요.\n\n" +
                "플레이 중에는 씬 인스턴스의 ReplaceController 대체 유형 / 가중치 목록(_types)만 임시 수정할 수 있습니다. 다음 사건 발생 요청부터 읽으며, 이미 진행 중인 사건을 바꾸지는 않습니다. 플레이 종료 후 유지할 설정은 편집 모드에서 다시 설정하고 저장하세요.\n\n" +
                "나머지 설정은 초기화 때 복사될 수 있어 읽기 전용입니다. 플레이 중 프리팹 원본 수정과 저장은 비활성화됩니다. 플레이 모드를 전환하면 이전 대상 선택은 해제됩니다.", MessageType.Info );
        }

        _checkHelp = EditorGUILayout.Foldout( _checkHelp, "대상이나 설정이 보이지 않을 때", true );
        if ( _checkHelp )
        {
            EditorGUILayout.HelpBox(
                "원본 없음: 검색 폴더와 이름 검색, 원본 구성 상태 필터를 확인하세요. 원본은 AnomalyTarget이 없어도 표시됩니다. 설정 대상 없음: 미설정 원본들을 체크하고 기능을 일괄 구성하거나 열린 씬의 AnomalyTarget 부착 여부를 확인하세요. 누락 필터는 설정 대상 목록에만 적용됩니다.\n\n" +
                "컴포넌트 없음 또는 중복: 설정 컴포넌트는 AnomalyTarget과 같은 오브젝트에서 조회합니다. 해당 형식이 없거나 둘 이상이면 임의로 추가하거나 수정하지 않습니다. '찾기'로 위치를 확인하고 Inspector에서 구성하세요.\n\n" +
                "편집할 직렬화 설정 없음: 선택한 컴포넌트에 표시 가능한 직렬화 항목이 있는지 확인하세요.\n\n" +
                "사용 방법 탭을 오가도 검색과 선택은 유지됩니다. 씬과 프리팹 사이로 조회 범위를 바꾸면 선택이 해제됩니다.", MessageType.Info );
        }
        EditorGUILayout.EndScrollView( );
    }

    #endregion

    #region ----- 선택 항목 편집 -----

    /// <summary>
    /// 선택한 대상들의 지정 항목만 혼합 값 표시와 함께 편집
    /// </summary>
    void DrawSelectedSettings ()
    {
        EditorGUILayout.LabelField( "선택 대상의 항목 편집", EditorStyles.boldLabel );
        List<Object> components = new( );
        bool editable = true;
        foreach ( AnomalyTarget target in _targets )
        {
            if ( target == null || _selected.Contains( target.GetInstanceID( ) ) == false )
                continue;
            Component component = GetComponent( target );
            if ( component == null )
                continue;
            components.Add( component );
            editable &= AnomalyTargetSettingsUtility.CanEdit( component, _propertyPath );
        }
        if ( components.Count == 0 || string.IsNullOrEmpty( _propertyPath ) )
        {
            EditorGUILayout.LabelField( "목록에서 편집할 대상을 선택해 주세요." );
            return;
        }
        EditorGUILayout.HelpBox( $"선택 {_selected.Count}개 중 같은 컴포넌트가 있는 {components.Count}개에 이 항목만 적용합니다. 검색으로 가려진 선택 대상도 포함합니다. 서로 다른 값은 혼합 표시됩니다.", MessageType.Info );
        using ( SerializedObject serialized = new( components.ToArray( ) ) )
        {
            //선택한 컴포넌트들의 동일 경로만 편집하고 서로 다른 값은 혼합 값으로 처리
            serialized.Update( );
            SerializedProperty property = serialized.FindProperty( _propertyPath );
            if ( property == null ) return;
            if ( property.isArray && components.Count > 1 )
                EditorGUILayout.HelpBox( "목록 크기를 바꾸면 선택 대상들의 목록 크기도 바뀝니다. 각 원소 수정은 같은 인덱스에 적용됩니다.", MessageType.Warning );
            using ( new EditorGUI.DisabledScope( editable == false ) )
            {
                EditorGUILayout.PropertyField( property, new GUIContent( property.displayName ), true );
                if ( editable )
                    ApplyChanges( serialized );
            }
        }
    }

    #endregion

}
