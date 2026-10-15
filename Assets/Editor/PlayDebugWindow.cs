using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 플레이 통합 디버그 창 - 상태 조회, 테스트와 최근 기록
/// </summary>
public class PlayDebugWindow : EditorWindow
{
    PlayScene _scene;		//조회할 플레이 씬
    Room _room;		//방 필터와 발생 요청 방
    Enemy _enemy;		//테스트할 적
    Component _captureTarget;		//지정 NPC 또는 플레이어
    bool _autoCapturePlayer;		//미지정 시 플레이어 자동 선택
    AnomalyTarget _anomalyTarget;        //지정 사건의 대상
    AnomalyTarget _anomalySecondTarget;        //교환 사건의 두 번째 대상
    AnomalySpawnSystem _pressure;		//발생 관리자
    GameFlowController _flow;		//게임 진행
    PlaySceneTestSettings _testSettings;		//조회 씬의 테스트 설정
    int _tab;		//표시 구역
    string _search = string.Empty;		//이름과 기록 검색
    string _result = string.Empty;		//최근 요청 결과
    bool _dirty = true;		//씬 목록 재조회 여부
    double _nextRefresh;		//다음 표시 갱신
    Vector2 _scroll;		//구역 스크롤
    bool _showPoolHistory;		//풀 기록 펼침 여부
    bool _showNavigationChecks;		//경로 검사 펼침 여부
    NavMeshAgent _navigationAgent;		//경로 검사 조건의 에이전트
    RouteGraph _navigationGraph;		//검사할 노드 그래프
    float _navigationSampleDistance = 0.3f;		//검사 위치의 NavMesh 조회 거리

    /// <summary>
    /// 버튼 실행 시 보관한 링크와 노드 연결 검사 결과
    /// </summary>
    List<AnomalyDebugDetail> _navigationChecks = new( );


    /// <summary>
    /// 씬의 적 목록
    /// </summary>
    readonly List<Enemy> _enemies = new( );
    /// <summary>
    /// 씬의 NPC 목록
    /// </summary>
    readonly List<Npc> _npcs = new( );
    /// <summary>
    /// 씬의 사건 목록
    /// </summary>
    readonly List<AnomalyEventSystem> _events = new( );
    /// <summary>
    /// 재사용할 상세 표시 목록
    /// </summary>
    readonly List<AnomalyDebugDetail> _details = new( );
    /// <summary>
    /// 설정 검사 결과
    /// </summary>
    readonly List<AnomalyDebugDetail> _checks = new( );

    /// <summary>
    /// 표시할 프리팹별 풀 목록
    /// </summary>
    List<Pool> _pools = new( );
    /// <summary>
    /// 현재 풀의 대여 객체 목록
    /// </summary>
    List<GameObject> _rented = new( );
    /// <summary>
    /// 현재 풀의 보관 객체 목록
    /// </summary>
    List<GameObject> _stored = new( );

    /// <summary>
    /// 조회 씬의 AI 실행 제외 여부
    /// </summary>
    bool IsAiTest => Application.isPlaying == true &&
        _testSettings != null && _testSettings.DisableAi == true;

    /// <summary>
    /// 발생 테스트 실행 가능 여부
    /// </summary>
    bool CanTestPressure => Application.isPlaying == true &&
        IsAiTest == false && _pressure != null && _pressure.DebugIsReady == true;


    private void OnEnable ()
    {
        EditorApplication.hierarchyChanged += RequestRefresh;
        EditorApplication.playModeStateChanged += HandlePlayMode;
        EditorApplication.update += RefreshDisplay;
    }

    private void OnDisable ()
    {
        EditorApplication.hierarchyChanged -= RequestRefresh;
        EditorApplication.playModeStateChanged -= HandlePlayMode;
        EditorApplication.update -= RefreshDisplay;
    }

    private void OnGUI ()
    {
        EditorGUI.BeginChangeCheck( );
        _scene = ( PlayScene ) EditorGUILayout.ObjectField( "플레이 씬", _scene, typeof( PlayScene ), true );
        if ( EditorGUI.EndChangeCheck( ) )
        {
            _enemy = null;
            _captureTarget = null;
            _room = null;
            _checks.Clear( );
            _navigationChecks.Clear( );
            _navigationAgent = null;
            _navigationGraph = null;
            _dirty = true;
        }
        if ( _dirty && Event.current.type == EventType.Layout )
            RefreshTargets( );
        EditorGUI.BeginChangeCheck( );
        _room = ( Room ) EditorGUILayout.ObjectField( "방 필터 / 발생 방", _room, typeof( Room ), true );
        if ( EditorGUI.EndChangeCheck( ) )
            _checks.Clear( );
        _search = EditorGUILayout.TextField( "이름 / 기록 검색", _search );
        _tab = GUILayout.Toolbar( _tab, new[] { "전체 / 설정", "사건", "적", "NPC", "테스트", "기록", "풀링" } );
        if ( _result.Length > 0 )
            EditorGUILayout.HelpBox( _result, MessageType.Info );
        if ( _scene == null && _tab != 5 && _tab != 6 )
            return;
        if ( Application.isPlaying == false && _tab != 0 )
        {
            EditorGUILayout.HelpBox( "플레이 중에 상태와 테스트를 사용할 수 있습니다.", MessageType.Info );
            return;
        }
        _scroll = EditorGUILayout.BeginScrollView( _scroll );
        switch ( _tab )
        {
            case 0: DrawOverview( ); break;
            case 1: DrawEvents( ); break;
            case 2: DrawEnemies( ); break;
            case 3: DrawNpcs( ); break;
            case 4: DrawTests( ); break;
            case 5: DrawHistory( ); break;
            case 6: DrawPools( ); break;
        }
        EditorGUILayout.EndScrollView( );
    }


    /// <summary>
    /// 통합 디버그 창 열기
    /// </summary>
    [MenuItem( "Tools/Play/플레이 디버그" )]
    public static void Open ()
    {
        GetWindow<PlayDebugWindow>( "플레이 디버그" ).minSize = new Vector2( 650f, 440f );
    }

    /// <summary>
    /// 선택한 플레이 씬으로 창 열기
    /// </summary>
    /// <param name="scene">조회 씬</param>
    public static void Open ( PlayScene scene )
    {
        Open( );
        PlayDebugWindow window = GetWindow<PlayDebugWindow>( );
        window._scene = scene;
        window._dirty = true;
    }

    /// <summary>
    /// 다음 레이아웃에서 목록 갱신
    /// </summary>
    void RequestRefresh ()
    {
        _dirty = true;
        Repaint( );
    }

    /// <summary>
    /// 플레이 전환 시 이전 선택 정리
    /// </summary>
    /// <param name="state">전환 상태</param>
    void HandlePlayMode ( PlayModeStateChange state )
    {
        _enemy = null;
        _captureTarget = null;
        _room = null;
        _testSettings = null;
        PlayDebugHistory.ConsoleSourceId = 0;
        _result = string.Empty;
        _checks.Clear( );
        _navigationChecks.Clear( );
        _navigationAgent = null;
        _navigationGraph = null;
        RequestRefresh( );
    }


    /// <summary>
    /// 표시만 주기적으로 갱신
    /// </summary>
    void RefreshDisplay ()
    {
        if ( EditorApplication.timeSinceStartup < _nextRefresh )
            return;
        _nextRefresh = EditorApplication.timeSinceStartup + 0.2d;
        Repaint( );
    }

    /// <summary>
    /// 씬이 바뀌었을 때 대상 목록 수집
    /// </summary>
    void RefreshTargets ()
    {
        _dirty = false;
        if ( _scene == null )
            _scene = FindFirstObjectByType<PlayScene>( );
        _enemies.Clear( );
        _npcs.Clear( );
        _events.Clear( );
        _pressure = null;
        _flow = null;
        _testSettings = null;
        if ( _scene == null )
            return;
        _testSettings = _scene.GetComponent<PlaySceneTestSettings>( );
        var scene = _scene.gameObject.scene;
        PlaySetupValidator.Collect( scene, _enemies );
        PlaySetupValidator.Collect( scene, _npcs );
        PlaySetupValidator.Collect( scene, _events );
        List<AnomalySpawnSystem> pressures = new( );
        List<GameFlowController> flows = new( );
        PlaySetupValidator.Collect( scene, pressures );
        PlaySetupValidator.Collect( scene, flows );
        if ( pressures.Count == 1 )
            _pressure = pressures [ 0 ];
        if ( flows.Count == 1 )
            _flow = flows [ 0 ];
    }


    /// <summary>
    /// 읽기 전용 객체 표시와 선택
    /// </summary>
    /// <param name="value">표시할 객체</param>
    static void DrawObject ( UnityEngine.Object value )
    {
        EditorGUILayout.BeginHorizontal( );
        EditorGUILayout.ObjectField( value, typeof( UnityEngine.Object ), true );
        if ( GUILayout.Button( "선택", GUILayout.Width( 48f ) ) )
        {
            Selection.activeObject = value;
            EditorGUIUtility.PingObject( value );
        }
        EditorGUILayout.EndHorizontal( );
    }

    /// <summary>
    /// 상세 항목 표시
    /// </summary>
    /// <param name="details">표시 목록</param>
    static void DrawDetails ( List<AnomalyDebugDetail> details )
    {
        foreach ( AnomalyDebugDetail detail in details )
            EditorGUILayout.LabelField( detail.Label, detail.Value, EditorStyles.wordWrappedLabel );
    }

    /// <summary>
    /// 요청 결과 표시와 기록
    /// </summary>
    /// <param name="success">성공 여부</param>
    /// <param name="reason">실행 설명</param>
    void SetResult ( bool success, string reason )
    {
        _result = $"{( success ? "성공" : "거절" )}: {reason}";
        PlayDebugHistory.Record( "요청 결과", _enemy, _result );
    }

    /// <summary>
    /// 적용 난이도 설정과 방 압력, 설정 검사 표시
    /// </summary>
    void DrawOverview ()
    {
        if ( GUILayout.Button( "플레이 준비 점검 열기" ) )
            PlayReadinessWindow.Open( _scene );
        if ( Application.isPlaying == true )
            PlaySceneTestSettingsEditor.DrawAppliedSettings( _testSettings );

        if ( _flow != null && Application.isPlaying == true )
        {
            EditorGUILayout.LabelField( "게임", $"{_flow.State} / 남은 시간 {_flow.RemainingTime:F1}" );
            GameFlowControllerEditor.DrawSettings( _flow );
            EditorGUILayout.Space( );
        }

        if ( IsAiTest == true )
            EditorGUILayout.HelpBox( "AI 테스트 설정으로 압력과 자동 발생을 제외했습니다.", MessageType.Info );
        else if ( _pressure != null )
        {
            DrawObject( _pressure );
            _details.Clear( );
            _pressure.GetDebugDetails( _details );
            DrawDetails( _details );
            if ( _pressure.DebugSpawnMap != null )
                DrawObject( _pressure.DebugSpawnMap );
        }
        if ( GUILayout.Button( "설정 / 씬 연결 검사" ) )
            PlaySetupValidator.Check( _pressure, _enemies, _events, _checks, IsAiTest );
        using ( new EditorGUI.DisabledScope( CanTestPressure == false || _room == null ) )
        {
            if ( GUILayout.Button( "선택 방의 실제 후보 검사 — 추첨 / 발생 없음" ) )
            {
                _checks.Clear( );
                _pressure.CheckDebugCandidates( _room, _checks );
            }
        }
        DrawDetails( _checks );
        using ( new EditorGUI.DisabledScope( CanTestPressure == false ) )
        {
            if ( GUILayout.Button( "현재 조건으로 압력 판정 — 주기 / 확률 / 대기 유지" ) )
                _pressure.TestPressureSpawn( );
        }
    }

    /// <summary>
    /// 창에서 선택한 대상으로 사건 발생을 요청하고 결과 표시
    /// </summary>
    /// <param name="presenter">발생할 사건 시스템</param>
    /// <param name="registered">등록 후보에서 선택할지 여부</param>
    void RequestAnomalyStart ( AnomalyEventSystem presenter, bool registered )
    {
        AnomalyStartResult result = presenter.RequestDebugStart(
            registered, _anomalyTarget, _anomalySecondTarget );

        _result = result.Success == true
            ? $"{presenter.DebugName}: 발생 성공"
            : $"{presenter.DebugName}: {result.Failure.Message}";
    }

    /// <summary>
    /// 사건과 실제 발생 건 표시
    /// </summary>
    void DrawEvents ()
    {
        //테스트 대상은 창에서만 선택하고 씬에 저장하지 않음
        _anomalyTarget = EditorGUILayout.ObjectField(
            "지정 대상", _anomalyTarget, typeof( AnomalyTarget ), true ) as AnomalyTarget;
        _anomalySecondTarget = EditorGUILayout.ObjectField(
            "교환 대상 B", _anomalySecondTarget, typeof( AnomalyTarget ), true ) as AnomalyTarget;

        foreach ( AnomalyEventSystem presenter in _events )
        {
            if ( presenter == null || presenter.name.IndexOf( _search, StringComparison.OrdinalIgnoreCase ) < 0 )
                continue;
            DrawObject( presenter );
            using ( new EditorGUI.DisabledScope(
                Application.isPlaying == false || presenter.isActiveAndEnabled == false ) )
            using ( new EditorGUILayout.HorizontalScope( ) )
            {
                if ( GUILayout.Button( "지정 대상 발생" ) )
                    RequestAnomalyStart( presenter, false );
                if ( GUILayout.Button( "등록 후보 발생" ) )
                    RequestAnomalyStart( presenter, true );
            }

            for ( int i = 0; i < presenter.DebugOccurrenceCount; i++ )
            {
                IAnomalyDebugOccurrence occurrence = presenter.GetDebugOccurrence( i );
                if ( _room != null && ( occurrence.Target == null ||
                     RoomRegister.TryGetRoom( occurrence.Target.transform.position, out Room room ) == false || room != _room ) )
                    continue;
                EditorGUILayout.LabelField( occurrence.DebugId, $"{occurrence.TargetName} / {occurrence.Status} / {occurrence.RemainingTime:F1}초 / 복구 {occurrence.RecoveryRemainingTime:F1}초" );
                _details.Clear( );
                occurrence.GetDebugDetails( _details );
                DrawDetails( _details );
            }
            for ( int i = 0; i < presenter.DebugFailureDetailCount; i++ )
                EditorGUILayout.LabelField( presenter.GetDebugFailure( i ), EditorStyles.wordWrappedLabel );
        }
        EditorGUILayout.HelpBox( "개별 사건의 기능 버튼과 종료 기록은 선택 버튼으로 기존 사건 Inspector에서 확인합니다.", MessageType.Info );
    }

    /// <summary>
    /// 적 상태와 사건 연결 표시
    /// </summary>
    void DrawEnemies ()
    {
        foreach ( Enemy enemy in _enemies )
        {
            if ( Matches( enemy ) == false )
                continue;
            DrawObject( enemy );
            if ( IsAiTest == true || enemy.DebugIsReady == false )
            {
                EditorGUILayout.LabelField( "행동 상세", IsAiTest == true
                    ? "AI 테스트 설정으로 제외됨" : "초기화 전 / 상세 조회 불가" );
                continue;
            }
            _details.Clear( );
            enemy.GetDebugDetails( _details );
            DrawDetails( _details );
            if ( enemy is IAnomalyEventOwner owner && owner.EventLinker != null )
            {
                AnomalyLinker linker = owner.EventLinker;
                EditorGUILayout.LabelField( "연결 사건", $"{EnemyDebugUtility.Name( linker.DebugPresenter )} / {linker.DebugEvent?.Status.ToString( ) ?? "없음"}" );
            }
            if ( GUILayout.Button( "이 적 테스트" ) )
            {
                _enemy = enemy;
                _tab = 4;
                break;
            }
        }
    }

    /// <summary>
    /// NPC 행동과 포획 표시
    /// </summary>
    void DrawNpcs ()
    {
        foreach ( Npc npc in _npcs )
        {
            if ( Matches( npc ) == false )
                continue;
            DrawObject( npc );
            EditorGUILayout.LabelField( "행동 / 안전", $"{npc.CurrentState} / {npc.Status}" );
            if ( IsAiTest == true )
            {
                EditorGUILayout.LabelField( "행동 / 포획 테스트", "AI 테스트 설정으로 제외됨" );
                continue;
            }
            EditorGUILayout.LabelField( "포획 가능 / 구조 진행", $"{npc.CanCapture} / {npc.RescueProgressRatio:P0}" );
            _details.Clear( );
            npc.GetDebugDetails( _details );
            DrawDetails( _details );
            NpcAnimatorHandler animator = npc.GetComponentInChildren<NpcAnimatorHandler>( true );
            if ( animator != null )
                EditorGUILayout.LabelField( "포획 애니메이션", animator.GetCaptureDebugDetails( ), EditorStyles.wordWrappedLabel );
            if ( GUILayout.Button( "납치 테스트 대상으로 선택" ) )
            {
                _captureTarget = npc;
                _tab = 4;
                break;
            }
            using ( new EditorGUI.DisabledScope( npc.CanRescue == false ) )
            {
                if ( GUILayout.Button( "구조 입력 1회 — 조준 / 거리 생략" ) )
                    SetResult( npc.TryRescue( ), "NPC 구조 입력 요청" );
            }
        }
    }

    /// <summary>
    /// 상황 준비와 실제 실행 경로 테스트
    /// </summary>
    void DrawTests ()
    {
        //이동 명령이나 맵 변경 없이 현재 연결만 검사하므로 AI 제외 모드에서도 사용
        DrawNavigationChecks( );
        if ( IsAiTest == true )
        {
            EditorGUILayout.HelpBox( "AI 테스트 설정으로 활동 준비, 발생과 납치 테스트를 제외했습니다.", MessageType.Info );
            return;
        }

        _enemy = ( Enemy ) EditorGUILayout.ObjectField( "적", _enemy, typeof( Enemy ), true );
        EditorGUILayout.HelpBox( "테스트 실행 시 기존 활동과 포획을 정리합니다. 연결 사건은 취소하며 사건별 현재 변형은 남을 수 있습니다. 지정 발생은 확률 / 대기를 생략하고 성공 후 정상 쿨다운을 적용합니다.", MessageType.Info );
        if ( _enemy == null )
            return;
        if ( _enemy.DebugIsReady == false )
        {
            EditorGUILayout.HelpBox( "초기화된 적을 선택해 주세요.", MessageType.Info );
            return;
        }
        if ( _enemy.gameObject.scene != _scene.gameObject.scene ||
             ( _room != null && _room.gameObject.scene != _scene.gameObject.scene ) )
        {
            EditorGUILayout.HelpBox( "같은 플레이 씬의 방 / 적 / 대상을 지정해 주세요.", MessageType.Warning );
            return;
        }
        string reason = string.Empty;
        AnomalyLinker linker = ( _enemy as IAnomalyEventOwner )?.EventLinker;
        bool supportsPreparation = _enemy is EnemyHarm || _enemy is EnemyKidnap;
        bool ready = EnemyDebugUtility.CheckReady( _enemy, out reason );
        if ( ready == false )
            EditorGUILayout.HelpBox( reason, MessageType.Info );
        using ( new EditorGUI.DisabledScope( supportsPreparation == false || ready == false || CanTestPressure == false ) )
        {
            if ( GUILayout.Button( "방 활동 준비 — 잠복 대기 생략" ) )
            {
                if ( _enemy is EnemyKidnap kidnap )
                    SetResult( kidnap.TryPrepareDebugActivity( out reason, _room ), reason );
                else if ( _enemy is EnemyHarm harm )
                    SetResult( harm.TryPrepareDebugActivity( out reason, _room ), reason );
            }
            if ( GUILayout.Button( "저지 기준 채우기 — 보복 확률 유지" ) )
            {
                if ( _enemy is EnemyKidnap kidnap )
                    SetResult( kidnap.TryDebugRetaliation( out reason, _room ), reason );
                else if ( _enemy is EnemyHarm harm )
                    SetResult( harm.TryDebugRetaliation( out reason, _room ), reason );
            }
        }
        if ( _room != null )
        {
            EditorGUILayout.LabelField( "발생 조건", "전체 / 설정 탭의 후보 검사로 확인합니다.", EditorStyles.wordWrappedLabel );
        }
        bool fixedSpawn = _enemy is IAnomalySpawnTarget;
        Room testRoom = fixedSpawn == true ? _enemy.DebugStartRoom : _room;
        EditorGUILayout.LabelField( "실제 발생 방", EnemyDebugUtility.Name( testRoom ) );
        using ( new EditorGUI.DisabledScope( CanTestPressure == false || ready == false || testRoom == null ) )
        {
            string label = fixedSpawn == true ? "기존 활동 정리 후 재등장 — 설정된 시작 방" :
                "기존 활동 정리 후 연결 사건 발생 — 선택 방";
            if ( GUILayout.Button( label ) )
                SetResult( _pressure.TryDebugStart( testRoom, _enemy, out reason ), reason );
        }
        if ( linker?.DebugEvent != null )
        {
            AnomalyEvent occurrence = linker.DebugEvent;
            using ( new EditorGUI.DisabledScope( occurrence.Status != AnomalyStatus.Active ) )
            {
                if ( GUILayout.Button( "연결 사건 시간 초과 — 실제 실패 알림" ) )
                {
                    occurrence.Tick( Mathf.Max( 0.001f, occurrence.RemainingTime ) );
                    SetResult( true, "연결된 사건의 제한시간 소진" );
                }
            }
            EditorGUILayout.HelpBox( "해결은 사건 Inspector의 기능 테스트 또는 실제 상호작용을 사용합니다. 환경을 복구하지 않는 강제 성공은 제공하지 않습니다.", MessageType.Info );
        }
        if ( _enemy is EnemyDemand demandEnemy )
        {
            EditorGUILayout.Space( );
            EditorGUILayout.LabelField( "곰 공격 / 포획", EditorStyles.boldLabel );
            EditorGUILayout.HelpBox( "설정된 시작 방에서 새 등장으로 준비합니다. 공격 1회는 난동 대상 없이도 실행하며 " +
                "범위 안 물건에는 실제 피해를 적용합니다. 포획은 NPC 수 / 확률 분기를 생략하고 가까운 NPC를 선택합니다.", MessageType.Info );
            using ( new EditorGUI.DisabledScope( ready == false || CanTestPressure == false ) )
            {
                if ( GUILayout.Button( "곰 공격 1회 — 등장 위치 / 명중 검사" ) )
                    SetResult( demandEnemy.TryDebugAttack( out reason ), reason );
                if ( GUILayout.Button( "곰 포획 — NPC 접근 / 명중부터" ) )
                    SetResult( demandEnemy.TryDebugKidnap( false, out reason ), reason );
                if ( GUILayout.Button( "곰 즉시 포획 — 운반 / 퇴장부터" ) )
                    SetResult( demandEnemy.TryDebugKidnap( true, out reason ), reason );
            }
        }
        if ( _enemy is EnemyKidnap kidnapEnemy )
        {
            _captureTarget = ( Component ) EditorGUILayout.ObjectField( "지정 대상 (선택)", _captureTarget, typeof( Component ), true );
            if ( _captureTarget == null )
                _autoCapturePlayer = EditorGUILayout.Popup( "자동 대상 종류", _autoCapturePlayer ? 1 : 0, new[] { "NPC", "플레이어" } ) == 1;
            EditorGUILayout.HelpBox( "미지정 시 같은 씬에서 포획 가능한 가까운 대상을 선택합니다. 실행 시 적을 대상 방의 잠복 지점으로 재배치합니다. 방 / 이름 필터는 납치 대상 선택에 적용하지 않습니다.", MessageType.Info );
            using ( new EditorGUI.DisabledScope( ready == false || CanTestPressure == false ) )
            {
                if ( GUILayout.Button( "납치 — 대상 방에서 접근 / 명중부터" ) )
                    SetResult( kidnapEnemy.TryDebugKidnap( _captureTarget, false, out reason, _autoCapturePlayer ), reason );
                if ( GUILayout.Button( "즉시 포획 — 운반 / 도주부터" ) )
                    SetResult( kidnapEnemy.TryDebugKidnap( _captureTarget, true, out reason, _autoCapturePlayer ), reason );
            }
        }
    }

    /// <summary>
    /// Agent 조건의 링크와 노드 검사 입력 및 결과 표시
    /// </summary>
    void DrawNavigationChecks ()
    {
        _showNavigationChecks = EditorGUILayout.Foldout( _showNavigationChecks, "링크 / 노드 경로 검사", true );
        if ( _showNavigationChecks == false )
            return;

        _navigationAgent = ( NavMeshAgent ) EditorGUILayout.ObjectField(
            "검사 Agent", _navigationAgent, typeof( NavMeshAgent ), true );
        _navigationGraph = ( RouteGraph ) EditorGUILayout.ObjectField(
            "노드 그래프 (선택)", _navigationGraph, typeof( RouteGraph ), true );
        _navigationSampleDistance = Mathf.Max( 0.01f,
            EditorGUILayout.FloatField( "NavMesh 조회 거리", _navigationSampleDistance ) );
        EditorGUILayout.HelpBox( "NavMesh 위의 활성 Agent를 지정하세요. 그래프를 생략하면 링크 양 끝만 검사합니다. " +
            "검사는 위치와 연결을 변경하지 않으며 완전 경로가 있어도 해당 링크 통과 성공을 보장하지 않습니다.", MessageType.Info );
        using ( new EditorGUI.DisabledScope( _navigationAgent == null ||
            _navigationAgent.gameObject.scene != _scene.gameObject.scene ) )
        {
            if ( GUILayout.Button( "Agent 조건으로 링크 / 노드 검사" ) )
                PlayNavigationChecker.Check( _navigationAgent, _navigationGraph,
                    _navigationSampleDistance, _navigationChecks );
        }
        if ( GUILayout.Button( "경로 검사 결과 복사" ) )
        {
            List<string> lines = new( );
            foreach ( AnomalyDebugDetail detail in _navigationChecks )
                lines.Add( $"{detail.Label}: {detail.Value}" );
            EditorGUIUtility.systemCopyBuffer = string.Join( "\n", lines );
        }
        DrawDetails( _navigationChecks );
        EditorGUILayout.Space( );
    }

    /// <summary>
    /// 최근 기록 검색과 복사
    /// </summary>
    void DrawHistory ()
    {
        PlayDebugHistory.ConsoleEnabled = EditorGUILayout.Toggle( "콘솔에도 출력", PlayDebugHistory.ConsoleEnabled );
        EditorGUILayout.BeginHorizontal( );
        if ( GUILayout.Button( "전체 대상 로그" ) )
            PlayDebugHistory.ConsoleSourceId = 0;
        using ( new EditorGUI.DisabledScope( Selection.activeGameObject == null ) )
        {
            if ( GUILayout.Button( "선택 대상 로그" ) )
            {
                Component source = Selection.activeGameObject.GetComponent<Enemy>( );
                if ( source == null )
                    source = Selection.activeGameObject.GetComponent<Npc>( );
                if ( source == null )
                    source = Selection.activeGameObject.GetComponent<AnomalyEventSystem>( );
                if ( source != null )
                    PlayDebugHistory.ConsoleSourceId = source.GetInstanceID( );
            }
        }
        EditorGUILayout.EndHorizontal( );
        EditorGUILayout.LabelField( "콘솔 범위", PlayDebugHistory.ConsoleSourceId == 0 ? "전체" : $"선택 대상 ID {PlayDebugHistory.ConsoleSourceId}" );
        if ( GUILayout.Button( "기록 비우기" ) )
            PlayDebugHistory.Clear( );
        List<string> visible = new( );
        foreach ( string record in PlayDebugHistory.Records )
        {
            if ( record.IndexOf( _search, StringComparison.OrdinalIgnoreCase ) >= 0 )
                visible.Add( record );
        }
        if ( GUILayout.Button( "검색 결과 복사" ) )
            EditorGUIUtility.systemCopyBuffer = string.Join( "\n", visible );
        foreach ( string record in visible )
            EditorGUILayout.LabelField( record, EditorStyles.wordWrappedLabel );
    }

    /// <summary>
    /// 풀 객체의 ID, 씬, 부모와 선택 버튼 표시
    /// </summary>
    /// <param name="items">표시할 객체 목록</param>
    void DrawPoolObjects ( List<GameObject> items )
    {
        foreach ( GameObject item in items )
        {
            DrawObject( item );
            string parent = item.transform.parent != null ? item.transform.parent.name : "씬 루트";
            EditorGUILayout.LabelField( $"ID {item.GetInstanceID( )}",
                $"씬 {item.scene.name} / 부모 {parent} / 활성 {item.activeInHierarchy}",
                EditorStyles.wordWrappedLabel );
        }
    }

    /// <summary>
    /// 프리팹별 풀과 실제 대여, 보관 상태 표시
    /// </summary>
    void DrawPools ()
    {
        //목록 조회가 불가능해도 이미 보관한 풀 기록은 복사 / 삭제할 수 있음
        _showPoolHistory = EditorGUILayout.Foldout( _showPoolHistory,
            $"풀 기록 ({PlayDebugHistory.PoolRecords.Count}개)", true );
        if ( _showPoolHistory == true )
        {
            if ( GUILayout.Button( "풀 기록 비우기" ) )
                PlayDebugHistory.ClearPool( );
            List<string> visible = new( );
            foreach ( string record in PlayDebugHistory.PoolRecords )
            {
                if ( record.IndexOf( _search, StringComparison.OrdinalIgnoreCase ) >= 0 )
                    visible.Add( record );
            }
            if ( GUILayout.Button( "풀 검색 결과 복사" ) )
                EditorGUIUtility.systemCopyBuffer = string.Join( "\n", visible );
            foreach ( string record in visible )
                EditorGUILayout.LabelField( record, EditorStyles.wordWrappedLabel );
        }
        EditorGUILayout.Space( );
        GameManager game = GameManager.Instance;
        if ( game == null || game.IsReady == false )
        {
            EditorGUILayout.HelpBox( "GameManager 초기화가 완료되어야 풀을 조회할 수 있습니다.", MessageType.Info );
            return;
        }

        EditorGUILayout.HelpBox(
            "재료는 플레이 씬 초기화 때 설정한 수량만큼 준비됩니다. 보관 객체가 없으면 추가 생성됩니다. " +
            "비활성 객체는 DontDestroyOnLoad의 Pool_프리팹명 아래 보관됩니다. " +
            "같은 ID의 반환 → 대여 기록으로 재사용을 확인하세요. 풀은 전체 씬 기준이며 방 필터는 적용하지 않습니다.", MessageType.Info );
        game.Pool.GetDebugPools( _pools );
        if ( _pools.Count == 0 )
            EditorGUILayout.LabelField( "생성된 풀이 없습니다. 플레이 씬의 공급원 초기화 상태를 확인해 주세요." );

        foreach ( Pool pool in _pools )
        {
            GameObject prefab = pool.DebugPrefab;
            if ( prefab == null || prefab.name.IndexOf( _search, StringComparison.OrdinalIgnoreCase ) < 0 )
                continue;

            pool.GetDebugObjects( _rented, _stored );
            EditorGUILayout.BeginVertical( EditorStyles.helpBox );
            EditorGUILayout.LabelField( prefab.name, EditorStyles.boldLabel );
            DrawObject( prefab );
            DrawObject( pool.DebugStorage );
            EditorGUILayout.LabelField( "객체 수", $"대여 {_rented.Count} / 보관 {_stored.Count}" );
            if ( string.IsNullOrEmpty( pool.DebugReturnFailure ) == false )
                EditorGUILayout.HelpBox( pool.DebugReturnFailure, MessageType.Warning );
            EditorGUILayout.LabelField( "대여 객체", EditorStyles.boldLabel );
            DrawPoolObjects( _rented );
            EditorGUILayout.LabelField( "보관 객체", EditorStyles.boldLabel );
            DrawPoolObjects( _stored );
            EditorGUILayout.EndVertical( );
        }
    }


    /// <summary>
    /// 표시 대상 필터
    /// </summary>
    /// <param name="value">조회 대상</param>
    /// <returns>표시 여부</returns>
    bool Matches ( Component value )
    {
        if ( value == null || value.name.IndexOf( _search, StringComparison.OrdinalIgnoreCase ) < 0 )
            return false;
        return _room == null || ( RoomRegister.TryGetRoom( value.transform.position, out Room room ) && room == _room );
    }

}
