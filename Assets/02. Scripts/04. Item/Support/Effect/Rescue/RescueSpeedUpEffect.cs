using UnityEngine;

/// <summary>
/// 구조 속도 증가 효과
/// </summary>
public class RescueSpeedUpEffect : ISupportItemEffect
{
    RescueSpeedUpData _data;		//효과 설정
    RescueInteracter _target;       //적용 대상
    float _remainingTime;       //남은 효과 시간

    /// <summary>
    /// 남은 효과 시간
    /// </summary>
    public float RemainingTime => _remainingTime;

    /// <summary>
    /// 효과 연결
    /// </summary>
    /// <param name="data">효과 설정</param>
    /// <param name="target">적용 기능</param>
    public RescueSpeedUpEffect (
        RescueSpeedUpData data, RescueInteracter target )
    {
        _data = data;
        _target = target;
    }

    /// <summary>
    /// 효과 적용
    /// </summary>
    /// <returns>실행 결과</returns>
    public SupportItemUseResult Use ()
    {
        //사용 가능 시 효과 적용
        if ( _data.IsValid == false ||
            _remainingTime > 0f ||
             _target == null ||
             _target.TryApplySupportProgress( this, _data.Multiplier ) == false )
            return SupportItemUseResult.Rejected;

        //남은 시간
        _remainingTime = _data.Duration;

        //적용 결과 반환
        return SupportItemUseResult.Applied;
    }

    /// <summary>
    /// 적용값 복원
    /// </summary>
    public void End ()
    {
        //남은 시간이 0이 되면 적용 이전 값으로 복구
        _remainingTime = 0f;

        if ( _target != null )
            _target.ClearSupportProgress( this );
    }

    /// <summary>
    /// 효과 시간 진행
    /// </summary>
    /// <param name="deltaTime">경과시간</param>
    public void Tick ( float deltaTime )
    {
        //시간 확인
        if ( _remainingTime <= 0f || deltaTime <= 0f ||
             float.IsNaN( deltaTime ) == true ||
             float.IsInfinity( deltaTime ) == true )
            return;

        //남은 시간 설정
        _remainingTime = Mathf.Max( 0f, _remainingTime - deltaTime );

        if ( _remainingTime == 0f )
            End( );
    }
}
