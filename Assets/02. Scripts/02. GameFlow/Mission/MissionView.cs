using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System;

/// <summary>
/// 미션 뷰 - 목표 목록 표시와 완료 연출
/// </summary>
public class MissionView : MonoBehaviour
{
    [Header( "----- 미션 박스 -----" )]
    [SerializeField] RectTransform _missionBox;     //미션 표시 박스
    [SerializeField] TMP_Text _titleText;     //미션 단계
    [SerializeField] TMP_Text _missionText;     //목표별 진행량

    [Header( "----- 목표 목록 -----" )]
    [SerializeField] RectTransform _goalRoot;       //목표 항목을 배치할 목록
    [SerializeField] MissionGoalView _goalPrefab;       //목표 항목 프리팹

    [Header( "----- 완료 표시 -----" )]
    [SerializeField, Min( 0f )] float _holdDuration = 0.8f;		//완료된 미션 화면 유지 시간

    int _stageIndex = -1;		//현재 목표 목록의 단계 번호
    bool _isInitialized;		//초기화 완료 여부

    bool _isHolding;     //완료된 미션 화면 유지 여부
    float _releaseAt;       //미션 화면 전환 시각
    /// <summary>
    /// 대기 중인 최신 표시 요청
    /// </summary>
    Action _pendingDisplay;

    /// <summary>
    /// 재활성화에서 복구할 최신 표시 요청
    /// </summary>
    Action _latestDisplay;

    /// <summary>
    /// 재사용할 목표 표시 항목
    /// </summary>
    List<MissionGoalView> _goalEntries = new( );
    /// <summary>
    /// 현재 표시 순서별 목표 아이디
    /// </summary>
    List<string> _goalIds = new( );
    /// <summary>
    /// 전체 미션 완료 화면 표시 알림 이벤트
    /// </summary>
    public event Action OnCompleted;


    /// <summary>
    /// 미션 표시 상태 초기화
    /// </summary>
    /// <returns>초기화 성공 여부</returns>
    public bool Init ()
    {
        CancelMissionHold( );
        _latestDisplay = null;

        _isInitialized = false;

        //목록 연결을 시작했다면 루트와 프리팹을 모두 지정해야 함
        if ( ( _goalRoot == null ) != ( _goalPrefab == null ) )
        {
            Debug.LogError(
                "[미션 화면 초기화 실패] " +
                "목표 목록과 항목 프리팹을 함께 연결해 주세요.", this );
            return false;
        }

        ClearMissionEntries( );
        _titleText.text = string.Empty;
        _missionText.text = string.Empty;
        _isInitialized = true;

        _missionBox.gameObject.SetActive( true );
        _titleText.gameObject.SetActive( true );
        _missionText.gameObject.SetActive( true );
        return true;
    }

    private void Update ()
    {
        if ( _isHolding == false || Time.unscaledTime < _releaseAt )
            return;

        ReleaseMission( );
    }


    private void OnEnable ()
    {
        //표시 복구는 완료 알림과 강조를 다시 요청하지 않음
        _latestDisplay?.Invoke( );
    }

    private void OnDisable ()
    {
        CancelMissionHold( );
        ClearMissionEntries( );
    }

    /// <summary>
    /// 목표 표시 항목 갱신
    /// </summary>
    /// <param name="goals">표시할 목표 목록</param>
    void UpdateMissionEntries (
        IReadOnlyList<MissionGoalDisplayData> goals )
    {
        _goalIds.Clear( );

        for ( int i = 0; i < _goalEntries.Count; i++ )
        {
            MissionGoalView entry = _goalEntries [ i ];     //재사용할 표시 항목

            //이번 단계에서 사용하지 않는 항목은 초기화 후 숨김
            if ( i >= goals.Count )
            {
                entry.Clear( );
                entry.gameObject.SetActive( false );
                continue;
            }

            MissionGoalDisplayData data = goals [ i ];     //현재 순서의 목표 정보

            entry.transform.SetSiblingIndex( i );
            entry.gameObject.SetActive( true );
            entry.SetProgress( data );

            _goalIds.Add( data.Id );
        }
    }

    /// <summary>
    /// 미션 단계와 목표 안내 문구 표시
    /// </summary>
    /// <param name="stageName">단계 이름</param>
    /// <param name="goals">목표별 안내 문구</param>
    public void SetMission ( string stageName, string goals )
    {
        _latestDisplay = () => DisplayMission( stageName, goals );

        //완료된 미션 화면 유지 중에는 최신 안내 요청만 보관
        if ( _isHolding == true )
        {
            _pendingDisplay = _latestDisplay;
            return;
        }

        if ( isActiveAndEnabled == true )
            _latestDisplay.Invoke( );
    }

    /// <summary>
    /// 미션 안내 문구 표시
    /// </summary>
    /// <param name="stageName">단계 이름</param>
    /// <param name="goals">목표별 안내 문구</param>
    void DisplayMission ( string stageName, string goals )
    {
        ClearMissionEntries( );

        if ( _goalRoot != null )
            _goalRoot.gameObject.SetActive( false );

        _titleText.text = stageName;
        _missionText.text = goals;
        _missionText.gameObject.SetActive( true );
    }

    /// <summary>
    /// 미션 단계와 목표별 진행량 표시
    /// </summary>
    /// <param name="stageIndex">표시할 단계 번호</param>
    /// <param name="stageName">단계 표시 이름</param>
    /// <param name="goals">목표별 표시 정보</param>
    public void SetMission (
        int stageIndex,
        string stageName,
        IReadOnlyList<MissionGoalDisplayData> goals )
    {
        if ( _isInitialized == false )
            return;

        if ( _goalRoot == null || _goalPrefab == null )
        {
            Debug.LogError(
                "[미션 화면 갱신 실패] " +
                "목표 목록과 항목 프리팹을 연결해 주세요.", this );
            return;
        }

        if ( goals == null )
        {
            Debug.LogError(
                "[미션 화면 갱신 실패] 목표 표시 목록이 없습니다.",
                this );
            return;
        }

        //표시 도중 일부 항목만 갱신되지 않도록 먼저 목록 검사
        for ( int i = 0; i < goals.Count; i++ )
        {
            if ( goals [ i ] != null )
                continue;

            Debug.LogError(
                "[미션 화면 갱신 실패] " +
                "목표 표시 정보가 누락되었습니다.", this );
            return;
        }

        //원본 목록 재사용과 무관하게 최신 표시 정보 보관
        MissionGoalDisplayData [ ] snapshot =
            new MissionGoalDisplayData [ goals.Count ];

        for ( int i = 0; i < goals.Count; i++ )
            snapshot [ i ] = goals [ i ];

        _latestDisplay = () => DisplayMission( stageIndex, stageName, snapshot );

        if ( _isHolding == true )
        {
            _pendingDisplay = _latestDisplay;
            return;
        }

        if ( isActiveAndEnabled == true )
            _latestDisplay.Invoke( );
    }

    /// <summary>
    /// 미션 단계와 목표 목록 표시
    /// </summary>
    /// <param name="stageIndex">표시할 단계 번호</param>
    /// <param name="stageName">단계 표시 이름</param>
    /// <param name="goals">보관한 목표별 표시 정보</param>
    void DisplayMission (
        int stageIndex, string stageName,
        IReadOnlyList<MissionGoalDisplayData> goals )
    {
        //새 단계에서는 같은 목표 아이디도
        //이전 표시 상태를 이어받지 않음
        if ( _stageIndex != stageIndex )
            ClearMissionEntries( );

        CreateMissionEntries( goals.Count );
        UpdateMissionEntries( goals );

        _stageIndex = stageIndex;
        _titleText.text = stageName;

        //목표 목록 표시 중에는 기존 안내 텍스트를 숨김
        _missionText.text = string.Empty;
        _missionText.gameObject.SetActive( false );
        _goalRoot.gameObject.SetActive( true );
    }

    /// <summary>
    /// 단계별 완료 기록과 전체 완료 안내 표시
    /// </summary>
    /// <param name="missions">단계별 완료 기록</param>
    public void ShowMissionCompleted (
        IReadOnlyList<MissionResult> missions )
    {
        _latestDisplay = () => DisplayMissionCompleted( missions );

        if ( _isHolding == true )
        {
            _pendingDisplay = () => ShowMissionCompleted( missions );
            return;
        }

        if ( isActiveAndEnabled == false )
            return;

        _latestDisplay.Invoke( );

        //실제 완료 화면을 표시한 시점에 알림
        OnCompleted?.Invoke( );
    }

    /// <summary>
    /// 단계별 완료 기록과 유지 안내를 개별 항목으로 표시
    /// </summary>
    /// <param name="missions">단계별 완료 기록</param>
    void DisplayMissionCompleted ( IReadOnlyList<MissionResult> missions )
    {
        ClearMissionEntries( );
        CreateMissionEntries( missions.Count + 1 );

        for ( int i = 0; i < missions.Count; i++ )
        {
            MissionResult mission = missions [ i ];       //표시할 단계 기록
            MissionGoalView entry = _goalEntries [ i ];       //단계별 표시 항목

            entry.transform.SetSiblingIndex( i );
            entry.gameObject.SetActive( true );
            entry.SetMessage(
                $"{mission.Name} {mission.Completed} / {mission.Total}",
                mission.Completed >= mission.Total );
        }

        MissionGoalView notice = _goalEntries [ missions.Count ];       //유지 안내 항목
        notice.transform.SetSiblingIndex( missions.Count );
        notice.gameObject.SetActive( true );
        notice.SetMessage( "남은 시간 동안\n파티를 지켜 주세요.", false );

        _titleText.text = "모든 미션 완료";
        _missionText.text = string.Empty;
        _missionText.gameObject.SetActive( false );
        _goalRoot.gameObject.SetActive( true );
    }

    /// <summary>
    /// 미션 화면 숨김과 목표 연출 정리
    /// </summary>
    public void Hide ()
    {
        CancelMissionHold( );
        _latestDisplay = null;
        ClearMissionEntries( );
        _missionBox.gameObject.SetActive( false );
    }

    /// <summary>
    /// 목표 목록의 표시 정보와 연출 초기화
    /// </summary>
    void ClearMissionEntries ()
    {
        foreach ( MissionGoalView entry in _goalEntries )
        {
            if ( entry == null )
                continue;

            entry.Clear( );
            entry.gameObject.SetActive( false );
        }

        _goalIds.Clear( );
        _stageIndex = -1;
    }

    /// <summary>
    /// 필요한 목표 표시 항목 확보
    /// </summary>
    /// <param name="count">필요한 항목 수</param>
    void CreateMissionEntries ( int count )
    {
        //기존 항목은 유지하고 부족한 수만 생성
        while ( _goalEntries.Count < count )
        {
            MissionGoalView entry = Instantiate( _goalPrefab, _goalRoot );
            entry.Clear( );
            entry.gameObject.SetActive( false );
            _goalEntries.Add( entry );
        }
    }

    /// <summary>
    /// 지정한 목표의 완료 강조 연출 요청
    /// </summary>
    /// <param name="stageIndex">완료된 목표의 단계 번호</param>
    /// <param name="goalId">완료된 목표 아이디</param>
    public void PlayGoalCompleted ( int stageIndex, string goalId )
    {
        //현재 표시 중인 단계에 대해서만 연출
        if ( _stageIndex != stageIndex ||
             string.IsNullOrEmpty( goalId ) == true )
            return;

        for ( int i = 0; i < _goalIds.Count; i++ )
        {
            if ( string.Equals(
                     _goalIds [ i ], goalId,
                     StringComparison.Ordinal ) == false )
                continue;

            MissionGoalView entry = _goalEntries [ i ];     //완료된 목표 표시 항목

            if ( entry != null )
                entry.PlayCompleted( );

            return;
        }
    }

    /// <summary>
    /// 마지막 목표 완료 연출
    /// </summary>
    /// <param name="stageIndex">완료한 미션 인덱스</param>
    /// <param name="goalId">완료한 목표 아이디</param>
    public void HoldCompletedMission (
        int stageIndex, string goalId )
    {
        //연속 완료 시 기존 화면과 유지 시간을 보존
        if ( _isHolding == true ||
             _stageIndex != stageIndex ||
             isActiveAndEnabled == false )
            return;

        PlayGoalCompleted( stageIndex, goalId );

        _isHolding = true;
        _releaseAt = Time.unscaledTime + Mathf.Max( 0f, _holdDuration );
    }

    /// <summary>
    /// 표시 요청 취소
    /// </summary>
    void CancelMissionHold ()
    {
        _isHolding = false;
        _releaseAt = 0f;
        _pendingDisplay = null;
    }

    /// <summary>
    /// 표시 요청 실행
    /// </summary>
    void ReleaseMission ()
    {
        Action display = _pendingDisplay;       //실행할 최신 표시 요청

        //표시 함수가 다시 대기하지 않도록 실행 전에 상태 해제
        CancelMissionHold( );
        display?.Invoke( );
    }

}
