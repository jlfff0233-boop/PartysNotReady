using System.Collections.Generic;
using System;
using UnityEngine;

/// <summary>
/// 분위기 시스템 - 게임 시간, 미션, 사건과 NPC 결과 반영
/// </summary>
public class MoodSystem : MonoBehaviour
{
    GameFlowController _flow;		//게임 시간과 미션 진행

    MoodData _data;		//선택한 난이도의 분위기 설정
    MoodModel _model;		//현재 분위기와 평균
    bool _isRunning;		//게임 진행 중 여부

    /// <summary>
    /// 사건 프레젠터 목록
    /// </summary>
    List<AnomalyEventSystem> _anomalyPresenters = new ( );
    /// <summary>
    /// NPC 결과 구독 집합
    /// </summary>
    HashSet<Npc> _npcs = new ( );
    /// <summary>
    /// 초기 NPC 조회 목록
    /// </summary>
    List<Npc> _initialNpcs = new ( );

    /// <summary>
    /// 현재 분위기
    /// </summary>
    public float Current => _model == null ? 0f : _model.Current;

    /// <summary>
    /// 게임 종료 시점까지 반영한 평균 분위기
    /// </summary>
    public float Average => _model == null ? 0f : _model.Average;

    /// <summary>
    /// 분위기 변경 이벤트(현재 분위기)
    /// </summary>
    public event Action<float> OnMoodChanged;

    /// <summary>
    /// 게임 시간과 결과 알림 연결
    /// </summary>
    /// <param name="flow">게임 흐름 중재자</param>
    /// <param name="data">분위기 설정</param>
    /// <param name="presenters">씬의 사건 프레젠터</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init (
        GameFlowController flow , MoodData data ,
        AnomalyEventSystem [ ] presenters )
    {
        if ( _model != null )
        {
            Debug.LogError ( "[분위기 초기화 실패] 이미 초기화되었습니다." , this );
            return false;
        }

        if ( flow == null || data == null ||
             presenters == null || presenters.Length == 0 )
        {
            Debug.LogError (
                "[분위기 초기화 실패] " +
                "게임 진행, 설정과 사건 목록을 확인해 주세요." , this );
            return false;
        }

        _flow = flow;
        _data = data;

        if ( float.IsNaN ( _data.DecreaseDelay ) ||
             float.IsInfinity ( _data.DecreaseDelay ) ||
             float.IsNaN ( _data.Decrease ) ||
             float.IsInfinity ( _data.Decrease ) )
        {
            Debug.LogError (
                "[분위기 초기화 실패] 자연 감소 설정을 확인해 주세요." , this );
            return false;
        }

        //구독 전에 빈 참조와 중복 설정 확인
        HashSet<AnomalyEventSystem> unique = new ( );

        foreach ( AnomalyEventSystem presenter in presenters )
        {
            if ( presenter == null || unique.Add ( presenter ) == false )
            {
                Debug.LogError (
                    "[분위기 초기화 실패] " +
                    "사건 목록의 빈 참조 또는 중복을 확인해 주세요." , this );
                return false;
            }
        }

        _model = new MoodModel ( _data.InitialMood );

        foreach ( AnomalyEventSystem presenter in unique )
        {
            presenter.OnResult += HandleAnomalyResult;
            _anomalyPresenters.Add ( presenter );
        }

        NpcRegister.OnUnregistered += UnsubscribeNpc;
        NpcRegister.GetInitial ( _initialNpcs );

        foreach ( Npc npc in _initialNpcs )
            SubscribeNpc ( npc );

        _initialNpcs.Clear ( );

        _flow.OnElapsedTimeChanged += HandleElapsedTime;
        _flow.OnMissionProgressed += HandleMissionProgressed;
        _isRunning = true;
        return true;
    }

    private void OnDisable ( )
    {
        Stop ( );
    }


    /// <summary>
    /// 분위기가 바뀌었으면 현재 값 알림
    /// </summary>
    /// <param name="previous">변경 전 분위기</param>
    void NotifyMoodChanged ( float previous )
    {
        if ( _model.Current != previous )
            OnMoodChanged?.Invoke ( _model.Current );
    }


    /// <summary>
    /// 시간 변화에 따른 분위기 갱신
    /// </summary>
    /// <param name="elapsedTime">현재 게임 경과 시간</param>
    void HandleElapsedTime ( float elapsedTime )
    {
        if ( _isRunning == false )
            return;

        float previous = _model.Current;
        _model.Advance (
            elapsedTime , _data.DecreaseDelay , _data.Decrease );
        NotifyMoodChanged ( previous );
    }

    /// <summary>
    /// 미션 진행을 분위기에 반영
    /// </summary>
    /// <param name="addedAmount">실제 증가량</param>
    /// <param name="completed">전체 미션 완료 여부</param>
    void HandleMissionProgressed ( int addedAmount , bool completed )
    {
        if ( _isRunning == false || addedAmount <= 0 )
            return;

        float elapsedTime = _flow.ElapsedTime;
        HandleElapsedTime ( elapsedTime );
        float previous = _model.Current;
        _model.RecordMissionProgress (
            elapsedTime , addedAmount , _data.MissionRecovery , completed );
        NotifyMoodChanged ( previous );
    }

    /// <summary>
    /// 종료 시간까지 분위기 반영
    /// </summary>
    /// <param name="elapsedTime">확정된 게임 경과 시간</param>
    public void FinalizeAt ( float elapsedTime )
    {
        HandleElapsedTime ( elapsedTime );
        _isRunning = false;
    }

    /// <summary>
    /// 사건/NPC의 확정된 변화량 반영
    /// </summary>
    /// <param name="amount">분위기 변화량</param>
    public void ApplyChange ( float amount )
    {
        if ( _isRunning == false ||
             float.IsNaN ( amount ) || float.IsInfinity ( amount ) )
            return;

        HandleElapsedTime ( _flow.ElapsedTime );
        float previous = _model.Current;
        _model.ApplyChange ( amount );
        NotifyMoodChanged ( previous );
    }


    /// <summary>
    /// NPC 포획 결과 구독
    /// </summary>
    /// <param name="npc">등록된 NPC</param>
    void SubscribeNpc ( Npc npc )
    {
        if ( _npcs.Add ( npc ) == false )
            return;

        npc.OnCaptureResult += HandleCaptureResult;
    }

    /// <summary>
    /// NPC 포획 결과 구독 해제
    /// </summary>
    /// <param name="npc">등록 해제된 NPC</param>
    void UnsubscribeNpc ( Npc npc )
    {
        if ( npc == null || _npcs.Remove ( npc ) == false )
            return;

        npc.OnCaptureResult -= HandleCaptureResult;
    }

    /// <summary>
    /// 사건 결과 반영
    /// </summary>
    /// <param name="occurrence">발생한 사건</param>
    /// <param name="status">사건 결과</param>
    void HandleAnomalyResult (
        AnomalyEvent occurrence , AnomalyStatus status )
    {
        if ( CanRecordResult ( ) == false ||
             ( status != AnomalyStatus.Resolved &&
               status != AnomalyStatus.Failed ) )
            return;

        HandleElapsedTime ( _flow.ElapsedTime );
        float previous = _model.Current;
        _model.TryRecordAnomaly (
            occurrence , status ,
            _data.ResolvedRecovery , _data.FailedDecrease );
        NotifyMoodChanged ( previous );
    }

    /// <summary>
    /// NPC 포획 결과 반영
    /// </summary>
    /// <param name="npc">포획된 NPC</param>
    /// <param name="round">포획 회차</param>
    /// <param name="result">포획 결과</param>
    void HandleCaptureResult (
        Npc npc , int round , CaptureResult result )
    {
        if ( CanRecordResult ( ) == false ||
             ( result != CaptureResult.Rescued &&
               result != CaptureResult.Kidnapped ) )
            return;

        HandleElapsedTime ( _flow.ElapsedTime );
        float previous = _model.Current;
        _model.TryRecordCapture (
            npc , round , result ,
            _data.RescueRecovery , _data.DepartureDecrease );
        NotifyMoodChanged ( previous );
    }

    /// <summary>
    /// 결과, 시간 알림 구독 해제
    /// </summary>
    public void Stop ( )
    {
        _isRunning = false;

        if ( _flow != null )
        {
            _flow.OnElapsedTimeChanged -= HandleElapsedTime;
            _flow.OnMissionProgressed -= HandleMissionProgressed;
        }

        NpcRegister.OnUnregistered -= UnsubscribeNpc;

        foreach ( Npc npc in _npcs )
        {
            if ( npc != null )
                npc.OnCaptureResult -= HandleCaptureResult;
        }

        _npcs.Clear ( );

        foreach ( AnomalyEventSystem presenter in _anomalyPresenters )
        {
            if ( presenter != null )
                presenter.OnResult -= HandleAnomalyResult;
        }

        _anomalyPresenters.Clear ( );
    }


    /// <summary>
    /// 게임 진행 중인지 확인
    /// </summary>
    /// <returns>결과 반영 가능 여부</returns>
    bool CanRecordResult ( )
    {
        return _isRunning == true &&
            ( _flow.State == GameFlowState.Playing ||
              _flow.State == GameFlowState.FinalRush );
    }

}
