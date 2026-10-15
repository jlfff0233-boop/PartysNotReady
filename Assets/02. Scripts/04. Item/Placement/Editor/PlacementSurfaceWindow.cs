using System.Collections.Generic;
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 배치 표면 설정 창 - 씬 후보 다중 선택과 설정 미리보기 및 일괄 적용
/// </summary>
public class PlacementSurfaceWindow : EditorWindow
{
    List<PlacementSurfaceEntry> _entries = new( );     //조회한 씬 후보
    readonly PlacementSurfaceOptions _options = new( );     //신규 구성과 수정 선택
    readonly PlacementSurfaceTree _tree = new( );     //원본 구조를 유지하는 후보 계층
    Vector2 _listScroll;     //왼쪽 후보 목록 스크롤
    Vector2 _settingsScroll;     //오른쪽 설정 스크롤
    string _search = string.Empty;     //객체 이름 또는 경로 검색
    bool _searchPath;     //전체 경로 검색, 기본은 객체 이름
    string _message = string.Empty;     //적용 결과 안내
    int _kind = 2;     //바닥, 벽과 일반 표면 구분
    int _source;     //열린 씬 전체 또는 하이어라키 선택 범위
    int _available;     //미리보기에서 적용 가능한 선택 수
    bool _hasPreview;     //현재 선택과 설정의 미리보기 완료
    bool _refreshRequested;     //다음 화면 배치 시 후보 조회
    AnomalyPrefabBackupBatch _backup;     //최근 원본 적용의 복구 기록

    /// <summary>
    /// 창 초기화 - 기본 공간 검사 레이어와 실행 취소 알림 연결
    /// </summary>
    void OnEnable ()
    {
        _options.Layer = LayerMask.NameToLayer( "PlacementTarget" );
        _options.ItemMask = LayerMask.GetMask( "InteractableItem" );
        _refreshRequested = true;
        Undo.undoRedoPerformed += HandleUndo;
    }

    /// <summary>
    /// 창 해제 - 실행 취소 알림 연결 종료
    /// </summary>
    void OnDisable ()
    {
        Undo.undoRedoPerformed -= HandleUndo;
    }

    /// <summary>
    /// 화면 구성 - 왼쪽 후보 목록과 오른쪽 설정 및 결과를 독립 스크롤로 배치
    /// </summary>
    void OnGUI ()
    {
        //화면 배치와 그리기가 같은 후보 목록을 사용하도록 배치 시점에만 갱신
        if ( _refreshRequested && Event.current.type == EventType.Layout )
            Refresh( );
        if ( AnomalyPrefabBackupUtility.IsRestoring )
            EditorGUILayout.HelpBox( "프리팹 원본 복구 중입니다. 완료 후 목록을 새로고침하세요.", MessageType.Info );
        using ( new EditorGUI.DisabledScope( EditorApplication.isPlayingOrWillChangePlaymode || AnomalyPrefabBackupUtility.IsRestoring ) )
        {
            EditorGUILayout.BeginHorizontal( );
            EditorGUILayout.BeginVertical( GUILayout.Width( position.width * 0.42f ) );
            DrawList( );
            EditorGUILayout.EndVertical( );
            EditorGUILayout.BeginVertical( GUILayout.MinWidth( 0f ) );
            DrawSettings( );
            EditorGUILayout.EndVertical( );
            EditorGUILayout.EndHorizontal( );
        }
    }


    /// <summary>
    /// 미리보기 해제 - 선택과 설정 변경 후 적용을 다시 준비
    /// </summary>
    void ClearPreview ()
    {
        _hasPreview = false;
        _available = 0;
        foreach ( PlacementSurfaceEntry entry in _entries )
            entry.Result = string.Empty;
    }

    /// <summary>
    /// 실행 취소 반영 - 변경된 씬 상태에 맞게 미리보기 해제
    /// </summary>
    void HandleUndo ()
    {
        ClearPreview( );
        Repaint( );
    }


    /// <summary>
    /// 설정 창 열기 - 배치 표면 목록과 설정 화면 표시
    /// </summary>
    [MenuItem( "Tools/Placement/배치 표면 설정" )]
    public static void Open ()
    {
        PlacementSurfaceWindow window = GetWindow<PlacementSurfaceWindow>( "배치 표면 설정" );
        window.minSize = new Vector2( 760f, 420f );
    }

    /// <summary>
    /// 후보 조회 - 열린 씬과 편집 화면 또는 선택한 씬 객체와 원본에서 후보 수집
    /// </summary>
    void Refresh ()
    {
        _entries = _source == 0 ? PlacementSurfaceUtility.CollectOpenScenes( ) :
            PlacementSurfaceUtility.Collect( Selection.gameObjects );
        _tree.Build( _entries );
        if ( _search.Length > 0 )
            _tree.ExpandMatches( _search, _searchPath );
        ClearPreview( );
        _refreshRequested = false;
        _message = $"후보 {_entries.Count}개를 조회했습니다.";
    }


    /// <summary>
    /// 목록 표시 - 원본 계층과 묶음 선택 및 검색 범위 지정
    /// </summary>
    void DrawList ()
    {
        EditorGUILayout.LabelField( "배치 표면 후보", EditorStyles.boldLabel );
        int source = GUILayout.Toolbar( _source, new[] { "열린 씬 / 편집 화면", "선택한 객체와 자식" } );
        if ( source != _source )
        {
            _source = source;
            ClearPreview( );
            _refreshRequested = true;
            Repaint( );
        }
        if ( GUILayout.Button( "목록 새로고침" ) )
        {
            _refreshRequested = true;
            Repaint( );
        }
        EditorGUI.BeginChangeCheck( );
        _searchPath = GUILayout.Toolbar( _searchPath ? 1 : 0, new[] { "이름 검색", "경로 검색" } ) == 1;
        _search = EditorGUILayout.TextField( _searchPath ? "경로 검색" : "이름 검색", _search );
        if ( EditorGUI.EndChangeCheck( ) && _search.Length > 0 )
            _tree.ExpandMatches( _search, _searchPath );
        int selected = _entries.FindAll( entry => entry.Selected ).Count;
        int hidden = _entries.FindAll( entry => entry.Selected && IsVisible( entry ) == false ).Count;
        int visible = _entries.FindAll( IsVisible ).Count;
        EditorGUILayout.LabelField( $"전체 {_entries.Count}개 / 검색 결과 {visible}개" );
        EditorGUILayout.LabelField( $"선택 {selected}개 / 숨겨진 선택 {hidden}개" );
        if ( _entries.Count == 0 )
            EditorGUILayout.HelpBox( _source == 0
                ? "열린 씬이나 프리팹 편집 화면에서 충돌체가 있는 후보를 찾지 못했습니다. 맵을 열고 목록을 새로고침하세요."
                : "하이어라키의 객체 또는 프로젝트의 맵 프리팹 원본을 선택하고 목록을 새로고침하세요.", MessageType.Info );
        else if ( visible == 0 )
            EditorGUILayout.HelpBox( "선택한 검색 범위에서 일치하는 후보가 없습니다. 이름 검색은 부모 이름과 경로를 검사하지 않습니다.", MessageType.Info );
        EditorGUILayout.BeginHorizontal( );
        if ( GUILayout.Button( "검색 결과 전체 선택" ) )
        {
            ClearPreview( );
            foreach ( PlacementSurfaceEntry entry in _entries )
            {
                if ( IsVisible( entry ) )
                    entry.Selected = true;
            }
        }
        if ( GUILayout.Button( "선택 해제" ) )
        {
            ClearPreview( );
            foreach ( PlacementSurfaceEntry entry in _entries )
                entry.Selected = false;
        }
        EditorGUILayout.EndHorizontal( );
        EditorGUILayout.LabelField( "묶음 체크는 검색에 해당하는 하위 후보를 함께 선택합니다.", EditorStyles.wordWrappedLabel );
        _listScroll = EditorGUILayout.BeginScrollView( _listScroll, GUILayout.MinHeight( 0f ) );
        if ( _tree.Draw( _search, _searchPath ) )
            ClearPreview( );
        EditorGUILayout.EndScrollView( );
    }

    /// <summary>
    /// 후보 상세 표시 - 계층에서 선택한 후보의 표면과 기존 설정 확인
    /// </summary>
    void DrawCandidate ()
    {
        PlacementSurfaceEntry entry = _tree.Active;
        if ( entry == null )
        {
            EditorGUILayout.HelpBox( "왼쪽 계층에서 후보 이름이나 상세 버튼을 누르면 대표 표면과 기존 설정을 확인할 수 있습니다.", MessageType.Info );
            return;
        }
        EditorGUILayout.BeginVertical( EditorStyles.helpBox );
        EditorGUILayout.LabelField( "개별 후보 확인", EditorStyles.boldLabel );
        EditorGUILayout.LabelField( entry.Path, EditorStyles.wordWrappedLabel );
        EditorGUI.BeginChangeCheck( );
        entry.Surface = (Collider)EditorGUILayout.ObjectField( "대표 표면", entry.Surface, typeof( Collider ), true );
        if ( EditorGUI.EndChangeCheck( ) )
            ClearPreview( );
        PlacementTarget target = entry.Owner != null ? entry.Owner.GetComponentInParent<PlacementTarget>( true ) : null;
        using ( new EditorGUI.DisabledScope( true ) )
        {
            EditorGUILayout.ObjectField( "기존 배치 대상", target, typeof( PlacementTarget ), true );
            EditorGUILayout.ObjectField( "기존 대표 표면", target != null ? target.SurfaceCollider : null, typeof( Collider ), true );
        }
        EditorGUILayout.LabelField( "현재 레이어", entry.Owner != null ? LayerMask.LayerToName( entry.Owner.layer ) : "없음" );
        if ( GUILayout.Button( "객체 찾기" ) && entry.Owner != null )
            EditorGUIUtility.PingObject( entry.Owner );
        EditorGUILayout.EndVertical( );
    }

    /// <summary>
    /// 공간 검사 레이어 표시 - 이름이 있는 일반 레이어의 선택 마스크 편집
    /// </summary>
    void DrawMask ()
    {
        List<string> names = new( );
        List<int> layers = new( );
        int compact = 0;
        for ( int layer = 0; layer < 32; layer++ )
        {
            string name = LayerMask.LayerToName( layer );
            if ( name.Length == 0 )
                continue;
            if ( ( _options.ItemMask.value & ( 1 << layer ) ) != 0 )
                compact |= 1 << names.Count;
            layers.Add( layer );
            names.Add( name );
        }
        int selected = EditorGUILayout.MaskField( "공간 검사 레이어", compact, names.ToArray( ) );
        int mask = 0;
        for ( int i = 0; i < layers.Count; i++ )
        {
            if ( ( selected & ( 1 << i ) ) != 0 )
                mask |= 1 << layers[i];
        }
        _options.ItemMask = mask;
    }

    /// <summary>
    /// 결과 표시 - 대상별 미리보기와 적용 결과를 계층 경로와 함께 표시
    /// </summary>
    void DrawResults ()
    {
        EditorGUILayout.Space( );
        EditorGUILayout.LabelField( "미리보기 / 적용 결과", EditorStyles.boldLabel );
        if ( _message.Length > 0 )
            EditorGUILayout.HelpBox( _message, MessageType.Info );
        foreach ( PlacementSurfaceEntry entry in _entries )
        {
            if ( entry.Result.Length == 0 )
                continue;
            EditorGUILayout.BeginVertical( EditorStyles.helpBox );
            EditorGUILayout.LabelField( entry.Path, EditorStyles.wordWrappedLabel );
            EditorGUILayout.LabelField( entry.Result, EditorStyles.wordWrappedLabel );
            EditorGUILayout.EndVertical( );
        }
    }

    /// <summary>
    /// 설정 표시 - 신규 기본값과 기존 설정 수정 항목 및 적용 버튼 구성
    /// </summary>
    void DrawSettings ()
    {
        _settingsScroll = EditorGUILayout.BeginScrollView( _settingsScroll, GUILayout.MinHeight( 0f ) );
        DrawCandidate( );
        EditorGUILayout.LabelField( "배치 표면 설정", EditorStyles.boldLabel );
        EditorGUILayout.HelpBox( "씬 객체, 프로젝트의 프리팹 원본과 프리팹 편집 화면을 지원합니다. 기존 설정은 기본적으로 유지하며 숨겨진 선택도 적용에 포함됩니다. 씬과 편집 화면은 실행 취소를 기록합니다. 원본은 백업 후 저장하고 아래 복구 버튼으로 되돌립니다.", MessageType.Info );
        EditorGUI.BeginChangeCheck( );
        int kind = GUILayout.Toolbar( _kind, new[] { "바닥", "벽", "일반 표면" } );
        if ( kind != _kind )
        {
            _kind = kind;
            _options.Layer = LayerMask.NameToLayer( _kind == 0 ? "Floor" : _kind == 1 ? "Wall" : "PlacementTarget" );
        }
        _options.ChangeLayer = EditorGUILayout.ToggleLeft( "표면 객체 레이어 변경", _options.ChangeLayer );
        using ( new EditorGUI.DisabledScope( _options.ChangeLayer == false ) )
            _options.Layer = EditorGUILayout.LayerField( "표면 레이어", _options.Layer );
        EditorGUILayout.Space( );
        EditorGUILayout.LabelField( "신규 기본값 / 기존 대상 수정", EditorStyles.boldLabel );
        _options.EditPlacement = EditorGUILayout.ToggleLeft( "기존 배치 방식 수정", _options.EditPlacement );
        _options.Type = (PlacementType)EditorGUILayout.EnumPopup( "배치 방식", _options.Type );
        if ( _options.Type == PlacementType.Fixed )
        {
            _options.FixedType = (FixedPlacementType)EditorGUILayout.EnumPopup( "고정 배치 방식", _options.FixedType );
            _options.Space = EditorGUILayout.FloatField( "배치 간격", _options.Space );
            _options.Tries = EditorGUILayout.IntField( "무작위 탐색 횟수", _options.Tries );
        }
        _options.EditMask = EditorGUILayout.ToggleLeft( "기존 공간 검사 레이어 수정", _options.EditMask );
        DrawMask( );
        _options.EditFood = EditorGUILayout.ToggleLeft( "기존 음식 배치 용도 수정", _options.EditFood );
        _options.Food = (FoodPlacementType)EditorGUILayout.EnumFlagsField( "음식 배치 용도", _options.Food );
        _options.EditPreparation = EditorGUILayout.ToggleLeft( "기존 준비 평가 용도 수정", _options.EditPreparation );
        _options.Preparation = (PreparationLocationType)EditorGUILayout.EnumPopup( "준비 평가 용도", _options.Preparation );
        if ( EditorGUI.EndChangeCheck( ) )
            ClearPreview( );
        EditorGUILayout.Space( );
        if ( GUILayout.Button( "선택한 표면 미리보기" ) )
        {
            _available = PlacementSurfaceUtility.Preview( _entries, _options );
            _hasPreview = true;
            _message = $"선택 {_entries.FindAll( entry => entry.Selected ).Count}개 / 구성 가능 {_available}개";
        }
        using ( new EditorGUI.DisabledScope( _hasPreview == false || _available == 0 ) )
        {
            if ( GUILayout.Button( "선택한 표면 일괄 적용" ) )
            {
                int count = PlacementSurfaceUtility.Apply( _entries, _options, out AnomalyPrefabBackupBatch backup );
                if ( backup.Entries.Count > 0 )
                    _backup = backup;
                foreach ( PlacementSurfaceEntry entry in _entries )
                    entry.Selected = false;
                _hasPreview = false;
                _message = $"{count}개 후보의 설정을 반영했습니다. 씬과 프리팹 편집 화면은 저장 상태를 확인하세요. 프로젝트 원본은 백업 후 저장했습니다. 원본 목록은 새로고침하면 갱신됩니다.";
            }
        }
        using ( new EditorGUI.DisabledScope( _backup == null || _backup.Entries.Count == 0 ) )
        {
            if ( GUILayout.Button( "최근 적용한 프리팹 원본 복구" ) )
            {
                ClearPreview( );
                AnomalyPrefabBackupUtility.Restore( _backup, new List<AnomalyPrefabBackupEntry>( _backup.Entries ) );
                _message = "원본 복구를 요청했습니다. 완료 후 목록을 새로고침하세요.";
            }
        }
        DrawResults( );
        EditorGUILayout.EndScrollView( );
    }


    /// <summary>
    /// 표시 여부 조회 - 객체 이름 또는 선택한 경로 범위로 검색
    /// </summary>
    /// <param name="entry">검색할 후보</param>
    /// <returns>현재 목록에 표시할지 여부</returns>
    bool IsVisible ( PlacementSurfaceEntry entry )
    {
        return PlacementSurfaceTree.Matches( entry, _search, _searchPath );
    }

}
