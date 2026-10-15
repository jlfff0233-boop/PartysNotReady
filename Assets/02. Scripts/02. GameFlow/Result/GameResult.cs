
using System;
using System.Collections.Generic;

/// <summary>
/// 게임 종료 판정
/// </summary>
public enum GameOutcome
{
    Unscored,
    Success,
    Failure
}

/// <summary>
/// 게임 결과 - 종료 당시 기록 보관
/// </summary>
public class GameResult
{
    int _initialNpcCount;        //시작 손님 수
    IReadOnlyList<MissionResult> _missions;        //단계별 완료 기록
    IReadOnlyList<GameScoreResult> _scores;        //항목별 점수
    string _grade;        //확정 등급
    GameOutcome _outcome;        //게임 성공, 실패 또는 미정산
    bool _isScored;        //점수 정산 여부
    long _totalScore;        //총점
    GameEndReason _reason;        //종료 사유
    float _elapsedTime;        //경과 시간
    int _remainingNpcCount;        //잔존 NPC 수
    int _completedStageCount;        //완료한 단계 수
    bool _isMissionCompleted;        //전체 미션 완료 여부

    /// <summary>
    /// 시작 손님 수
    /// </summary>
    public int InitialNpcCount => _initialNpcCount;

    /// <summary>
    /// 단계별 완료 기록
    /// </summary>
    public IReadOnlyList<MissionResult> Missions => _missions;

    /// <summary>
    /// 항목별 점수
    /// </summary>
    public IReadOnlyList<GameScoreResult> Scores => _scores;

    /// <summary>
    /// 확정 등급
    /// </summary>
    public string Grade => _grade;

    /// <summary>
    /// 게임 성공, 실패 또는 미정산
    /// </summary>
    public GameOutcome Outcome => _outcome;

    /// <summary>
    /// 점수 정산 여부
    /// </summary>
    public bool IsScored => _isScored;

    /// <summary>
    /// 총점
    /// </summary>
    public long TotalScore => _totalScore;
    /// <summary>
    /// 종료 사유
    /// </summary>
    public GameEndReason Reason => _reason;

    /// <summary>
    /// 경과 시간
    /// </summary>
    public float ElapsedTime => _elapsedTime;

    /// <summary>
    /// 잔존 NPC 수
    /// </summary>
    public int RemainingNpcCount => _remainingNpcCount;

    /// <summary>
    /// 완료한 단계 수
    /// </summary>
    public int CompletedStageCount => _completedStageCount;

    /// <summary>
    /// 전체 미션 완료 여부
    /// </summary>
    public bool IsMissionCompleted => _isMissionCompleted;


    /// <summary>
    /// 결과 저장
    /// </summary>
    /// <param name="reason">종료 사유</param>
    /// <param name="outcome">게임 종료 판정</param>
    /// <param name="elapsedTime">경과 시간</param>
    /// <param name="remainingNpcCount">잔존 NPC 수</param>
    /// <param name="completedStageCount">완료한 단계 수</param>
    /// <param name="isMissionCompleted">전체 미션 완료 여부</param>
    /// <param name="initialNpcCount">시작 손님 수</param>
    /// <param name="missions">단계별 기록</param>
    /// <param name="scores">정산 항목</param>
    /// <param name="grade">최종 등급</param>
    public GameResult (
        GameEndReason reason,
        GameOutcome outcome,
        float elapsedTime,
        int remainingNpcCount,
        int completedStageCount,
        bool isMissionCompleted,
        int initialNpcCount,
        MissionResult [ ] missions,
        GameScoreResult [ ] scores = null,
        string grade = null )
    {
        _reason = reason;
        _outcome = outcome;
        _elapsedTime = elapsedTime;
        _remainingNpcCount = remainingNpcCount;
        _completedStageCount = completedStageCount;
        _isMissionCompleted = isMissionCompleted;
        _initialNpcCount = initialNpcCount;
        //외부 배열 변경이 종료 기록에 반영되지 않도록 복사
        _missions = Array.AsReadOnly( ( MissionResult [ ] ) missions.Clone( ) );
        _isScored = scores != null;
        GameScoreResult [ ] copiedScores = scores == null
            ? Array.Empty<GameScoreResult>( )
            : ( GameScoreResult [ ] ) scores.Clone( );
        _scores = Array.AsReadOnly( copiedScores );
        foreach ( GameScoreResult score in copiedScores )
            _totalScore += score.Points;
        _grade = scores == null ? null : grade;
    }
}
