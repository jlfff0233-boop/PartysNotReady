using UnityEngine;

/// <summary>
/// 게임 진행 상태
/// </summary>
public enum GameFlowState
{
    Ready,
    Playing,
    FinalRush,
    Ended
}

/// <summary>
/// 게임 종료 사유
/// </summary>
public enum GameEndReason
{
    None,
    TimeExpired,
    AllNpcDeparted
}

/// <summary>
/// 게임 진행 모델 - 시간, 종료 상태와 승패 판정
/// </summary>
public class GameFlowModel
{
    float _duration;        //제한 시간
    float _finalRushDuration;       //마무리 구간
    float _remainingTime;       //남은 시간

    GameFlowState _state = GameFlowState.Ready;       //게임 진행 상태
    GameEndReason _endReason = GameEndReason.None;      //종료 사유

    /// <summary>
    /// 진행 상태
    /// </summary>
    public GameFlowState State => _state;
    /// <summary>
    /// 종료 이유
    /// </summary>
    public GameEndReason EndReason => _endReason;
    /// <summary>
    /// 남은 시간
    /// </summary>
    public float RemainingTime => _remainingTime;
    /// <summary>
    /// 경과 시간
    /// </summary>
    public float ElapsedTime => _duration - _remainingTime;

    /// <summary>
    /// 게임 진행 중 여부
    /// </summary>
    public bool IsRunning =>
        _state == GameFlowState.Playing ||
        _state == GameFlowState.FinalRush;

    /// <summary>
    /// 진행 초기화
    /// </summary>
    /// <param name="duration">전체 제한시간</param>
    /// <param name="finalRushDuration">종료 전 마무리 시간</param>
    public GameFlowModel ( float duration , float finalRushDuration )
    {
        _duration = Mathf.Max ( 1f , duration );        //제한시간 설정
        _finalRushDuration = Mathf.Clamp ( finalRushDuration , 0f , _duration );        //마무리 구간 시간 설정

        _remainingTime = _duration;
    }


    /// <summary>
    /// 게임 시작
    /// </summary>
    /// <returns>이번 호출로 시작했는지 여부</returns>
    public bool TryStart ( )
    {
        if ( _state != GameFlowState.Ready )     //준비되지 않은 상태면 종료
            return false;

        //남은 시간에 따라 진행 상태 설정
        _state = _finalRushDuration > 0f &&
                 _remainingTime <= _finalRushDuration
            ? GameFlowState.FinalRush
            : GameFlowState.Playing;

        return true;
    }

    /// <summary>
    /// 시간과 NPC 잔존 수를 기준으로 게임 진행 갱신
    /// </summary>
    /// <param name="deltaTime">경과 시간</param>
    /// <param name="remainingNpcCount">잔존 NPC 수(포획 중 포함)</param>
    public void Update ( float deltaTime , int remainingNpcCount )
    {
        if ( IsRunning == false )
            return;

        //같은 갱신에서 두 조건을 만족하면 전멸 사유를 우선
        if ( remainingNpcCount <= 0 )
        {
            TryEnd ( GameEndReason.AllNpcDeparted );
            return;
        }

        _remainingTime = Mathf.Max (
            0f , _remainingTime - Mathf.Max ( 0f , deltaTime ) );

        if ( _remainingTime <= 0f )
        {
            TryEnd ( GameEndReason.TimeExpired );
            return;
        }

        if ( _state == GameFlowState.Playing &&
             _finalRushDuration > 0f &&
             _remainingTime <= _finalRushDuration )
        {
            _state = GameFlowState.FinalRush;
        }
    }


    /// <summary>
    /// 종료 사유와 평가 등급으로 승패 판정
    /// </summary>
    /// <param name="reason">게임 종료 사유</param>
    /// <param name="isMissionCompleted">필수 미션 완료 여부</param>
    /// <param name="scores">평가 점수</param>
    /// <param name="grade">평가 등급</param>
    /// <returns>게임 종료 판정</returns>
    public GameOutcome DetermineOutcome (
        GameEndReason reason , bool isMissionCompleted ,
        GameScoreResult [ ] scores , string grade )
    {
        //손님 전원 이탈은 점수와 관계없이 실패
        if ( reason == GameEndReason.AllNpcDeparted )
            return GameOutcome.Failure;

        if ( scores == null || string.IsNullOrWhiteSpace ( grade ) )
            return GameOutcome.Unscored;

        bool meetsGrade = grade == "A" || grade == "B";
        return reason == GameEndReason.TimeExpired &&
               isMissionCompleted && meetsGrade
            ? GameOutcome.Success
            : GameOutcome.Failure;
    }

    /// <summary>
    /// 진행 중인 게임 종료를 한 번만 확정
    /// </summary>
    /// <param name="reason">종료 사유</param>
    /// <returns>이번 호출로 종료했는지 여부</returns>
    public bool TryEnd ( GameEndReason reason )
    {
        if ( IsRunning == false || reason == GameEndReason.None )
            return false;

        _state = GameFlowState.Ended;
        _endReason = reason;
        return true;
    }

}
