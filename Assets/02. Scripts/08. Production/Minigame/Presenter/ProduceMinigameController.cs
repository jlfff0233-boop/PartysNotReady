using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 제작 미니게임 제어 - 입력, 진행과 판정 연결
/// </summary>
public class ProduceMinigameController
{
    ProduceMinigameData _data;		//미니게임 데이터
    bool _handling;		//입력/콜백 재진입 차단
    ProductionSession _session;		//제작 세션
    ProduceMinigameModel _model;		//미니게임 판정 모델

    /// <summary>
    /// 미니게임 데이터 준비 여부
    /// </summary>
    public bool IsReady => _model != null && _model.IsReady;

    /// <summary>
    /// 입력 가능 여부
    /// </summary>
    public bool CanPlayMinigame =>
        _handling == false && _session.CanRunMinigame &&
        _model != null && _model.IsRunning;

    /// <summary>
    /// 현재 미니게임 종류
    /// </summary>
    public ProduceMinigameType MinigameType => _data == null ? ProduceMinigameType.Timing : _data.MinigameType;

    /// <summary>
    /// 타이밍 진행 위치
    /// </summary>
    public float Progress => _model == null ? 0f : _model.Progress;

    /// <summary>
    /// 회차 제한시간 잔여 비율
    /// </summary>
    public float RemainingTimeRatio => _model == null ? 0f : _model.RemainingTimeRatio;

    /// <summary>
    /// 타이밍 성공 구간 시작
    /// </summary>
    public float RangeStart => _model == null ? 0f : _model.RangeStart;

    /// <summary>
    /// 타이밍 성공 구간 끝
    /// </summary>
    public float RangeEnd => _model == null ? 0f : _model.RangeEnd;

    /// <summary>
    /// 미니게임 시도 ID
    /// </summary>
    public Guid AttemptId => _model == null ? Guid.Empty : _model.AttemptId;

    /// <summary>
    /// 클릭 목표 ID
    /// </summary>
    public IEnumerable<int> TargetIds =>
        _model == null ? Array.Empty<int>( ) : _model.TargetIds;

    /// <summary>
    /// 미니게임 진행률 변경 이벤트(진행률)
    /// </summary>
    public event Action<float> OnProgressChanged;

    /// <summary>
    /// 회차 남은 시간 변경 이벤트(잔여 비율)
    /// </summary>
    public event Action<float> OnTimeChanged;

    /// <summary>
    /// 제작 성공 횟수 변경 이벤트(성공 횟수, 필요 횟수)
    /// </summary>
    public event Action<int, int> OnSuccessChanged;

    /// <summary>
    /// 제작 미니게임 결과 이벤트(미니게임 결과)
    /// </summary>
    public event Action<ProduceMinigameResult> OnResult;


    /// <summary>
    /// 제작 세션 연결
    /// </summary>
    /// <param name="session">제작 규칙과 소비를 담당할 세션</param>
    public ProduceMinigameController ( ProductionSession session )
    {
        _session = session;
    }

    /// <summary>
    /// 미니게임 설정
    /// </summary>
    /// <param name="data">미니게임 데이터</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>검증한 새 모델 연결 여부</returns>
    public bool TryConfigure ( ProduceMinigameData data, out string reason )
    {
        ProduceMinigameModel model = new(
            data.AttemptDuration, data.HandleTravelDuration,
            data.SuccessRangeStart, data.SuccessRangeEnd,
            data.MinigameType, data.HitPointSpawnInterval, data.HitPointLifetime );

        reason = model.FailureReason;

        if ( model.IsReady == false )
            return false;

        //이전 레시피의 시도 정리
        Stop( );
        _model = model;
        _data = data;

        return true;
    }


    /// <summary>
    /// 제작 완화 효과 적용
    /// </summary>
    /// <param name="multiplier">검증한 효과 배율</param>
    public void SetDifficultyMultiplier ( float multiplier )
    {
        _model.SetDifficultyMultiplier( multiplier );
    }

    /// <summary>
    /// 제작 효과 복원
    /// </summary>
    public void ClearDifficultyEffect ()
    {
        if ( _model != null )
            _model.ClearDifficultyEffect( );
    }


    #region ----- 제작 미니게임 -----

    /// <summary>
    /// 미니게임 회차 시작
    /// </summary>
    public void BeginRound ()
    {
        _model.TryBeginRound( );
    }

    /// <summary>
    /// 미니게임 진행
    /// </summary>
    /// <param name="deltaTime">경과시간</param>
    public void Tick ( float deltaTime )
    {
        if ( CanPlayMinigame == false )
            return;

        _handling = true;

        try
        {
            ProduceMinigameResult result = _model.Tick( deltaTime );

            if ( result == ProduceMinigameResult.Failed )
            {
                Resolve( result, out _ );
            }
            else
                NotifyProgress( );
        }
        finally
        {
            _handling = false;
        }
    }

    /// <summary>
    /// 타이밍 입력 판정
    /// </summary>
    /// <param name="result">제작 반영 결과</param>
    /// <returns>판정 반영 여부</returns>
    public bool TryCheckTiming ( out ProduceMinigameResult result )
    {
        result = ProduceMinigameResult.Unavailable;

        if ( CanPlayMinigame == false ||
            MinigameType != ProduceMinigameType.Timing )
            return false;

        _handling = true;

        try
        {
            //성공 시 진행도 증가
            ProduceMinigameResult checkedResult = _model.CheckProgress( );
            return Resolve( checkedResult, out result );
        }
        finally
        {
            _handling = false;
        }
    }

    /// <summary>
    /// 클릭 목표 입력 판정
    /// </summary>
    /// <param name="attemptId">시도 ID</param>
    /// <param name="targetId">입력한 목표 ID</param>
    /// <param name="result">미니게임 판정 결과</param>
    /// <returns>유효 입력 반영 여부</returns>
    public bool TryCheckTarget (
        Guid attemptId, int targetId,
        out ProduceMinigameResult result )
    {
        result = ProduceMinigameResult.Unavailable;

        if ( CanPlayMinigame == false ||
            MinigameType != ProduceMinigameType.ClickArea )
            return false;

        _handling = true;

        try
        {
            //입력 성공 시 진행도 반영
            ProduceMinigameResult checkedResult = _model.CheckTarget( attemptId, targetId );
            return Resolve( checkedResult, out result );
        }
        finally
        {
            _handling = false;
        }
    }

    /// <summary>
    /// 빈 영역 입력 판정
    /// </summary>
    /// <param name="attemptId">표시된 시도 ID</param>
    /// <param name="result">제작 반영 결과</param>
    /// <returns>실패 판정 반영 여부</returns>
    public bool TryCheckMiss (
        Guid attemptId, out ProduceMinigameResult result )
    {
        result = ProduceMinigameResult.Unavailable;

        if ( CanPlayMinigame == false ||
            MinigameType != ProduceMinigameType.ClickArea )
            return false;

        _handling = true;

        try
        {
            ProduceMinigameResult checkedResult = _model.CheckMiss( attemptId );
            return Resolve( checkedResult, out result );
        }
        finally
        {
            _handling = false;
        }
    }

    /// <summary>
    /// 미니게임 중단
    /// </summary>
    public void Stop ()
    {
        //레시피 선택 전에는 종료할 시도가 없음
        if ( _model == null )
            return;

        _model.Reset( );
    }

    /// <summary>
    /// 미니게임 판정 반영
    /// </summary>
    /// <param name="checkedResult">입력 또는 시간 판정</param>
    /// <param name="result">실제 반영 결과</param>
    /// <returns>제작 반영 성공 여부</returns>
    bool Resolve (
        ProduceMinigameResult checkedResult,
        out ProduceMinigameResult result )
    {
        result = checkedResult;

        if ( checkedResult == ProduceMinigameResult.Unavailable )
            return false;

        NotifyProgress( );

        bool applied = _session.ApplyMinigameResult(
            checkedResult, out result, out ProductionProgressResult progress );

        if ( applied && progress == ProductionProgressResult.Progressed )
        {
            //중간 성공 후 다음 회차 시간 적용
            if ( MinigameType == ProduceMinigameType.Timing )
                BeginRound( );
            else
                _model.ResetRoundTime( );

            NotifyProgress( );
        }
        else if ( applied == false &&
            _session.CanRunMinigame &&
                  ( _model.IsRunning == false ||
                  progress == ProductionProgressResult.Completed ) )
        {
            BeginRound( );
            NotifyProgress( );
        }

        if ( applied == true )
        {
            if ( result == ProduceMinigameResult.Succeeded )
                NotifySuccess( );

            //결과 알림 이벤트 발행
            OnResult?.Invoke( result );
        }
        return applied;
    }

    /// <summary>
    /// 미니게임 진행률과 남은 시간 알림
    /// </summary>
    public void NotifyProgress ()
    {
        OnProgressChanged?.Invoke( Progress );
        OnTimeChanged?.Invoke( RemainingTimeRatio );
    }

    /// <summary>
    /// 제작 성공 횟수 변경 알림
    /// </summary>
    public void NotifySuccess ()
    {
        OnSuccessChanged?.Invoke(
            _session.SuccessCount, _session.RequiredSuccessCount );
    }

    /// <summary>
    /// UI 배치 실패 목표 제거
    /// </summary>
    /// <param name="attemptId">시도 ID</param>
    /// <param name="targetId">배치 실패 목표</param>
    public void RejectDisplay ( Guid attemptId, int targetId )
    {
        _model?.RejectDisplay( attemptId, targetId );
    }

    #endregion

    #region ----- 디버그 -----

#if UNITY_EDITOR

    /// <summary>
    /// 제작 완료 디버그 실행
    /// </summary>
    public void DebugComplete ()
    {
        if ( _session.CanBegin )
            _session.TryBegin( out _ );
        if ( CanPlayMinigame == false || _session.IsForcedFailure )
            return;
        _handling = true;
        try
        {
            int remaining = _session.RequiredSuccessCount - _session.SuccessCount;
            for ( int i = 0; i < remaining; i++ )
            {
                if ( Resolve( ProduceMinigameResult.Succeeded, out _ ) == false )
                    break;
            }
        }
        finally
        {
            _handling = false;
        }
    }

    /// <summary>
    /// 제작 실패 디버그 실행
    /// </summary>
    public void DebugFail ()
    {
        if ( _session.CanBegin )
            _session.TryBegin( out _ );
        if ( CanPlayMinigame == false )
            return;
        _handling = true;
        try
        {
            Resolve( ProduceMinigameResult.Failed, out _ );
        }
        finally
        {
            _handling = false;
        }
    }

#endif

    #endregion
}
