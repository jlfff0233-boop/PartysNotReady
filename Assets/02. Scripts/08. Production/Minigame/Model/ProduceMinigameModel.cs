
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 제작 미니게임 모델 - 회차 진행과 성공 구간 판정
/// </summary>
public class ProduceMinigameModel
{
    float _actionDuration;		//회차 제한시간
    float _handleTravelDuration;		//핸들 편도 이동시간
    float _handlePosition;		//핸들 위치, 0~1
    int _handleDirection = 1;		//1: 오른쪽, -1: 왼쪽

    float _successRangeStart;		//성공 구간 시작
    float _successRangeEnd;		//성공 구간 종료

    float _baseRangeStart;		//설정 성공 구간 시작
    float _baseRangeEnd;		//설정 성공 구간 끝


    ProduceMinigameType _type;		//미니게임 종류
    float _spawnInterval;		//목표 출현 간격
    float _targetLifetime;		//목표 수명
    float _baseTargetLifetime;		//설정 목표 유지시간

    float _nextSpawnTime;		//다음 목표 출현 시각
    int _nextTargetId;		//다음 목표 ID
    Guid _attemptId;		//미니게임 시도 ID

    float _elapsedTime;		//현재 회차 경과시간
    float _targetElapsedTime;		//클릭 목표 경과시간
    bool _isRunning;		//회차 진행 여부
    bool _isReady;		//설정 유효 여부

    /// <summary>
    /// 클릭 목표 딕셔너리(목표 ID, 만료 시각)
    /// </summary>
    readonly Dictionary<int, float> _targets = new( );

    /// <summary>
    /// 만료 목표 목록(목표 ID)
    /// </summary>
    readonly List<int> _expired = new( );

    string _failureReason = string.Empty;		//초기화 실패 사유

    /// <summary>
    /// 적용 성공 구간 시작
    /// </summary>
    public float RangeStart => _successRangeStart;

    /// <summary>
    /// 적용 성공 구간 끝
    /// </summary>
    public float RangeEnd => _successRangeEnd;

    /// <summary>
    /// 현재 미니게임 시도 ID
    /// </summary>
    public Guid AttemptId => _attemptId;

    /// <summary>
    /// 클릭 목표 ID
    /// </summary>
    public IEnumerable<int> TargetIds => _targets.Keys;

    /// <summary>
    /// 초기화 완료 여부
    /// </summary>
    public bool IsReady => _isReady;

    /// <summary>
    /// 초기화 실패 사유
    /// </summary>
    public string FailureReason => _failureReason;

    /// <summary>
    /// 현재 회차 진행 여부
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// 현재 핸들 위치
    /// </summary>
    public float Progress => _isReady ? _handlePosition : 0f;

    /// <summary>
    /// 회차 제한시간 잔여 비율
    /// </summary>
    public float RemainingTimeRatio =>
        _isReady
            ? Mathf.Clamp01( 1f - _elapsedTime / _actionDuration )
            : 0f;


    /// <summary>
    /// 제작 미니게임 판정 설정 생성
    /// </summary>
    /// <param name="actionDuration">회차 제한시간</param>
    /// <param name="handleTravelDuration">핸들 이동 시간</param>
    /// <param name="successRangeStart">성공 구간 시작</param>
    /// <param name="successRangeEnd">성공 구간 종료</param>
    /// <param name="type">제작 미니게임 종류</param>
    /// <param name="spawnInterval">목표 출현 간격</param>
    /// <param name="targetLifetime">목표 유지시간</param>
    public ProduceMinigameModel (
        float actionDuration, float handleTravelDuration,
        float successRangeStart, float successRangeEnd,
        ProduceMinigameType type = ProduceMinigameType.Timing,
        float spawnInterval = 0.5f, float targetLifetime = 1.5f )
    {
        //생성 시에도 사전 검사와 같은 규칙 적용
        if ( CheckSettings(
            actionDuration, handleTravelDuration,
            successRangeStart, successRangeEnd,
            spawnInterval, targetLifetime, out _failureReason ) == false )
            return;

        _type = type;
        _spawnInterval = spawnInterval;
        _baseTargetLifetime = _targetLifetime = targetLifetime;

        _actionDuration = actionDuration;
        _handleTravelDuration = handleTravelDuration;

        _baseRangeStart = _successRangeStart = successRangeStart;
        _baseRangeEnd = _successRangeEnd = successRangeEnd;

        _isReady = true;
    }


    /// <summary>
    /// 제작 효과 복원
    /// </summary>
    public void ClearDifficultyEffect ()
    {
        _successRangeStart = _baseRangeStart;
        _successRangeEnd = _baseRangeEnd;
        _targetLifetime = _baseTargetLifetime;
    }

    /// <summary>
    /// 제작 유형별 완화 효과 적용
    /// </summary>
    /// <param name="multiplier">검증한 효과 배율</param>
    public void SetDifficultyMultiplier ( float multiplier )
    {
        //기본값 기준으로 적용하여 배율 누적 방지
        ClearDifficultyEffect( );

        if ( _type == ProduceMinigameType.ClickArea )
        {
            _targetLifetime = _baseTargetLifetime * multiplier;
            return;
        }

        float center = ( _baseRangeStart + _baseRangeEnd ) * 0.5f;		//구간 중심
        float halfWidth = ( _baseRangeEnd - _baseRangeStart ) * 0.5f * multiplier;		//구간 반폭
        _successRangeStart = Mathf.Clamp01( center - halfWidth );
        _successRangeEnd = Mathf.Clamp01( center + halfWidth );
    }

    #region ----- 미니게임 처리 -----

    /// <summary>
    /// 회차 제한시간 초기화
    /// </summary>
    public void ResetRoundTime ()
    {
        if ( _isReady == false || _isRunning == false )
            return;

        _elapsedTime = 0f;
    }

    /// <summary>
    /// 값 초기화 및 새 회차 시작
    /// </summary>
    /// <returns>회차 시작 여부</returns>
    public bool TryBeginRound ()
    {
        if ( _isReady == false )
            return false;

        _elapsedTime = 0f;
        _handlePosition = 0f;
        _handleDirection = 1;

        _targetElapsedTime = 0f;
        _targets.Clear( );
        _attemptId = Guid.NewGuid( );

        _nextTargetId = 0;
        _nextSpawnTime = _spawnInterval;

        _isRunning = true;

        if ( _type == ProduceMinigameType.ClickArea )
            SpawnTarget( );

        return true;
    }

    /// <summary>
    /// 회차 진행
    /// </summary>
    /// <param name="deltaTime">반영할 경과시간</param>
    /// <returns>진행 중/제한시간 만료 판정</returns>
    public ProduceMinigameResult Tick ( float deltaTime )
    {
        if ( _isReady == false ||
            _isRunning == false ||
             float.IsNaN( deltaTime ) ||
             float.IsInfinity( deltaTime ) ||
             deltaTime < 0f )
            return ProduceMinigameResult.Unavailable;

        float remaining = _actionDuration - _elapsedTime;		//남은 시간
        float advancedTime = Mathf.Min( deltaTime, remaining );     //진행 시간

        _elapsedTime += advancedTime;

        //목표 생성과 만료 시간은 회차 초기화와 별도로 진행
        if ( _type == ProduceMinigameType.ClickArea )
            _targetElapsedTime += advancedTime;

        float distance = advancedTime / _handleTravelDuration;
        float unfolded = _handleDirection == 1
            ? _handlePosition + distance
            : 2f - _handlePosition + distance;

        float phase = Mathf.Repeat( unfolded, 2f );

        _handlePosition = phase <= 1f ? phase : 2f - phase;
        _handleDirection = phase < 1f ? 1 : -1;

        if ( _elapsedTime < _actionDuration )
        {
            if ( _type == ProduceMinigameType.ClickArea )
                TickTargets( );

            return ProduceMinigameResult.InProgress;
        }

        _isRunning = false;
        return ProduceMinigameResult.Failed;
    }

    /// <summary>
    /// 상태 초기화
    /// </summary>
    public void Reset ()
    {
        _elapsedTime = 0f;
        _targetElapsedTime = 0f;
        _handlePosition = 0f;
        _handleDirection = 1;
        _isRunning = false;
    }

    /// <summary>
    /// 현재 진행 위치 판정
    /// </summary>
    /// <returns>입력 판정 결과</returns>
    public ProduceMinigameResult CheckProgress ()
    {
        if ( _isReady == false || _isRunning == false )
            return ProduceMinigameResult.Unavailable;

        _isRunning = false;

        if ( Progress >= _successRangeStart &&
             Progress <= _successRangeEnd )
        {
            return ProduceMinigameResult.Succeeded;
        }

        return ProduceMinigameResult.Failed;
    }

    #endregion

    #region ----- 클릭 목표 -----

    /// <summary>
    /// 클릭 목표 갱신
    /// </summary>
    void TickTargets ()
    {
        //만료한 대상 목록 정리
        _expired.Clear( );

        foreach ( KeyValuePair<int, float> target in _targets )
        {
            //만료 시 만료 목록에 추가
            if ( target.Value <= _targetElapsedTime )
                _expired.Add( target.Key );
        }

        //딕셔너리에서 만료 대상 제거
        foreach ( int id in _expired )
            _targets.Remove( id );

        //목록 정리
        _expired.Clear( );

        if ( _targetElapsedTime >= _nextSpawnTime )
        {
            //일정 간격으로 대상 스폰
            _nextSpawnTime += _spawnInterval;
            SpawnTarget( );
        }
    }

    /// <summary>
    /// 클릭 목표 입력 판정
    /// </summary>
    /// <param name="attemptId">입력 시도 ID</param>
    /// <param name="targetId">입력한 목표</param>
    /// <returns>목표 판정 결과</returns>
    public ProduceMinigameResult CheckTarget ( Guid attemptId, int targetId )
    {
        if ( IsTargetActive( attemptId, targetId ) == false )
            return ProduceMinigameResult.Unavailable;

        _targets.Remove( targetId );

        return ProduceMinigameResult.Succeeded;
    }

    /// <summary>
    /// 빈 영역 입력 판정
    /// </summary>
    /// <param name="attemptId">입력 시도 ID</param>
    /// <returns>실패/입력 거절</returns>
    public ProduceMinigameResult CheckMiss ( Guid attemptId )
    {
        if ( _type != ProduceMinigameType.ClickArea ||
            _isRunning == false || attemptId != _attemptId )
            return ProduceMinigameResult.Unavailable;

        _isRunning = false;

        return ProduceMinigameResult.Failed;
    }

    /// <summary>
    /// UI 배치 실패 목표 제거
    /// </summary>
    /// <param name="attemptId">입력 시도 ID</param>
    /// <param name="targetId">배치 실패 목표</param>
    public void RejectDisplay ( Guid attemptId, int targetId )
    {
        if ( attemptId == _attemptId )
            _targets.Remove( targetId );
    }

    /// <summary>
    /// 클릭 목표 생성
    /// </summary>
    void SpawnTarget ()
    {
        _targets.Add( ++_nextTargetId, _targetElapsedTime + _targetLifetime );
    }

    /// <summary>
    /// 클릭 목표 유효 여부
    /// </summary>
    /// <param name="attemptId">입력 시도 ID</param>
    /// <param name="targetId">입력 목표 ID</param>
    /// <returns>아직 처리되지 않은 유효 목표 여부</returns>
    public bool IsTargetActive ( Guid attemptId, int targetId )
    {
        return _type == ProduceMinigameType.ClickArea &&
            _isRunning &&
            attemptId == _attemptId &&
            _targets.TryGetValue( targetId, out float expires ) &&
            expires > _targetElapsedTime;
    }

    #endregion

    #region ----- 검사 -----

    /// <summary>
    /// 제작 미니게임 설정 검사
    /// </summary>
    /// <param name="actionDuration">회차 제한시간</param>
    /// <param name="handleTravelDuration">핸들 편도 이동시간</param>
    /// <param name="successRangeStart">성공 구간 시작</param>
    /// <param name="successRangeEnd">성공 구간 종료</param>
    /// <param name="spawnInterval">목표 출현 간격</param>
    /// <param name="targetLifetime">목표 유지시간</param>
    /// <param name="reason">설정 오류 사유</param>
    /// <returns>설정 유효 여부</returns>
    public static bool CheckSettings (
        float actionDuration,
        float handleTravelDuration,
        float successRangeStart,
        float successRangeEnd,
        float spawnInterval, float targetLifetime,
        out string reason )
    {
        reason = string.Empty;

        if ( float.IsNaN( actionDuration ) ||
             float.IsInfinity( actionDuration ) ||
             actionDuration <= 0f )
        {
            reason = "제작 미니게임 제한시간은 0보다 커야 합니다.";
            return false;
        }

        if ( float.IsNaN( handleTravelDuration ) ||
             float.IsInfinity( handleTravelDuration ) ||
             handleTravelDuration <= 0f )
        {
            reason = "핸들 편도 이동시간은 0보다 커야 합니다.";
            return false;
        }

        if ( float.IsNaN( successRangeStart ) == true ||
             float.IsInfinity( successRangeStart ) == true ||
             float.IsNaN( successRangeEnd ) == true ||
             float.IsInfinity( successRangeEnd ) == true ||
             successRangeStart < 0f ||
             successRangeEnd > 1f ||
             successRangeStart > successRangeEnd )
        {
            reason =
                "성공 구간은 0 이상 1 이하이며 시작이 끝보다 클 수 없습니다.";
            return false;
        }

        if ( float.IsNaN( spawnInterval ) ||
             float.IsInfinity( spawnInterval ) ||
             spawnInterval <= 0f ||
             float.IsNaN( targetLifetime ) ||
             float.IsInfinity( targetLifetime ) ||
             targetLifetime <= 0f )
        {
            reason = "클릭 목표 출현과 유지시간이 유효하지 않습니다.";
            return false;
        }

        return true;
    }

    #endregion
}
