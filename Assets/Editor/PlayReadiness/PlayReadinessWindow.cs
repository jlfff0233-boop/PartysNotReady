using System;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>
/// 씬과 연결 자산의 준비 상태를 읽기 전용으로 표시
/// </summary>
public class PlayReadinessWindow : EditorWindow
{
    Object _target;        //지정 씬 또는 선택 검사 대상
    DefaultAsset _folder;        //추가 검사할 자산 폴더
    bool _selectionOnly;        //선택 범위 한정 여부
    bool _stale = true;        //검사 이후 변경 여부
    bool _issuesOnly = true;        //정적 통과 숨김 여부
    string _search = string.Empty;        //대상과 필드 검색
    int _group;        //시스템 필터
    int _status;        //판정 필터
    int _agentIndex;        //NavMesh 에이전트 선택
    float _radius = 1f;        //NavMesh 표본 반경
    Vector2 _scroll;        //결과 목록 위치
    Vector2 _detailScroll;        //상세 설명 위치
    PlayReadinessResult _selected;        //선택 결과
    PlayReadinessChecker _checker = new( );        //분할 검사 담당
    double _nextRepaint;        //진행 표시 갱신 시각
    string[] _groups = { "전체 시스템", "씬", "데이터", "콘텐츠", "표현", "맵", "통합" };        //시스템 필터 이름
    string[] _statuses = { "전체 판정", "오류", "경고", "정적 통과", "플레이 확인 대기", "검사 불가" };        //판정 표시 이름

    /// <summary>
    /// 점검 진행과 변경 감지 이벤트 구독
    /// </summary>
    private void OnEnable ()
    {
        EditorApplication.update += Tick;
        EditorApplication.hierarchyChanged += MarkStale;
        EditorApplication.projectChanged += MarkStale;
        EditorApplication.playModeStateChanged += HandlePlayMode;
        Undo.undoRedoPerformed += MarkStale;
        ObjectChangeEvents.changesPublished += HandleObjectChanges;
        AssemblyReloadEvents.beforeAssemblyReload += MarkStale;
    }

    /// <summary>
    /// 이벤트 구독과 진행 검사 해제
    /// </summary>
    private void OnDisable ()
    {
        EditorApplication.update -= Tick;
        EditorApplication.hierarchyChanged -= MarkStale;
        EditorApplication.projectChanged -= MarkStale;
        EditorApplication.playModeStateChanged -= HandlePlayMode;
        Undo.undoRedoPerformed -= MarkStale;
        ObjectChangeEvents.changesPublished -= HandleObjectChanges;
        AssemblyReloadEvents.beforeAssemblyReload -= MarkStale;
        _checker.Cancel( );
    }

    /// <summary>
    /// 읽기 전용 점검 화면 표시
    /// </summary>
    private void OnGUI ()
    {
        DrawToolbar( );
        EditorGUILayout.HelpBox( "읽기 전용 점검입니다. Init·게임 시작·임포트·자동 할당·자산 저장은 실행하지 않습니다. 정적 통과와 실제 플레이 확인을 구분합니다.", MessageType.Info );
        if ( _stale == true )
            EditorGUILayout.HelpBox( "검사 대상 또는 설정이 변경되었습니다. 이전 결과를 사용하기 전에 새로고침해 주세요.", MessageType.Warning );
        DrawSummary( );
        DrawResults( );
        DrawDetail( );
    }


    /// <summary>
    /// 준비 점검 창 열기
    /// </summary>
    [MenuItem( "Tools/Play/플레이 준비 점검" )]
    public static void Open ()
    {
        GetWindow<PlayReadinessWindow>( "플레이 준비 점검" ).minSize = new Vector2( 780f, 580f );
    }

    /// <summary>
    /// 지정 플레이 씬을 검사할 창 열기
    /// </summary>
    /// <param name="scene">검사할 플레이 씬</param>
    public static void Open ( PlayScene scene )
    {
        Open( );
        PlayReadinessWindow window = GetWindow<PlayReadinessWindow>( );
        window._target = scene;
        window._selectionOnly = false;
        window.RequestRefresh( );
    }

    /// <summary>
    /// 현재 결과를 오래된 상태로 표시하고 진행 검사 취소
    /// </summary>
    void MarkStale ()
    {
        _stale = true;
        _checker.Cancel( );
        Repaint( );
    }

    /// <summary>
    /// Inspector 값 변경을 검사 상태에 반영
    /// </summary>
    /// <param name="stream">편집기 객체 변경 목록</param>
    void HandleObjectChanges ( ref ObjectChangeEventStream stream )
    {
        MarkStale( );
    }

    /// <summary>
    /// 플레이 전환 전 기존 결과 무효화
    /// </summary>
    /// <param name="state">플레이 전환 상태</param>
    void HandlePlayMode ( PlayModeStateChange state )
    {
        MarkStale( );
    }


    /// <summary>
    /// 검사 진행과 표시 갱신
    /// </summary>
    void Tick ()
    {
        if ( _checker.IsRunning == false )
            return;
        _checker.CheckNext( );
        if ( EditorApplication.timeSinceStartup >= _nextRepaint || _checker.IsRunning == false )
        {
            _nextRepaint = EditorApplication.timeSinceStartup + 0.1;
            Repaint( );
        }
    }

    /// <summary>
    /// 현재 선택 조건으로 새 점검 시작
    /// </summary>
    void RequestRefresh ()
    {
        if ( _selectionOnly == true && _target == null )
        {
            ShowNotification( new GUIContent( "선택 검사 대상을 지정해 주세요." ) );
            return;
        }
        string folder = _folder == null ? string.Empty : AssetDatabase.GetAssetPath( _folder );
        if ( folder.Length > 0 && ( folder.StartsWith( "Assets/", StringComparison.Ordinal ) == false && folder != "Assets" ||
             AssetDatabase.IsValidFolder( folder ) == false ) )
        {
            ShowNotification( new GUIContent( "Assets 내부 폴더를 지정해 주세요." ) );
            return;
        }
        int count = NavMesh.GetSettingsCount( );
        if ( PlayDataSetupChecker.Finite( _radius ) == false )
        {
            ShowNotification( new GUIContent( "표본 반경에는 유한한 양수를 입력해 주세요." ) );
            return;
        }
        _agentIndex = count == 0 ? 0 : Mathf.Clamp( _agentIndex, 0, count - 1 );
        int agentType = count == 0 ? 0 : NavMesh.GetSettingsByIndex( _agentIndex ).agentTypeID;
        _checker.Begin( _target, _selectionOnly, folder, _radius, agentType );
        _selected = null;
        _stale = false;
    }


    /// <summary>
    /// 검사 범위, 검색과 시스템 필터 표시
    /// </summary>
    void DrawToolbar ()
    {
        EditorGUI.BeginChangeCheck( );
        _target = EditorGUILayout.ObjectField( "기준 씬 / 선택 대상", _target, typeof( Object ), true );
        using ( new EditorGUILayout.HorizontalScope( ) )
        {
            _selectionOnly = EditorGUILayout.ToggleLeft( "선택 대상과 연결 자산만 검사", _selectionOnly, GUILayout.Width( 240f ) );
            if ( GUILayout.Button( "현재 선택 사용", GUILayout.Width( 120f ) ) )
            {
                _target = Selection.activeObject;
                _selectionOnly = true;
                MarkStale( );
            }
            _folder = (DefaultAsset)EditorGUILayout.ObjectField( "추가 폴더", _folder, typeof( DefaultAsset ), false );
        }
        using ( new EditorGUILayout.HorizontalScope( ) )
        {
            int count = NavMesh.GetSettingsCount( );
            string[] names = new string [ Math.Max( 1, count ) ];
            for ( int i = 0; i < count; i++ )
                names [ i ] = NavMesh.GetSettingsNameFromID( NavMesh.GetSettingsByIndex( i ).agentTypeID );
            if ( count == 0 )
                names [ 0 ] = "등록 에이전트 없음";
            _agentIndex = EditorGUILayout.Popup( "NavMesh 에이전트", Mathf.Clamp( _agentIndex, 0, names.Length - 1 ), names );
            _radius = Mathf.Max( 0.01f, EditorGUILayout.FloatField( "표본 반경", _radius, GUILayout.Width( 210f ) ) );
        }
        if ( EditorGUI.EndChangeCheck( ) == true )
            MarkStale( );
        using ( new EditorGUILayout.HorizontalScope( ) )
        {
            using ( new EditorGUI.DisabledScope( EditorApplication.isCompiling == true ) )
            {
                if ( GUILayout.Button( "새로고침", GUILayout.Width( 100f ) ) )
                    RequestRefresh( );
            }
            using ( new EditorGUI.DisabledScope( _checker.IsRunning == false ) )
            {
                if ( GUILayout.Button( "검사 취소", GUILayout.Width( 100f ) ) )
                    MarkStale( );
            }
            if ( GUILayout.Button( "플레이 디버그", GUILayout.Width( 120f ) ) )
                PlayDebugWindow.Open( _target as PlayScene );
            if ( GUILayout.Button( "결과 복사", GUILayout.Width( 100f ) ) )
                CopyResults( );
            EditorGUILayout.LabelField( _checker.Progress );
        }
        using ( new EditorGUILayout.HorizontalScope( EditorStyles.toolbar ) )
        {
            _group = EditorGUILayout.Popup( _group, _groups, GUILayout.Width( 120f ) );
            _status = EditorGUILayout.Popup( _status, _statuses, GUILayout.Width( 160f ) );
            _issuesOnly = GUILayout.Toggle( _issuesOnly, "정적 통과 숨김", EditorStyles.toolbarButton, GUILayout.Width( 110f ) );
            _search = GUILayout.TextField( _search, EditorStyles.toolbarTextField );
        }
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage( );
        EditorGUILayout.LabelField( "검사 범위", _selectionOnly == true ? "선택 대상 + 연결 자산" :
            stage != null ? "현재 Prefab Stage + 연결 자산" : "기준 PlayScene의 씬 또는 활성 씬 전체 + 연결 자산" );
    }

    /// <summary>
    /// 판정별 개수 표시
    /// </summary>
    void DrawSummary ()
    {
        if ( _checker.Context == null )
            return;
        int[] counts = new int [ 5 ];
        foreach ( PlayReadinessResult result in _checker.Context.Results )
            counts [ (int)result.Status ]++;
        EditorGUILayout.LabelField( $"오류 {counts[0]} · 경고 {counts[1]} · 정적 통과 {counts[2]} · 플레이 확인 대기 {counts[3]} · 검사 불가 {counts[4]}", EditorStyles.boldLabel );
    }


    /// <summary>
    /// 판정·대상·설명·이동의 네 열 표시
    /// </summary>
    void DrawResults ()
    {
        _scroll = EditorGUILayout.BeginScrollView( _scroll );
        if ( _checker.Context != null )
        {
            foreach ( PlayReadinessResult result in _checker.Context.Results )
            {
                if ( Matches( result ) == false )
                    continue;
                using ( new EditorGUILayout.HorizontalScope( EditorStyles.helpBox ) )
                {
                    GUILayout.Label( _statuses [ (int)result.Status + 1 ], GUILayout.Width( 110f ) );
                    string name = result.Owner == null ? "검사 전체" : $"{result.Owner.name} ({result.Owner.GetType( ).Name})";
                    if ( GUILayout.Button( new GUIContent( $"{name}\n{result.Field}", GetPath( result.Owner ) + "\n" + result.Field ),
                        EditorStyles.label, GUILayout.Width( 235f ), GUILayout.Height( 36f ) ) )
                        _selected = result;
                    if ( GUILayout.Button( new GUIContent( $"[{result.Group} / {result.Scope}] {result.Message}", result.Message ),
                        EditorStyles.wordWrappedLabel, GUILayout.MinHeight( 36f ) ) )
                        _selected = result;
                    using ( new EditorGUI.DisabledScope( result.Owner == null ) )
                    {
                        if ( GUILayout.Button( "선택", GUILayout.Width( 55f ) ) )
                        {
                            _selected = result;
                            SelectTarget( result.Owner );
                        }
                    }
                }
            }
        }
        EditorGUILayout.EndScrollView( );
    }

    /// <summary>
    /// 상세 경로와 연결 자산 이동 표시
    /// </summary>
    void DrawDetail ()
    {
        if ( _selected == null )
            return;
        _detailScroll = EditorGUILayout.BeginScrollView( _detailScroll, GUILayout.Height( 145f ) );
        EditorGUILayout.LabelField( $"{_selected.Scope} · {_selected.Group} · {_selected.Rule}", EditorStyles.boldLabel );
        EditorGUILayout.SelectableLabel( $"{GetPath( _selected.Owner )}\n{_selected.Field}\n{_selected.Message}",
            EditorStyles.wordWrappedLabel, GUILayout.MinHeight( 70f ) );
        EditorGUILayout.ObjectField( "설정 소유자", _selected.Owner, typeof( Object ), true );
        using ( new EditorGUI.DisabledScope( _selected.Related == null ) )
        {
            if ( GUILayout.Button( "연결 대상 선택 / 자산 열기" ) )
                SelectTarget( _selected.Related );
        }
        EditorGUILayout.EndScrollView( );
    }

    /// <summary>
    /// 소유 객체의 자산 또는 씬 계층 경로 조회
    /// </summary>
    /// <param name="target">조회 대상</param>
    /// <returns>객체 경로</returns>
    static string GetPath ( Object target )
    {
        if ( target == null )
            return "검사 전체";
        string path = AssetDatabase.GetAssetPath( target );
        if ( path.Length > 0 )
            return path;
        Transform transform = target is Component component ? component.transform :
            target is GameObject gameObject ? gameObject.transform : null;
        if ( transform == null )
            return target.name;
        path = transform.name;
        while ( transform.parent != null )
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return transform.gameObject.scene.path + ":" + path;
    }

    /// <summary>
    /// Inspector 선택과 자산 열기
    /// </summary>
    /// <param name="target">이동 대상</param>
    static void SelectTarget ( Object target )
    {
        if ( target == null )
            return;
        Selection.activeObject = target;
        EditorGUIUtility.PingObject( target );
        //프리팹은 사용자 선택 동작에서만 열고 검사 중에는 열지 않습니다.
        if ( EditorUtility.IsPersistent( target ) == true &&
             AssetDatabase.GetAssetPath( target ).EndsWith( ".prefab", StringComparison.OrdinalIgnoreCase ) == true )
            AssetDatabase.OpenAsset( target is Component component ? component.gameObject : target );
    }

    /// <summary>
    /// 필터된 점검 결과를 클립보드에 복사
    /// </summary>
    void CopyResults ()
    {
        if ( _checker.Context == null )
            return;
        StringBuilder text = new( );
        text.AppendLine( _stale == true ? "이전 결과: 새로고침 필요" : _checker.Progress );
        foreach ( PlayReadinessResult result in _checker.Context.Results )
        {
            if ( Matches( result ) == true )
                text.AppendLine( $"[{_statuses[(int)result.Status + 1]}][{result.Scope}][{result.Group}] {GetPath( result.Owner )} | {result.Field} | {result.Message}" );
        }
        EditorGUIUtility.systemCopyBuffer = text.ToString( );
    }

    /// <summary>
    /// 결과의 검색 조건 일치 확인
    /// </summary>
    /// <param name="result">검사 결과</param>
    /// <returns>표시 여부</returns>
    bool Matches ( PlayReadinessResult result )
    {
        if ( _group > 0 && result.Group != _groups [ _group ] ||
             _status > 0 && (int)result.Status != _status - 1 ||
             _issuesOnly == true && _status == 0 && result.Status == PlayReadinessStatus.Passed )
            return false;
        string value = $"{result.Owner} {result.Field} {result.Message} {result.Scope}";
        return string.IsNullOrWhiteSpace( _search ) == true || value.IndexOf( _search, StringComparison.OrdinalIgnoreCase ) >= 0;
    }

}
