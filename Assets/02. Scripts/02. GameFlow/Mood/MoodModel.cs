using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 분위기 상태와 시간 가중 평균 관리
/// </summary>
public class MoodModel
{
    float _initialMood;		//시작 분위기
    float _current;     //현재 분위기
    float _lastTime;        //최근 반영한 경과 시간
    float _lastProgressTime;        //미션 진행 시간
    double _moodArea;       //시간에 따른 분위기 누적 면적
    bool _missionsCompleted;        //전체 미션 완료 여부

    /// <summary>
    /// 이미 반영한 사건 목록
    /// </summary>
    HashSet<AnomalyEvent> _recordedEvents = new( );

    /// <summary>
    /// 포획 기록 딕셔너리(NPC, 포획 회차)
    /// </summary>
    Dictionary<Npc, int> _recordedCaptures = new( );


    /// <summary>
    /// 현재 분위기
    /// </summary>
    public float Current => _current;

    /// <summary>
    /// 반영한 시간까지의 평균 분위기
    /// </summary>
    public float Average =>
        _lastTime > 0f
            ? ( float ) ( _moodArea / _lastTime )
            : _initialMood;


    /// <summary>
    /// 시작 분위기로 초기화
    /// </summary>
    /// <param name="initialMood">시작 분위기</param>
    public MoodModel ( float initialMood )
    {
        _initialMood = Mathf.Clamp( initialMood, 0f, 100f );
        _current = _initialMood;
    }

    /// <summary>
    /// 일정한 분위기로 지난 구간 누적
    /// </summary>
    /// <param name="endTime">구간 끝 시간</param>
    void AccumulateConstant ( float endTime )
    {
        _moodArea += ( double ) _current * ( endTime - _lastTime );
        _lastTime = endTime;
    }

    /// <summary>
    /// 분위기 감소 및 평균 반영
    /// </summary>
    /// <param name="elapsedTime">최근 반영한 게임 경과 시간</param>
    /// <param name="delay">감소 대기시간</param>
    /// <param name="decrease">초당 감소량</param>
    public void Advance (
        float elapsedTime, float delay, float decrease )
    {
        if ( elapsedTime <= _lastTime )
            return;

        //전체 완료 뒤에는 미션 진행 없음에 따른 감소를 멈춤
        if ( _missionsCompleted || decrease <= 0f )
        {
            AccumulateConstant( elapsedTime );
            return;
        }

        //감소 시작 시간
        float decreaseStart = _lastProgressTime + delay;

        //감소 시작 시간이 최근 갱신한 경과 시간보다 커지면
        if ( _lastTime < decreaseStart )
            AccumulateConstant( Mathf.Min( elapsedTime, decreaseStart ) );

        if ( elapsedTime <= _lastTime )
            return;

        //0에 도달한 뒤의 구간까지 나눠 평균에 반영
        float decreaseDuration = Mathf.Min(
            elapsedTime - _lastTime, _current / decrease );
        float nextMood = _current - decreaseDuration * decrease;

        _moodArea +=
            ( double ) ( _current + nextMood ) * 0.5d * decreaseDuration;
        _lastTime += decreaseDuration;
        _current = Mathf.Max( 0f, nextMood );

        if ( _lastTime < elapsedTime )
            AccumulateConstant( elapsedTime );
    }

    /// <summary>
    /// 실제 미션 진행량만큼 회복하고 감소 대기시간 초기화
    /// </summary>
    /// <param name="elapsedTime">진행 시각</param>
    /// <param name="addedAmount">실제 증가량</param>
    /// <param name="recovery">1단위당 회복량</param>
    /// <param name="completed">전체 미션 완료 여부</param>
    public void RecordMissionProgress (
        float elapsedTime, int addedAmount,
        float recovery, bool completed )
    {
        if ( addedAmount <= 0 )
            return;

        _current = Mathf.Clamp(
            _current + addedAmount * recovery, 0f, 100f );
        _lastProgressTime = elapsedTime;
        _missionsCompleted = completed;
    }

    /// <summary>
    /// 확정된 사건 또는 NPC 결과 반영
    /// </summary>
    /// <param name="amount">분위기 변화량</param>
    public void ApplyChange ( float amount )
    {
        _current = Mathf.Clamp( _current + amount, 0f, 100f );
    }

    /// <summary>
    /// 확정된 사건 결과 반영
    /// </summary>
    /// <param name="occurrence">발생한 사건</param>
    /// <param name="status">사건 결과</param>
    /// <param name="resolvedRecovery">해결 회복량</param>
    /// <param name="failedDecrease">실패 감소량</param>
    /// <returns>최근 결과인지 여부</returns>
    public bool TryRecordAnomaly (
        AnomalyEvent occurrence, AnomalyStatus status,
        float resolvedRecovery, float failedDecrease )
    {
        if ( occurrence == null ||
             ( status != AnomalyStatus.Resolved &&
               status != AnomalyStatus.Failed ) ||
             _recordedEvents.Add( occurrence ) == false )
            return false;

        ApplyChange(
            status == AnomalyStatus.Resolved
                ? resolvedRecovery
                : -failedDecrease );

        return true;
    }

    /// <summary>
    /// 확정된 NPC 포획 결과 반영
    /// </summary>
    /// <param name="npc">포획된 NPC</param>
    /// <param name="round">포획 회차</param>
    /// <param name="result">포획 결과</param>
    /// <param name="rescueRecovery">구조 회복량</param>
    /// <param name="departureDecrease">이탈 감소량</param>
    /// <returns>최근 결과인지 여부</returns>
    public bool TryRecordCapture (
        Npc npc, int round, CaptureResult result,
        float rescueRecovery, float departureDecrease )
    {
        if ( npc == null || round <= 0 ||
             ( result != CaptureResult.Rescued &&
               result != CaptureResult.Kidnapped ) )
            return false;

        //기록한 회차 가져오기
        if ( _recordedCaptures.TryGetValue(
            npc, out int recordedRound ) == true &&
             round <= recordedRound )
            return false;

        //포획된 npc 포획 회차 가져오기
        _recordedCaptures [ npc ] = round;
        ApplyChange( result == CaptureResult.Rescued
                ? rescueRecovery
                : -departureDecrease );

        return true;
    }
}