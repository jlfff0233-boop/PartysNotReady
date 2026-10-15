using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 변칙 사건 인스펙터 - 공통 상태 조회와 실행 도구
/// </summary>
[CustomEditor( typeof( AnomalyEventSystem ), true )]
[CanEditMultipleObjects]
public class AnomalyEventSystemEditor : Editor
{
    /// <summary>
    /// 주기적으로 갱신하고 다시 사용하는 사건 표시 행
    /// </summary>
    sealed class Row
    {
        public string Id;     //목록 위치 대신 유지할 발생 건 식별자
        public string Text;     //미리 구성한 표시 문구
        public string Targets;     //모든 관련 대상 이름
        public string Time;     //해결과 사후 처리 시간
    }

    const double RefreshInterval = 0.2d;     //초당 다섯 번 화면 갱신
    readonly List<Row> _rows = new( );
    readonly List<AnomalyDebugDetail> _details = new( );
    readonly List<AnomalyDebugRecord> _recent = new( );
    readonly AnomalyTargetDebugPanel _targetPanel = new( );     //등록 대상의 읽기 전용 진단
    bool _showRequestFailures;     //최근 요청의 실제 실패 목록 표시
    string _selectedRecentId;     //펼쳐 둔 종료 기록의 식별자
    int _activeCount;     //진행 건수
    int _failedCount;     //복구 대기 건수
    int _rowCount;     //현재 사용하는 행 수
    string _selectedId;     //선택한 발생 건
    string _requestMessage;     //최근 발생 요청 또는 기능 실행 결과
    MessageType _requestMessageType;
    double _nextRefresh;
    Guid _session;

    private void OnEnable ()
    {
        //Inspector가 열려 있는 동안 갱신과 플레이 전환 알림 구독
        EditorApplication.update += HandleEditorUpdate;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        _session = AnomalyDebugSession.Id;
    }

    private void OnDisable ()
    {
        //구독과 대상 참조를 함께 해제하여 닫힌 Inspector의 갱신 방지
        EditorApplication.update -= HandleEditorUpdate;
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        ClearDisplay( );
    }

    public override void OnInspectorGUI ()
    {
        //기존 직렬화 설정 아래에 단일 Presenter용 디버그 화면 추가
        DrawDefaultInspector( );
        EditorGUILayout.Space( );
        EditorGUILayout.LabelField( "변칙 사건 디버그", EditorStyles.boldLabel );

        if ( targets.Length != 1 )
        {
            EditorGUILayout.HelpBox( "실행할 Presenter 하나를 선택해 주세요.", MessageType.Info );
            return;
        }

        AnomalyEventSystem presenter = target as AnomalyEventSystem;
        if ( presenter == null )
            return;

        if ( Application.isPlaying == false )
        {
            EditorGUILayout.HelpBox( "플레이 중에 상태 조회와 테스트 버튼을 사용할 수 있습니다.", MessageType.Info );
            return;
        }

        if ( _session != AnomalyDebugSession.Id )
        {
            ClearDisplay( );
            _session = AnomalyDebugSession.Id;
        }

        if ( EditorApplication.timeSinceStartup >= _nextRefresh )
            RefreshDisplay( presenter );

        DrawStartButtons( presenter );
        _targetPanel.Draw( presenter );
        DrawOccurrences( presenter );
        DrawDetails( presenter );
        DrawRecentResults( presenter );
        if ( presenter.EventType == AnomalyEventType.Displace ||
             presenter.EventType == AnomalyEventType.Swap )
        {
            foreach ( AnomalyTarget pendingTarget in AnomalyRegister.Targets )
            {
                if ( pendingTarget != null && pendingTarget.NormalRefreshPending == true )
                    EditorGUILayout.HelpBox(
                        $"{pendingTarget.name} / 정상 기준 갱신 대기: {pendingTarget.NormalRefreshReason}", MessageType.Info );
            }
        }
    }


    #region ----- 표시 정보 갱신 -----

    /// <summary>
    /// 화면에 필요한 캐시와 선택 초기화
    /// </summary>
    void ClearDisplay ()
    {
        _rows.Clear( );
        _rowCount = 0;
        _activeCount = 0;
        _failedCount = 0;
        _details.Clear( );
        _recent.Clear( );
        _selectedId = null;
        _targetPanel.Clear( );
        _showRequestFailures = false;
        _selectedRecentId = null;
        _requestMessage = null;
        _nextRefresh = 0d;
    }

    /// <summary>
    /// 플레이 전환 중 이전 대상과 메시지 제거
    /// </summary>
    /// <param name="state">변경된 플레이 상태</param>
    void HandlePlayModeChanged ( PlayModeStateChange state )
    {
        ClearDisplay( );
        Repaint( );
    }

    /// <summary>
    /// Editor가 활성 상태일 때 정해진 간격으로 다시 그리기 요청
    /// </summary>
    void HandleEditorUpdate ()
    {
        //Editor의 매 갱신마다 수집하지 않고 0.2초 간격으로 목록 갱신
        if ( Application.isPlaying == true && targets.Length == 1 &&
             EditorApplication.timeSinceStartup >= _nextRefresh )
        {
            _nextRefresh = EditorApplication.timeSinceStartup + RefreshInterval;
            if ( target is AnomalyEventSystem presenter )
                RefreshDisplay( presenter );
            Repaint( );
        }
    }

    /// <summary>
    /// 현재 목록에서 동일한 발생 건을 다시 찾기
    /// </summary>
    /// <param name="presenter">조회할 사건 관리자</param>
    IAnomalyDebugOccurrence FindSelected ( AnomalyEventSystem presenter )
    {
        if ( string.IsNullOrEmpty( _selectedId ) == true )
            return null;

        for ( int i = 0; i < presenter.DebugOccurrenceCount; i++ )
        {
            IAnomalyDebugOccurrence occurrence = presenter.GetDebugOccurrence( i );
            //목록 순서가 달라져도 같은 발생 건을 찾고 종료된 사건은 제외
            if ( occurrence.DebugId == _selectedId &&
                 ( occurrence.Status == AnomalyStatus.Active || occurrence.Status == AnomalyStatus.Failed ) )
                return occurrence;
        }

        return null;
    }

    /// <summary>
    /// 진행 상태를 화면 표시 문구로 변환
    /// </summary>
    /// <param name="status">표시할 사건 상태</param>
    static string StatusText ( AnomalyStatus status )
    {
        return status switch
        {
            AnomalyStatus.Ready => "준비",
            AnomalyStatus.Active => "진행",
            AnomalyStatus.Failed => "실패 / 사후 처리",
            AnomalyStatus.Resolved => "해결",
            AnomalyStatus.Recovered => "사후 복구",
            AnomalyStatus.Cancelled => "취소",
            AnomalyStatus.Expired => "사후 처리 만료",
            _ => status.ToString( )
        };
    }

    /// <summary>
    /// 목록 문자열과 선택한 사건 상세를 주기적으로 갱신
    /// </summary>
    /// <param name="presenter">조회할 사건 관리자</param>
    void RefreshDisplay ( AnomalyEventSystem presenter )
    {
        _targetPanel.Refresh( presenter.EventType );
        _nextRefresh = EditorApplication.timeSinceStartup + RefreshInterval;
        _rowCount = 0;
        _activeCount = 0;
        _failedCount = 0;

        for ( int i = 0; i < presenter.DebugOccurrenceCount; i++ )
        {
            IAnomalyDebugOccurrence occurrence = presenter.GetDebugOccurrence( i );
            if ( occurrence.Status != AnomalyStatus.Active && occurrence.Status != AnomalyStatus.Failed )
                continue;

            if ( occurrence.Status == AnomalyStatus.Active )
                _activeCount++;
            else
                _failedCount++;

            //기존 행을 재사용하고 필요한 개수가 늘었을 때만 추가
            if ( _rowCount == _rows.Count )
                _rows.Add( new Row( ) );

            Row row = _rows [ _rowCount++ ];
            row.Id = occurrence.DebugId;
            row.Text = $"{row.Id.Substring( 0, 8 )} / {presenter.DebugName} / {StatusText( occurrence.Status )}";
            row.Targets = AnomalyDebugViewBuilder.GetTargetNames( occurrence );
            row.Time = occurrence.Status == AnomalyStatus.Active
                ? $"해결 {occurrence.RemainingTime:0.0}초 / 사후 처리 시작 전"
                : $"사후 처리 {occurrence.RecoveryRemainingTime:0.0}초";
        }

        IAnomalyDebugOccurrence selected = FindSelected( presenter );
        //외부 발생도 상세를 바로 표시하되 사용자의 기존 선택은 유지
        if ( selected == null )
        {
            _selectedId = _rowCount > 0 ? _rows[0].Id : null;
            selected = FindSelected( presenter );
        }
        _details.Clear( );
        if ( selected != null )
            AnomalyDebugViewBuilder.GetDetails( selected, _details );

        _recent.Clear( );
        for ( int i = presenter.RecentResultCount - 1; i >= 0; i-- )
        {
            AnomalyDebugRecord record = presenter.GetRecentResult( i );
            _recent.Add( record );
        }
        if ( _recent.Exists( record => record.Id == _selectedRecentId ) == false )
            _selectedRecentId = null;
    }

    #endregion

    #region ----- 발생 요청 -----

    /// <summary>
    /// 실제 발생 함수 실행 후 요청 결과 표시
    /// </summary>
    /// <param name="presenter">발생을 요청할 관리자</param>
    /// <param name="registered">등록 후보에서 선택할지 여부</param>
    void RequestStart ( AnomalyEventSystem presenter, bool registered )
    {
        AnomalyStartResult result = presenter.RequestDebugStart( registered );
        _requestMessageType = result.Success ? MessageType.Info : MessageType.Warning;
        _requestMessage = result.Success
            ? $"발생 성공 / 대상: {( result.Target != null ? result.Target.name : "종료 또는 무효화됨" )}"
            : $"발생 불가 [{result.Failure.Category}/{result.Failure.Code}]\n{result.Failure.Message}";

        if ( result.Success == true )
        {
            for ( int i = 0; i < presenter.DebugOccurrenceCount; i++ )
            {
                IAnomalyDebugOccurrence occurrence = presenter.GetDebugOccurrence( i );
                if ( occurrence.IsEvent( result.Event ) == true )
                {
                    _selectedId = occurrence.DebugId;
                    _requestMessage = "발생 성공 / " + AnomalyDebugViewBuilder.GetTargetNames( occurrence );
                    break;
                }
            }
        }

        RefreshDisplay( presenter );
        GUIUtility.ExitGUI( );
    }

    /// <summary>
    /// 지정 대상과 등록 후보의 발생 요청 버튼
    /// </summary>
    /// <param name="presenter">발생을 요청할 관리자</param>
    void DrawStartButtons ( AnomalyEventSystem presenter )
    {
        EditorGUILayout.LabelField( "발생 테스트", EditorStyles.boldLabel );
        using ( new EditorGUI.DisabledScope( presenter.isActiveAndEnabled == false ) )
        using ( new EditorGUILayout.HorizontalScope( ) )
        {
            if ( GUILayout.Button( "등록 후보 발생" ) )
                RequestStart( presenter, true );
        }

        if ( presenter.isActiveAndEnabled == false )
            EditorGUILayout.HelpBox( "Presenter가 비활성화되어 있습니다.", MessageType.Info );

        if ( string.IsNullOrEmpty( _requestMessage ) == false )
            EditorGUILayout.HelpBox( _requestMessage, _requestMessageType );

        _showRequestFailures = EditorGUILayout.Foldout(
            _showRequestFailures, $"최근 디버그 요청의 검사 실패 ({presenter.DebugFailureCount})", true );
        if ( _showRequestFailures )
        {
            EditorGUILayout.HelpBox(
                "실제 발생 요청에서 보고한 실패입니다. 후보를 순서대로 시도하므로 최종 발생이 성공해도 앞선 실패가 남을 수 있습니다. 아래 대상 조회는 현재 상태이며 이 기록은 요청 당시 상태입니다.",
                MessageType.Info );
            for ( int i = 0; i < presenter.DebugFailureDetailCount; i++ )
                EditorGUILayout.LabelField( presenter.GetDebugFailure( i ), EditorStyles.wordWrappedLabel );
            if ( presenter.DebugFailureCount > presenter.DebugFailureDetailCount )
                EditorGUILayout.LabelField( $"처음 {presenter.DebugFailureDetailCount}건만 표시합니다." );
        }
    }

    #endregion

    #region ----- 현재 사건과 상세 -----

    /// <summary>
    /// 진행 및 사후 처리 목록 표시
    /// </summary>
    /// <param name="presenter">표시할 사건 관리자</param>
    void DrawOccurrences ( AnomalyEventSystem presenter )
    {
        EditorGUILayout.Space( );
        EditorGUILayout.LabelField( $"현재 사건 ({_rowCount}) / 진행 {_activeCount} / 사후 처리 {_failedCount}", EditorStyles.boldLabel );

        if ( _rowCount == 0 )
            EditorGUILayout.LabelField( "진행 또는 복구 대기 중인 사건이 없습니다." );

        for ( int i = 0; i < _rowCount; i++ )
        {
            Row row = _rows [ i ];
            using ( new EditorGUILayout.VerticalScope( EditorStyles.helpBox ) )
            {
                string prefix = row.Id == _selectedId ? "▶ " : "";
                if ( GUILayout.Button( prefix + row.Text, EditorStyles.miniButton ) )
                {
                    _selectedId = row.Id;
                    RefreshDisplay( presenter );
                    GUIUtility.ExitGUI( );
                }
                EditorGUILayout.LabelField( row.Targets, EditorStyles.wordWrappedLabel );
                EditorGUILayout.LabelField( row.Time, EditorStyles.wordWrappedMiniLabel );
            }
        }
    }

    /// <summary>
    /// 기능별 상세 정보와 관련 대상 및 테스트 버튼 표시
    /// </summary>
    /// <param name="presenter">표시할 사건 관리자</param>
    void DrawDetails ( AnomalyEventSystem presenter )
    {
        IAnomalyDebugOccurrence selected = FindSelected( presenter );
        if ( selected == null )
            return;

        EditorGUILayout.Space( );
        EditorGUILayout.LabelField( "선택한 사건 상세", EditorStyles.boldLabel );
        EditorGUILayout.SelectableLabel( selected.DebugId, GUILayout.Height( EditorGUIUtility.singleLineHeight ) );

        foreach ( AnomalyDebugDetail detail in _details )
            EditorGUILayout.LabelField( $"{detail.Label}: {detail.Value}", EditorStyles.wordWrappedLabel );

        for ( int i = 0; i < selected.RelatedTargetCount; i++ )
        {
            Object related = selected.GetRelatedTarget( i );
            using ( new EditorGUILayout.HorizontalScope( ) )
            {
                using ( new EditorGUI.DisabledScope( true ) )
                    EditorGUILayout.ObjectField( $"대상 {i + 1}", related, typeof( Object ), true );

                using ( new EditorGUI.DisabledScope( related == null ) )
                {
                    if ( GUILayout.Button( "찾기", GUILayout.Width( 45f ) ) )
                        EditorGUIUtility.PingObject( related );
                    if ( GUILayout.Button( "선택", GUILayout.Width( 45f ) ) )
                        Selection.activeObject = related;
                }
            }
        }

        if ( selected.DebugActionCount == 0 )
            return;

        EditorGUILayout.HelpBox(
            "기능 API를 직접 실행합니다. E 홀딩 / 조준 / 거리 검사는 플레이어 조작으로 확인해 주세요.",
            MessageType.Info );

        for ( int i = 0; i < selected.DebugActionCount; i++ )
        {
            AnomalyDebugAction action = selected.GetDebugAction( i );
            bool disabled = presenter.isActiveAndEnabled == false ||
                string.IsNullOrEmpty( action.DisabledReason ) == false;

            using ( new EditorGUI.DisabledScope( disabled ) )
            {
                if ( GUILayout.Button( new GUIContent( action.Label, action.DisabledReason ) ) )
                {
                    //화면 갱신과 클릭 사이에 사건이 종료되거나 바뀌었는지 재확인
                    IAnomalyDebugOccurrence current = FindSelected( presenter );
                    bool success = current != null && current.TryExecuteDebugAction( i );
                    _requestMessage = success ? "기능 실행 완료" : "기능을 실행하지 못했습니다. 현재 상태를 확인해 주세요.";
                    _requestMessageType = success ? MessageType.Info : MessageType.Warning;
                    RefreshDisplay( presenter );
                    GUIUtility.ExitGUI( );
                }
            }

            if ( string.IsNullOrEmpty( action.DisabledReason ) == false )
                EditorGUILayout.LabelField( action.DisabledReason, EditorStyles.wordWrappedMiniLabel );
        }
    }

    #endregion

    #region ----- 종료 기록 -----

    /// <summary>
    /// 최근 종료 결과와 저장된 상세, 기록 삭제 버튼 표시
    /// </summary>
    /// <param name="presenter">종료 기록을 조회할 관리자</param>
    void DrawRecentResults ( AnomalyEventSystem presenter )
    {
        EditorGUILayout.Space( );
        EditorGUILayout.LabelField( "최근 종료 결과 (최대 20건 / 시간: 플레이 시작 후 초)", EditorStyles.boldLabel );

        if ( _recent.Count == 0 )
            EditorGUILayout.LabelField( "종료 기록이 없습니다." );

        foreach ( AnomalyDebugRecord record in _recent )
        {
            using ( new EditorGUILayout.VerticalScope( EditorStyles.helpBox ) )
            {
                bool expanded = _selectedRecentId == record.Id;
                string title = $"{record.Id.Substring( 0, 8 )} / {record.EventName} / {StatusText( record.Status )}";
                if ( GUILayout.Button( ( expanded ? "▼ " : "▶ " ) + title ) )
                    _selectedRecentId = expanded ? null : record.Id;

                EditorGUILayout.LabelField( record.TargetName, EditorStyles.wordWrappedLabel );
                EditorGUILayout.LabelField( $"종료 {record.Time:0.0}초 / 복구 방식: {record.RecoveryType}", EditorStyles.wordWrappedMiniLabel );
                if ( _selectedRecentId != record.Id )
                    continue;

                //정리 전 저장한 값 표시. 삭제된 대상이나 현재 상태를 다시 조회하지 않음
                for ( int i = 0; i < record.DetailCount; i++ )
                {
                    AnomalyDebugDetail detail = record.GetDetail( i );
                    EditorGUILayout.LabelField( $"{detail.Label}: {detail.Value}", EditorStyles.wordWrappedLabel );
                }
            }
        }

        if ( GUILayout.Button( "종료 기록 지우기" ) )
        {
            presenter.ClearRecentResults( );
            RefreshDisplay( presenter );
            GUIUtility.ExitGUI( );
        }
    }

    #endregion

}
