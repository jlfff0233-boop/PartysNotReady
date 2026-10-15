using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 진행 제어 - 시간, 미션 진행과 종료 결과 연결
/// </summary>
public class GameFlowController : MonoBehaviour
{
    [Header ( "----- 설정 -----" )]
    [SerializeField] GameDifficultyDataMap _difficultyMap;      //난이도별 설정
    [SerializeField] GameDifficulty _defaultDifficulty = GameDifficulty.Normal;		//직접 실행 난이도

    [Header ( "----- 화면 -----" )]
    [SerializeField] GameFlowHud _view;		//진행 화면
    [SerializeField] GameResultView _resultView;		//결과 화면

    [Header ( "----- 사운드 -----" )]
    [SerializeField] GameFlowSound _flowSound;		//게임 흐름 사운드
    [SerializeField] GameResultSound _resultSound;		//게임 결과 사운드

    GameDifficultySettings _settings;       //이번 판의 난이도 설정
    GameFlowData _data;		//이번 판의 게임 흐름

    bool _isUpdating;		//게임 갱신 여부
    bool _hasInitialized;		//성공 여부와 별개인 초기화 시도 기록

    GameFlowModel _model;		//시간과 종료 상태
    MissionProgress _missions;		//미션 진행

    ResultEvaluationSystem _evaluation;		//종료 평가 결과
    /// <summary>
    /// 화면 갱신에 사용할 목표별 표시 정보
    /// </summary>
    List<MissionGoalDisplayData> _missionGoals = new( );
    GameResult _result;     //종료 결과
    /// <summary>
    /// 평가 전 씬 처리
    /// </summary>
    Action _prepareEnd;


    #region ----- 프로퍼티/이벤트 -----

    /// <summary>
    /// 이번 판의 난이도
    /// </summary>
    public GameDifficulty Difficulty =>
        _settings == null ? _defaultDifficulty : _settings.Difficulty;

    /// <summary>
    /// 선택된 난이도의 설정
    /// </summary>
    public GameDifficultySettings Settings => _settings;

    /// <summary>
    /// 진행 상태
    /// </summary>
    public GameFlowState State =>
        _model == null ? GameFlowState.Ready : _model.State;

    /// <summary>
    /// 남은 시간
    /// </summary>
    public float RemainingTime =>
        _model == null ? 0f : _model.RemainingTime;

    /// <summary>
    /// 게임 시작부터 실제로 진행한 시간
    /// </summary>
    public float ElapsedTime =>
        _model == null ? 0f : _model.ElapsedTime;

    /// <summary>
    /// 현재 미션 단계
    /// </summary>
    public int CurrentStageIndex =>
        _missions == null ? 0 : _missions.CurrentStageIndex;

    /// <summary>
    /// 미션 단계 수
    /// </summary>
    public int StageCount =>
        _missions == null ? 0 : _missions.StageCount;

    /// <summary>
    /// 종료 결과
    /// </summary>
    public GameResult Result => _result;

    /// <summary>
    /// 게임 진행 시간 변경 이벤트(경과 시간)
    /// </summary>
    public event Action<float> OnElapsedTimeChanged;

    /// <summary>
    /// 미션 진행 이벤트(증가량, 전체 미션 완료 여부)
    /// </summary>
    public event Action<int , bool> OnMissionProgressed;

    /// <summary>
    /// 진행 상태 변경 이벤트(게임 진행 상태)
    /// </summary>
    public event Action<GameFlowState> OnStateChanged;

    /// <summary>
    /// 미션 진행 변경 이벤트
    /// </summary>
    public event Action OnMissionChanged;

    /// <summary>
    /// 게임 종료 이벤트(종료 결과)
    /// </summary>
    public event Action<GameResult> OnEnded;

    /// <summary>
    /// 시작 화면 복귀 요청
    /// </summary>
    public event Action OnReturnRequested;

    /// <summary>
    /// 전체 미션 완료 화면 표시 알림 이벤트
    /// </summary>
    public event Action OnMissionCompleted;

    /// <summary>
    /// 결과 화면 표시 알림 이벤트(게임 결과)
    /// </summary>
    public event Action<GameOutcome> OnResult;

    #endregion

    #region ----- 초기화/시작 -----

    /// <summary>
    /// 선택된 설정으로 게임 진행 초기화
    /// </summary>
    /// <param name="evaluation">종료 평가</param>
    /// <param name="prepareEnd">평가 전 씬 처리</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init (
        ResultEvaluationSystem evaluation , Action prepareEnd )
    {
        if ( _hasInitialized == true )
            return false;

        bool selectionReady = TryPrepareSettings (
            out string reason );		//설정 준비 결과

        _hasInitialized = true;

        if ( selectionReady == false )
        {
            Debug.LogError ( $"[게임 초기화 실패] {reason}" , this );
            return false;
        }

        if ( evaluation == null || prepareEnd == null )
        {
            Debug.LogError (
                "[게임 초기화 실패] 종료 평가와 씬 처리를 연결해 주세요." , this );
            return false;
        }

        if ( CheckSettings ( ) == false )
            return false;

        _evaluation = evaluation;
        _prepareEnd = prepareEnd;

        _missions = new MissionProgress ( _data );

        if ( _missions.IsReady == false )
        {
            Debug.LogError (
                $"[게임 초기화 실패] {_missions.FailureReason}" , this );
            return false;
        }

        //화면 준비 실패 시 진행 모델을 만들지 않고 씬 재로드로 복구
        if ( _resultView.Init ( ) == false || _view.Init ( ) == false )
            return false;

        //게임 종료 후에도 결과 화면의 복귀 요청 유지
        _resultView.OnReturnRequested += HandleReturnRequested;

        _model = new GameFlowModel (
            _data.Duration , _data.FinalRushDuration );

        //게임 시작 전에 화면 표시 알림과 사운드 담당 연결
        SubscribeViewEvents( );
        _flowSound.Init( this );
        _resultSound.Init( this );

        return true;
    }

    /// <summary>
    /// 결과 화면의 복귀 요청 연결 해제
    /// </summary>
    private void OnDestroy ( )
    {
        UnsubscribeViewEvents( );

        if ( _resultView != null )
            _resultView.OnReturnRequested -= HandleReturnRequested;
    }

    private void Update ( )
    {
        UpdateProgress ( Time.deltaTime , NpcRegister.InitialRemainingCount );
    }


    /// <summary>
    /// 전체 미션 완료 화면 표시 알림 전달
    /// </summary>
    void HandleMissionCompleted ()
    {
        OnMissionCompleted?.Invoke( );
    }

    /// <summary>
    /// 결과 화면 표시 알림 전달
    /// </summary>
    /// <param name="outcome">표시한 게임 결과</param>
    void HandleResult ( GameOutcome outcome )
    {
        OnResult?.Invoke( outcome );
    }

    /// <summary>
    /// 화면 표시 알림 연결 해제
    /// </summary>
    void UnsubscribeViewEvents ()
    {
        if ( _view != null )
            _view.OnMissionCompleted -= HandleMissionCompleted;

        if ( _resultView != null )
            _resultView.OnResult -= HandleResult;
    }

    /// <summary>
    /// 화면 표시 알림 연결
    /// </summary>
    void SubscribeViewEvents ()
    {
        UnsubscribeViewEvents( );
        _view.OnMissionCompleted += HandleMissionCompleted;
        _resultView.OnResult += HandleResult;
    }


    /// <summary>
    /// 선택된 설정을 유지하거나 기본 난이도 준비
    /// </summary>
    /// <param name="reason">설정 준비 실패 사유</param>
    /// <returns>준비 성공 여부</returns>
    public bool TryPrepareSettings ( out string reason )
    {
        reason = string.Empty;

        if ( _settings != null )
            return true;

        return TrySelectDifficulty ( _defaultDifficulty , out reason );
    }

    /// <summary>
    /// 초기화 전 난이도 선택
    /// </summary>
    /// <param name="difficulty">선택할 난이도</param>
    /// <param name="reason">선택 실패 사유</param>
    /// <returns>선택 성공 여부</returns>
    public bool TrySelectDifficulty (
        GameDifficulty difficulty , out string reason )
    {
        reason = string.Empty;

        if ( _hasInitialized == true )
        {
            reason = "게임 초기화 후에는 난이도를 변경할 수 없습니다.";
            return false;
        }

        if ( _difficultyMap == null )
        {
            reason = "난이도 데이터맵을 연결해 주세요.";
            return false;
        }

        if ( _difficultyMap.CheckSettings ( out reason ) == false )
            return false;

        if ( _difficultyMap.TryGetSettings (
            difficulty , out GameDifficultySettings settings ) == false )
        {
            reason = $"난이도 설정이 없습니다: {difficulty}";
            return false;
        }

        _settings = settings;
        _data = settings.Flow;
        return true;
    }

    /// <summary>
    /// 게임 시작
    /// </summary>
    /// <returns>시작 성공 여부</returns>
    public bool Begin ( )
    {
        if ( isActiveAndEnabled == false || _model == null )
            return false;

        if ( _model.TryStart ( ) == false )
            return false;

        _missions.Begin ( );		//미션 시작
        _isUpdating = true;		//갱신 여부
        OnElapsedTimeChanged?.Invoke ( _model.ElapsedTime );		//시간 경과 이벤트 발행

        //뷰 초기화
        _view.SetTime ( _model.RemainingTime );
        _view.SetGuestCount ( NpcRegister.InitialCount , NpcRegister.InitialCount );
        _view.SetFinalRush ( _model.State == GameFlowState.FinalRush );
        RefreshMission ( );

        OnStateChanged?.Invoke ( _model.State );
        return true;
    }


    #endregion


    /// <summary>
    /// 결과 화면의 복귀 요청 전달
    /// </summary>
    void HandleReturnRequested ( )
    {
        OnReturnRequested?.Invoke ( );
    }

    #region ----- 진행 갱신 -----

    /// <summary>
    /// 플레이 로그 표시
    /// </summary>
    /// <param name="message">표시할 문구</param>
    public void ShowLog ( string message )
    {
        if ( _isUpdating == true && _view != null )
            _view.ShowLog ( message );
    }

    /// <summary>
    /// 현재 장소 표시 요청 전달
    /// </summary>
    /// <param name="room">현재 방, 없으면 숨김</param>
    public void SetLocation ( Room room )
    {
        if ( _isUpdating == true && _view != null )
            _view.SetLocation( room != null ? room.DisplayName : null );
    }

    /// <summary>
    /// 현재 분위기를 HUD에 표시
    /// </summary>
    /// <param name="mood">현재 분위기(0~100)</param>
    public void SetMood ( float mood )
    {
        if ( _model == null || _view == null )
            return;

        _view.SetMood ( mood / 100f );
    }

    /// <summary>
    /// 종료 화면에 코인 지급 결과 표시
    /// </summary>
    /// <param name="amount">획득 코인</param>
    /// <param name="balance">지급 후 잔액</param>
    /// <param name="isGranted">지급 성공 여부</param>
    public void ShowReward ( int amount , int balance , bool isGranted )
    {
        if ( _result != null && _resultView != null )
            _resultView.ShowReward ( amount , balance , isGranted );
    }

    /// <summary>
    /// 시간과 손님 수를 반영하고 화면 갱신
    /// </summary>
    /// <param name="deltaTime">경과 시간</param>
    /// <param name="remainingNpcCount">남은 손님 수</param>
    void UpdateProgress ( float deltaTime , int remainingNpcCount )
    {
        if ( _isUpdating == false )
            return;

        GameFlowState previous = _model.State;
        _model.Update ( deltaTime , remainingNpcCount );

        if ( _model.State == GameFlowState.Ended )
            _isUpdating = false;

        OnElapsedTimeChanged?.Invoke ( _model.ElapsedTime );

        _view.SetTime ( _model.RemainingTime );
        _view.SetGuestCount ( remainingNpcCount , NpcRegister.InitialCount );

        if ( _model.State == GameFlowState.Ended )
        {
            CompleteGame ( remainingNpcCount );
            return;
        }

        if ( previous != _model.State )
        {
            _view.SetFinalRush ( _model.State == GameFlowState.FinalRush );
            OnStateChanged?.Invoke ( _model.State );
        }
    }

    #endregion

    #region ----- 미션 -----

    /// <summary>
    /// 단계 안에서 지정한 목표의 위치 조회
    /// </summary>
    /// <param name="stageIndex">단계 번호</param>
    /// <param name="goalId">찾을 목표 아이디</param>
    /// <returns>목표 위치, 없으면 -1</returns>
    int FindMissionGoal ( int stageIndex , string goalId )
    {
        for ( int i = 0; i < _missions.GetGoalCount( stageIndex ); i++ )
        {
            if ( string.Equals(
                     _missions.GetGoalId( stageIndex , i ) ,
                     goalId , StringComparison.Ordinal ) == true )
                return i;
        }

        return -1;
    }

    /// <summary>
    /// 지정한 단계의 목표 표시 정보 구성과 화면 갱신
    /// </summary>
    /// <param name="stageIndex">표시할 단계 번호</param>
    void ShowMissionStage ( int stageIndex )
    {
        _missionGoals.Clear( );

        for ( int i = 0; i < _missions.GetGoalCount( stageIndex ); i++ )
        {
            _missions.GetGoal(
                stageIndex , i ,
                out string name , out int current , out int required );

            //진행 모델을 직접 넘기지 않고 표시 시점의 값으로 구성
            _missionGoals.Add( new MissionGoalDisplayData(
                _missions.GetGoalId( stageIndex , i ) ,
                name , current , required ) );
        }

        _view.SetMission(
            stageIndex ,
            _missions.GetStageName( stageIndex ) ,
            _missionGoals );
    }

    /// <summary>
    /// 현재 미션 단계 또는 전체 완료 안내 표시
    /// </summary>
    void RefreshMission ( )
    {
        if ( _missions.IsCompleted == true )
        {
            _view.ShowMissionCompleted ( CreateMissionResults ( ) );
            return;
        }

        ShowMissionStage( _missions.CurrentStageIndex );
    }

    /// <summary>
    /// 미션 완료 개수 반영과 최초 목표 완료 연출 요청
    /// </summary>
    /// <param name="goalId">목표 아이디</param>
    /// <param name="resultId">제작 결과 또는 물건 아이디</param>
    /// <param name="amount">요청한 완료 개수</param>
    /// <param name="isItemPlacement">일반 아이템 배치 여부</param>
    /// <returns>반영 여부</returns>
    public bool TryAddMission (
        string goalId , string resultId , int amount = 1 ,
        bool isItemPlacement = false )
    {
        if ( _isUpdating == false ||
             _missions.IsCompleted == true )
            return false;

        //반영 과정에서 단계가 변경될 수 있으므로 이전 위치를 보관
        int stageIndex = _missions.CurrentStageIndex;     //반영 전 단계
        int goalIndex = FindMissionGoal( stageIndex , goalId );     //반영 대상

        if ( goalIndex < 0 )
            return false;

        _missions.GetGoal(
            stageIndex , goalIndex ,
            out _ , out int previousCurrent , out int required );

        if ( _missions.TryAdd (
            goalId , resultId , amount ,
            out int addedAmount , isItemPlacement ) == false )
            return false;

        //단계가 넘어갔더라도 이전 단계의 목표를 조회
        _missions.GetGoal(
            stageIndex , goalIndex ,
            out _ , out int current , out _ );

        bool newlyCompleted =
            previousCurrent < required && current >= required;     //최초 완료 여부

        bool stageChanged =
            _missions.CurrentStageIndex != stageIndex;     //단계 변경 여부

        bool allCompleted = _missions.IsCompleted;     //이번 반영의 전체 완료 여부

        //현재 미션이 완료되면 해당 미션의 최종 진행량부터 표시
        if ( newlyCompleted == true && stageChanged == true )
        {
            ShowMissionStage( stageIndex );
            _view.HoldCompletedMission( stageIndex, goalId );
        }

        //화면 유지 중에는 최신 미션 표시 요청으로 보관
        RefreshMission( );

        //현재 미션 안에서 완료된 목표는 바로 연출
        if ( newlyCompleted == true && stageChanged == false )
            _view.PlayGoalCompleted( stageIndex, goalId );

        //게임 이벤트는 화면 연출 완료를 기다리지 않고 즉시 전달
        OnMissionChanged?.Invoke ( );
        OnMissionProgressed?.Invoke (
            addedAmount , allCompleted );

        return true;
    }

    /// <summary>
    /// 단계 이름 조회
    /// </summary>
    /// <param name="stageIndex">단계 번호</param>
    /// <returns>단계 이름</returns>
    public string GetStageName ( int stageIndex )
    {
        return _missions.GetStageName ( stageIndex );
    }

    /// <summary>
    /// 목표 수 조회
    /// </summary>
    /// <param name="stageIndex">단계 번호</param>
    /// <returns>목표 수</returns>
    public int GetGoalCount ( int stageIndex )
    {
        return _missions.GetGoalCount ( stageIndex );
    }

    /// <summary>
    /// 목표 진행 조회
    /// </summary>
    /// <param name="stageIndex">단계 번호</param>
    /// <param name="goalIndex">목표 번호</param>
    /// <param name="name">목표 이름</param>
    /// <param name="current">완료 개수</param>
    /// <param name="required">필요 개수</param>
    public void GetGoal (
        int stageIndex , int goalIndex ,
        out string name , out int current , out int required )
    {
        _missions.GetGoal (
            stageIndex , goalIndex , out name , out current , out required );
    }

    #endregion

    #region ----- 종료 -----

    /// <summary>
    /// 미션 결과 생성
    /// </summary>
    /// <returns>단계별 완료 기록</returns>
    MissionResult [ ] CreateMissionResults ( )
    {
        MissionResult [ ] results = new MissionResult [ _missions.StageCount ];
        for ( int stage = 0 ; stage < results.Length ; stage++ )
        {
            int total = _missions.GetGoalCount ( stage );
            int completed = 0;
            for ( int goal = 0 ; goal < total ; goal++ )
            {
                _missions.GetGoal ( stage , goal , out _ , out int current , out int required );
                if ( current >= required )
                    completed++;
            }
            results [ stage ] = new MissionResult (
                _missions.GetStageName ( stage ) , completed , total );
        }
        return results;
    }


    /// <summary>
    /// 게임 종료 처리
    /// </summary>
    /// <param name="remainingNpcCount">잔존 NPC 수</param>
    void CompleteGame ( int remainingNpcCount )
    {
        if ( _result != null )
            return;

        //종료 알림에서 미션 결과가 들어와도 반영되지 않도록 먼저 차단
        _isUpdating = false;

        //발생 연결을 종료한 뒤 씬 정리 전 상태로 평가
        _prepareEnd ( );
        _evaluation.FinalizeAt ( _model.ElapsedTime );
        _evaluation.TryGetFinalScore (
            out GameScoreResult [ ] scores , out string grade );

        _missions.End ( );

        GameOutcome outcome = _model.DetermineOutcome (
            _model.EndReason , _missions.IsCompleted , scores , grade );

        _result = new GameResult (
            _model.EndReason , outcome , _model.ElapsedTime ,
            remainingNpcCount ,
            _missions.CurrentStageIndex ,
            _missions.IsCompleted ,
            NpcRegister.InitialCount , CreateMissionResults ( ) ,
            scores , grade );

        _view.Hide ( );
        _resultView.ShowResult ( _result );

        OnStateChanged?.Invoke ( GameFlowState.Ended );
        OnEnded?.Invoke ( _result );
    }

    #endregion

    #region ----- 디버그 -----
#if UNITY_EDITOR

    /// <summary>
    /// 진행 중인 플레이 확인
    /// </summary>
    /// <returns>플레이 진행 여부</returns>
    bool CheckDebugPlay ( )
    {
        if ( Application.isPlaying == true && _isUpdating == true && isActiveAndEnabled == true )
            return true;

        Debug.LogWarning ( "[진행 검증] 플레이 진행 중에 실행해 주세요." , this );
        return false;
    }

    /// <summary>
    /// 종료 조건 또는 마무리 구간 검증
    /// </summary>
    /// <param name="reason">종료 사유, None이면 마무리 구간 진입</param>
    public void DebugProgress ( GameEndReason reason )
    {
        if ( CheckDebugPlay ( ) == false )
            return;

        if ( reason == GameEndReason.None && _data.FinalRushDuration <= 0f )
        {
            Debug.LogWarning ( "[진행 검증] Final Rush 시간이 0이라 진입할 수 없습니다." , this );
            return;
        }

        //실제 NPC를 삭제하지 않고 전멸 입력만 재현
        int count = reason == GameEndReason.AllNpcDeparted ? 0 : NpcRegister.InitialRemainingCount;
        float elapsed;		//검증할 경과 시간

        if ( reason == GameEndReason.TimeExpired )
        {
            elapsed = _model.RemainingTime;
        }
        else if ( reason == GameEndReason.None )
        {
            elapsed = Mathf.Max ( 0f , _model.RemainingTime - _data.FinalRushDuration );
        }
        else
        {
            elapsed = 0f;
        }

        UpdateProgress ( elapsed , count );
        Debug.Log ( $"[진행 검증] 상태: {State} / 남은 시간: {RemainingTime:F1} / 종료: {_model.EndReason}" , this );
    }

    /// <summary>
    /// 현재 단계 또는 전체 미션 완료
    /// </summary>
    /// <param name="completeAll">전체 단계 완료 여부</param>
    public void DebugCompleteMission ( bool completeAll )
    {
        if ( CheckDebugPlay ( ) == false )
            return;

        int first = _missions.CurrentStageIndex;
        int end = completeAll ? _missions.StageCount : Mathf.Min ( first + 1 , _missions.StageCount );

        for ( int stage = first ; stage < end ; stage++ )
        {
            for ( int index = 0 ; index < _missions.GetGoalCount ( stage ) ; index++ )
            {
                _missions.GetGoal (
                    stage , index , out _ , out int current , out int required );

                if ( current >= required )
                    continue;

                //실제 완료 입력 경로로 UI, 단계 전환과 중복 방지까지 처리
                TryAddMission (
                    _missions.GetGoalId ( stage , index ) ,
                    Guid.NewGuid ( ).ToString ( ) , required - current );
            }
        }

        Debug.Log ( $"[미션 검증] 완료 단계: " +
            $"{CurrentStageIndex}/{StageCount} / " +
            $"전체 완료: {_missions.IsCompleted} / 진행 상태: {State}" , this );
    }

#endif
    #endregion

    #region ----- 설정 검사 -----

    /// <summary>
    /// 선택된 흐름 데이터의 시간 설정 확인
    /// </summary>
    /// <returns>설정 유효 여부</returns>
    bool CheckSettings ( )
    {
        float duration = _data.Duration;        //제한시간
        float finalRushDuration = _data.FinalRushDuration;      //마무리 구간

        //유효한 값인지 확인
        if ( float.IsNaN ( duration ) == true ||
            float.IsInfinity ( duration ) == true ||
             float.IsNaN ( finalRushDuration ) == true ||
             float.IsInfinity ( finalRushDuration ) == true )
        {
            Debug.LogError (
                "[게임 초기화 실패] 제한시간 설정을 확인해 주세요." , this );
            return false;
        }

        return true;
    }

    #endregion

    private void OnDisable ( )
    {
        //씬 정리는 정상 종료 결과로 기록하지 않음
        _isUpdating = false;
        _missions?.End ( );
    }

}
