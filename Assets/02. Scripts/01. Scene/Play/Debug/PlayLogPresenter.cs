using System.Collections.Generic;

/// <summary>
/// 플레이 로그 중재 - 미션과 제작, 사건, NPC 결과 표시
/// </summary>
public class PlayLogPresenter
{
    bool _isSubscribed;		//로그 구독 여부
    int _loggedStageIndex;		//마지막 로그의 미션 단계

    GameFlowController _flowPresenter;		//로그 표시와 미션 조회
    ProductionManager _productPresenter;		//제작 결과
    AnomalyEventSystem [ ] _anomalyPresenters;		//씬 변칙 사건

    /// <summary>
    /// 로그 결과를 구독한 NPC 집합
    /// </summary>
    HashSet<Npc> _logNpcs = new( );

    /// <summary>
    /// 로그 구독을 위한 NPC 조회 목록
    /// </summary>
    List<Npc> _logNpcBuffer = new( );

    #region ----- 초기화 -----

    /// <summary>
    /// 로그 대상 연결
    /// </summary>
    /// <param name="flow">게임 진행</param>
    /// <param name="product">제작 결과</param>
    /// <param name="presenters">사건 목록</param>
    public PlayLogPresenter (
        GameFlowController flow, ProductionManager product,
        AnomalyEventSystem [ ] presenters )
    {
        _flowPresenter = flow;
        _productPresenter = product;
        _anomalyPresenters = presenters;
    }

    /// <summary>
    /// 플레이 로그 구독
    /// </summary>
    public void Begin ()
    {
        if ( _isSubscribed == true )
            return;

        _isSubscribed = true;
        _loggedStageIndex = _flowPresenter.CurrentStageIndex;
        _flowPresenter.OnMissionProgressed += HandleLogMissionProgressed;

        if ( _productPresenter != null )
        {
            _productPresenter.OnCompleted += HandleLogProductionCompleted;

            _productPresenter.OnMinigameResult += HandleLogMinigameResult;
        }

        foreach ( AnomalyEventSystem presenter in _anomalyPresenters )
        {
            presenter.OnStarted += HandleLogAnomalyStarted;
            presenter.OnResult += HandleLogAnomalyResult;
        }

        NpcRegister.OnUnregistered += UnsubscribeLogNpc;
        NpcRegister.GetInitial( _logNpcBuffer );

        foreach ( Npc npc in _logNpcBuffer )
            SubscribeLogNpc( npc );

        _logNpcBuffer.Clear( );
    }

    /// <summary>
    /// 플레이 로그 구독 해제
    /// </summary>
    public void Stop ()
    {
        _isSubscribed = false;

        if ( _flowPresenter != null )
            _flowPresenter.OnMissionProgressed -= HandleLogMissionProgressed;

        if ( _productPresenter != null )
        {
            _productPresenter.OnCompleted -= HandleLogProductionCompleted;

            _productPresenter.OnMinigameResult -= HandleLogMinigameResult;
        }

        foreach ( AnomalyEventSystem presenter in _anomalyPresenters )
        {
            if ( presenter == null )
                continue;

            presenter.OnStarted -= HandleLogAnomalyStarted;
            presenter.OnResult -= HandleLogAnomalyResult;
        }

        NpcRegister.OnUnregistered -= UnsubscribeLogNpc;

        foreach ( Npc npc in _logNpcs )
        {
            if ( npc != null )
                npc.OnCaptureResult -= HandleLogCaptureResult;
        }

        _logNpcs.Clear( );
        _logNpcBuffer.Clear( );
    }

    /// <summary>
    /// NPC 로그 구독
    /// </summary>
    /// <param name="npc">등록된 NPC</param>
    void SubscribeLogNpc ( Npc npc )
    {
        if ( _logNpcs.Add( npc ) == true )
            npc.OnCaptureResult += HandleLogCaptureResult;
    }

    /// <summary>
    /// NPC 로그 구독 해제
    /// </summary>
    /// <param name="npc">해제된 NPC</param>
    void UnsubscribeLogNpc ( Npc npc )
    {
        if ( _logNpcs.Remove( npc ) == true && npc != null )
            npc.OnCaptureResult -= HandleLogCaptureResult;
    }

    #endregion

    #region ----- 핸들 -----

    /// <summary>
    /// 미션 진행 로그
    /// </summary>
    /// <param name="amount">실제 증가량</param>
    /// <param name="isCompleted">전체 완료 여부</param>
    void HandleLogMissionProgressed ( int amount, bool isCompleted )
    {
        if ( _isSubscribed == false )
            return;

        int currentStage = _flowPresenter.CurrentStageIndex;
        string message = $"미션 진행 +{amount}";

        if ( isCompleted == true )
            message += "/모든 미션 완료";
        else if ( currentStage > _loggedStageIndex )
            message += $"/{_flowPresenter.GetStageName( _loggedStageIndex )} 완료";

        _loggedStageIndex = currentStage;
        _flowPresenter.ShowLog( message );
    }

    /// <summary>
    /// 제작 완료 로그
    /// </summary>
    /// <param name="result">완성품 결과</param>
    void HandleLogProductionCompleted ( ProductionResult result )
    {
        if ( _isSubscribed == false || result == null )
            return;

        string name = result.Data == null ? null : result.Data.ItemName;

        _flowPresenter.ShowLog( string.IsNullOrWhiteSpace( name )
            ? "제작 완료" : $"{name} 제작 완료" );
    }

    /// <summary>
    /// 제작 실패 로그
    /// </summary>
    /// <param name="result">제작 미니게임 결과</param>
    void HandleLogMinigameResult ( ProduceMinigameResult result )
    {
        if ( _isSubscribed == true && result == ProduceMinigameResult.Failed )
            _flowPresenter.ShowLog( "제작 실패" );
    }

    /// <summary>
    /// 변칙 사건 시작 로그
    /// </summary>
    /// <param name="occurrence">발생한 사건</param>
    void HandleLogAnomalyStarted ( AnomalyEvent occurrence )
    {
        if ( _isSubscribed == true )
            _flowPresenter.ShowLog( "변칙 사건 발생" );
    }

    /// <summary>
    /// 변칙 사건 결과 로그
    /// </summary>
    /// <param name="occurrence">발생한 사건</param>
    /// <param name="status">사건 상태</param>
    void HandleLogAnomalyResult (
        AnomalyEvent occurrence, AnomalyStatus status )
    {
        if ( _isSubscribed == false )
            return;

        string message = status switch
        {
            AnomalyStatus.Resolved => "변칙 사건 해결",
            AnomalyStatus.Failed => "변칙 사건 실패",
            AnomalyStatus.Recovered => "변칙 사건 복구",
            AnomalyStatus.Cancelled => "변칙 사건 취소",
            _ => null
        };

        if ( message != null )
            _flowPresenter.ShowLog( message );
    }

    /// <summary>
    /// NPC 포획 결과 로그
    /// </summary>
    /// <param name="npc">대상 NPC</param>
    /// <param name="number">포획 번호</param>
    /// <param name="result">포획 결과</param>
    void HandleLogCaptureResult (
        Npc npc, int number, CaptureResult result )
    {
        if ( _isSubscribed == false )
            return;

        string message = result switch
        {
            CaptureResult.Rescued => "손님 구조 성공",
            CaptureResult.Kidnapped => "손님 납치",
            CaptureResult.TimedOut => "손님 구조 실패",
            _ => null
        };

        if ( message != null )
            _flowPresenter.ShowLog( message );
    }

    #endregion
}
