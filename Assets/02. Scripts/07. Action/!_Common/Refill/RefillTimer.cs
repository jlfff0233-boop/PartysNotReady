
using System;

/// <summary>
/// 재충전 타이머 - 대기 시간과 복구 대기 상태 관리
/// </summary>
public class RefillTimer
{
    bool _isRefilling;      //실제 복구 완료를 기다리는 상태
    float _duration;        //재충전 소요 시간
    float _initialDuration;      //진행률 기준 시간
    float _elapsedTime;     //진행한 재충전 시간


    /// <summary>
    /// 재충전 진행 여부
    /// </summary>
    public bool IsRefilling => _isRefilling;

    /// <summary>
    /// 대기 시간 완료 여부
    /// </summary>
    public bool IsElapsed => CheckElapsed( );

    /// <summary>
    /// 남은 재충전 시간
    /// </summary>
    public float RemainingTime => _isRefilling
        ? Math.Max( 0f, _duration - _elapsedTime ) : 0f;

    /// <summary>
    /// 최초 대기시간 기준 완료 진행률
    /// </summary>
    public float Progress => _isRefilling == false ? 0f :
        ( _initialDuration > 0f ? 1f - RemainingTime / _initialDuration : 1f );


    /// <summary>
    /// 유효한 시간으로 재충전 시작
    /// </summary>
    /// <param name="duration">재충전 소요 시간(초), 0 허용</param>
    /// <returns>재충전 시작 여부</returns>
    public bool TryStart ( float duration )
    {
        if ( _isRefilling == true ||
             float.IsNaN( duration ) || float.IsInfinity( duration ) ||
             duration < 0f )
            return false;

        _duration = duration;
        _initialDuration = duration;
        _elapsedTime = 0f;
        _isRefilling = true;
        return true;
    }

    /// <summary>
    /// 재충전 시간 진행
    /// </summary>
    /// <param name="deltaTime">이번에 진행할 시간(초)</param>
    public void Tick ( float deltaTime )
    {
        if ( _isRefilling == false || CheckElapsed( ) == true ||
             float.IsNaN( deltaTime ) || float.IsInfinity( deltaTime ) ||
             deltaTime <= 0f )
            return;

        //시간이 끝나도 공간 확보와 실제 복구가 끝날 때까지 진행 상태 유지
        float remainingTime = _duration - _elapsedTime;
        _elapsedTime = deltaTime >= remainingTime
            ? _duration
            : _elapsedTime + deltaTime;
    }

    /// <summary>
    /// 재충전 상태 초기화
    /// </summary>
    public void Cancel ()
    {
        _isRefilling = false;
        _elapsedTime = 0f;
    }


    /// <summary>
    /// 남은 재충전 시간 단축
    /// </summary>
    /// <param name="ratio">감소 비율</param>
    /// <returns>단축 여부</returns>
    public bool TryReduceRemaining ( float ratio )
    {
        if ( _isRefilling == false || IsElapsed == true ||
             float.IsNaN( ratio ) == true || float.IsInfinity( ratio ) == true ||
             ratio <= 0f || ratio > 1f )
            return false;

        float remaining = _duration - _elapsedTime;		//남은 시간
        float duration = _elapsedTime + remaining * ( 1f - ratio );		//단축한 종료 시간
        if ( duration >= _duration )
            return false;
        _duration = duration;
        return true;
    }


    /// <summary>
    /// 진행 중인 재충전의 대기 시간 완료 확인
    /// </summary>
    /// <returns>대기 시간 완료 여부</returns>
    bool CheckElapsed ()
    {
        return _isRefilling == true && _elapsedTime >= _duration;
    }


}
