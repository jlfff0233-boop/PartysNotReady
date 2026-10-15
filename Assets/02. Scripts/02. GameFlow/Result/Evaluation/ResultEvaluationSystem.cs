using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 결과 평가 시스템 - 대응 기록 수집과 최종 평가 확정
/// </summary>
public class ResultEvaluationSystem : MonoBehaviour
{
    [Header ( "----- 의존성 -----" )]
    [SerializeField] PreparationSystem _preparation;		//준비 상태
    [SerializeField] MoodSystem _mood;		//분위기 상태


    ResultScoreData _scoreData;		//선택한 난이도의 평가 설정
    int _remainingNpcCount;		//종료 시 잔존 NPC 수
    bool _isRecording;		//결과 입력 허용 여부
    bool _isFinalized;		//평가 확정 여부
    string _grade;		//확정 등급

    ResultEvaluationModel _model;		//안전과 대응 기록

    /// <summary>
    /// 확정 점수 항목
    /// </summary>
    GameScoreResult [ ] _scores;
    ResultScoreCalculator _scoreModel = new ( );		//평가 계산

    /// <summary>
    /// 사건 프레젠터 목록
    /// </summary>
    List<AnomalyEventSystem> _presenters = new ( );
    /// <summary>
    /// NPC 결과 구독 집합
    /// </summary>
    HashSet<Npc> _npcs = new ( );
    /// <summary>
    /// NPC 조회 목록
    /// </summary>
    List<Npc> _npcBuffer = new ( );


    /// <summary>
    /// 확정 성공 수
    /// </summary>
    public int SuccessCount => _model == null ? 0 : _model.SuccessCount;

    /// <summary>
    /// 확정 실패 수
    /// </summary>
    public int FailureCount => _model == null ? 0 : _model.FailureCount;

    /// <summary>
    /// 취소 수
    /// </summary>
    public int CancelledCount => _model == null ? 0 : _model.CancelledCount;

    /// <summary>
    /// 진행 중 종료 수
    /// </summary>
    public int EndedActiveCount =>
        _model == null ? 0 : _model.EndedActiveCount;


    #region ----- 초기화 -----

    /// <summary>
    /// 사건과 NPC 결과 입력 연결
    /// </summary>
    /// <param name="scoreData">평가 설정</param>
    /// <param name="presenters">사건 프레젠터</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init (
        ResultScoreData scoreData ,
        AnomalyEventSystem [ ] presenters )
    {
        if ( _model != null ||
             presenters == null || presenters.Length == 0 ||
             scoreData == null || scoreData.IsValid == false ||
             _preparation == null || _mood == null )
        {
            Debug.LogError ( "[결과 집계 초기화 실패] " +
                "평가 설정과 입력 연결을 확인해 주세요." , this );
            return false;
        }

        _scoreData = scoreData;

        HashSet<AnomalyEventSystem> unique = new ( );

        foreach ( AnomalyEventSystem presenter in presenters )
        {
            if ( presenter == null || unique.Add ( presenter ) == false )
            {
                Debug.LogError (
                    "[결과 집계 초기화 실패] 사건 목록의 빈 참조와 중복을 확인해 주세요." ,
                    this );
                return false;
            }
        }

        _model = new ResultEvaluationModel ( );
        _isRecording = true;

        foreach ( AnomalyEventSystem presenter in presenters )
        {
            presenter.OnStarted += HandleAnomalyStarted;
            presenter.OnResult += HandleAnomalyResult;
            _presenters.Add ( presenter );
        }

        NpcRegister.OnUnregistered += UnsubscribeNpc;
        NpcRegister.GetInitial ( _npcBuffer );

        foreach ( Npc npc in _npcBuffer )
            SubscribeNpc ( npc );

        _npcBuffer.Clear ( );
        return true;
    }

    private void OnDisable ( )
    {
        Stop ( );
    }

    /// <summary>
    /// NPC 결과 구독
    /// </summary>
    /// <param name="npc">등록된 NPC</param>
    void SubscribeNpc ( Npc npc )
    {
        if ( _npcs.Add ( npc ) == false )
            return;

        npc.OnCaptureStarted += HandleCaptureStarted;
        npc.OnCaptureResult += HandleCaptureResult;
    }

    /// <summary>
    /// NPC 결과 구독 해제
    /// </summary>
    /// <param name="npc">등록 해제된 NPC</param>
    void UnsubscribeNpc ( Npc npc )
    {
        if ( npc == null || _npcs.Remove ( npc ) == false )
            return;

        npc.OnCaptureStarted -= HandleCaptureStarted;
        npc.OnCaptureResult -= HandleCaptureResult;
    }

    /// <summary>
    /// 결과 입력 구독 해제
    /// </summary>
    public void Stop ( )
    {
        _isRecording = false;

        NpcRegister.OnUnregistered -= UnsubscribeNpc;

        foreach ( Npc npc in _npcs )
        {
            if ( npc == null )
                continue;

            npc.OnCaptureStarted -= HandleCaptureStarted;
            npc.OnCaptureResult -= HandleCaptureResult;
        }

        _npcs.Clear ( );

        foreach ( AnomalyEventSystem presenter in _presenters )
        {
            if ( presenter == null )
                continue;

            presenter.OnStarted -= HandleAnomalyStarted;
            presenter.OnResult -= HandleAnomalyResult;
        }

        _presenters.Clear ( );
    }

    #endregion

    #region ----- 핸들 -----

    /// <summary>
    /// 사건 시작 등록
    /// </summary>
    /// <param name="occurrence">발생한 사건</param>
    void HandleAnomalyStarted ( AnomalyEvent occurrence )
    {
        if ( _isRecording == true )
            _model.TryBegin ( occurrence );
    }

    /// <summary>
    /// 사건 결과 확정
    /// </summary>
    /// <param name="occurrence">발생한 사건</param>
    /// <param name="status">사건 결과</param>
    void HandleAnomalyResult (
        AnomalyEvent occurrence , AnomalyStatus status )
    {
        if ( _isRecording == false )
            return;

        switch ( status )
        {
            case AnomalyStatus.Resolved:
                _model.TryComplete ( occurrence , true );
                break;

            case AnomalyStatus.Failed:
                _model.TryComplete ( occurrence , false );
                break;

            case AnomalyStatus.Cancelled:
                _model.TryCancel ( occurrence );
                break;
        }

        //Recovered와 Expired는 기존 확정 결과를 바꾸지 않음
    }

    /// <summary>
    /// NPC 포획 시작 등록
    /// </summary>
    /// <param name="npc">포획된 NPC</param>
    /// <param name="number">포획 번호</param>
    void HandleCaptureStarted ( Npc npc , int number )
    {
        if ( _isRecording == true && npc != null )
            _model.TryBegin ( (npc, number) );
    }

    /// <summary>
    /// NPC 포획 결과 확정
    /// </summary>
    /// <param name="npc">포획된 NPC</param>
    /// <param name="number">포획 번호</param>
    /// <param name="result">포획 결과</param>
    void HandleCaptureResult (
        Npc npc , int number , CaptureResult result )
    {
        if ( _isRecording == false || npc == null )
            return;

        object key = (npc, number);

        switch ( result )
        {
            case CaptureResult.Rescued:
                _model.TryComplete ( key , true );
                break;

            case CaptureResult.Kidnapped:
                _model.TryComplete ( key , false );
                break;

            case CaptureResult.Cancelled:
                _model.TryCancel ( key );
                break;
        }
    }


    #endregion

    #region ----- 평가 -----

    /// <summary>
    /// 종료 평가 확정
    /// </summary>
    /// <param name="elapsedTime">종료 경과 시간</param>
    public void FinalizeAt ( float elapsedTime )
    {
        if ( _isRecording == false )
            return;

        //새 결과를 차단한 뒤 종료 시간까지 분위기 반영
        _isRecording = false;
        _mood.FinalizeAt ( elapsedTime );

        //씬 정리 전에 실제 준비 상태와 NPC 수 확인
        bool hasPreparation = _preparation.TryGetPreparationScore (
            out float preparation );
        //평가 확정 전에는 시작 NPC의 잔존 수로 집계
        _remainingNpcCount = NpcRegister.InitialRemainingCount;
        _model.Finish ( );

        bool hasSafety = TryGetSafetyScore ( out float safety );
        bool hasPredator = TryGetPredatorScore ( out float predator );

        _isFinalized = true;

        //필수 입력 누락은 평가 제외가 아니라 미정산
        if ( hasPreparation == false || hasSafety == false )
        {
            Debug.LogError ( "[결과 평가 실패] 준비 또는 NPC 입력이 없습니다." , this );
            return;
        }

        float mood = _mood.Average * _scoreData.MoodAverageRatio +
            _mood.Current * ( 1f - _scoreData.MoodAverageRatio );

        float [ ] values = { preparation , safety , mood , predator };
        bool [ ] included = { true , true , true , hasPredator };

        if ( _scoreModel.TryCalculate (
            _scoreData , values , included , out _scores , out _grade ) == false )
            Debug.LogError ( "[결과 평가 실패] 가중치 설정을 확인해 주세요." , this );
    }

    /// <summary>
    /// 확정된 평가 결과 조회
    /// </summary>
    /// <param name="scores">확정 점수 항목</param>
    /// <param name="grade">확정 등급</param>
    /// <returns>정산 완료 여부</returns>
    public bool TryGetFinalScore (
        out GameScoreResult [ ] scores , out string grade )
    {
        scores = _scores;
        grade = _grade;
        return _isFinalized == true && _scores != null;
    }

    /// <summary>
    /// 안전 점수 조회
    /// </summary>
    /// <param name="score">0~100 안전 점수</param>
    /// <returns>계산 가능 여부</returns>
    public bool TryGetSafetyScore ( out float score )
    {
        score = 0f;

        return _model != null &&
            _model.TryGetSafetyScore (
                NpcRegister.InitialCount , _remainingNpcCount , out score );
    }

    /// <summary>
    /// 확정 대응 점수 조회
    /// </summary>
    /// <param name="score">0~100 대응 점수</param>
    /// <returns>확정 결과 존재 여부</returns>
    public bool TryGetPredatorScore ( out float score )
    {
        score = 0f;
        return _model != null &&
            _model.TryGetPredatorScore ( out score );
    }
    #endregion
}
