
using UnityEngine;

/// <summary>
/// 변칙 발생 압력 모델 - 방별 누적, 임계치 대기와 재발생 시간 관리
/// </summary>
public class AnomalyPressureModel
{
    float _pressure;     //현재 압력
    float _unvisitedTime;     //미방문 시간
    float _thresholdReachedAt = -1f;     //임계 시간
    float _cooldown;     //재발생 대기 시간


    /// <summary>
    /// 현재 발생 압력
    /// </summary>
    public float Pressure => _pressure;

#if UNITY_EDITOR
    /// <summary>
    /// 방 재발생 가능 시각
    /// </summary>
    public float DebugCooldownUntil => _cooldown;
#endif
    /// <summary>
    /// 미방문 시간
    /// </summary>
    public float UnvisitedTime => _unvisitedTime;
    /// <summary>
    /// 임계 시간
    /// </summary>
    public float ThresholdReachedAt => _thresholdReachedAt;

    /// <summary>
    /// 방문 상태, 시간 반영
    /// </summary>
    /// <param name="elapsedTime">경과 시간</param>
    /// <param name="deltaTime">갱신 이후 진행 시간</param>
    /// <param name="hasPlayer">플레이어 존재 유무</param>
    /// <param name="data">발생 압력 설정</param>
    public void Tick (
        float elapsedTime, float deltaTime,
        bool hasPlayer, AnomalyPressureData data )
    {
        if ( deltaTime <= 0f )
            return;

        //방에 플레이어가 들어와도 이미 쌓인 압력은 유지
        _unvisitedTime = hasPlayer
            ? 0f
            : _unvisitedTime + deltaTime;

        float increase = data.Increase;

        //플레이어가 없는 방에 추가 증가량 적용
        if ( hasPlayer == false )
            increase += data.UnvisitedBonus;

        //설정된 최대 압력을 넘지 않도록 누적
        _pressure = Mathf.Min(
            data.MaxPressure, _pressure + increase * deltaTime );

        //먼저 임계치에 도달한 방의 대기 기록을 유지
        if ( _thresholdReachedAt < 0f &&
             _pressure >= data.Threshold )
            _thresholdReachedAt = elapsedTime;
    }


    /// <summary>
    /// 압력 감소와 재발생 시간 설정
    /// </summary>
    /// <param name="elapsedTime">현재 게임 경과 시간</param>
    /// <param name="data">발생 압력 설정</param>
    public void RecordSuccess (
        float elapsedTime, AnomalyPressureData data )
    {
        //시작 성공 시에만 압력 감소
        _pressure = Mathf.Max(
            0f, _pressure - data.DecreaseOnSuccess );

        //감소 후에도 임계치 이상이면 대기 시점 갱신
        _thresholdReachedAt = _pressure >= data.Threshold
            ? elapsedTime
            : -1f;

        //방별 재발생 대기시간 적용
        _cooldown = elapsedTime + data.RoomCooldown;
    }

    /// <summary>
    /// 발생 가능 여부 확인
    /// </summary>
    /// <param name="elapsedTime">현재 게임 경과 시간</param>
    /// <returns>방별 Cooldown 종료 여부</returns>
    public bool CanSpawn ( float elapsedTime )
    {
        return elapsedTime >= _cooldown;
    }

}
