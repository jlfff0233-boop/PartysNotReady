using System;

/// <summary>
/// 스턴 타이머 - 지속 시간과 해제 판단
/// </summary>
public class StunTimer
{
    float _remainingTime;       //남은 스턴 시간

    /// <summary>
    /// 스턴 적용 여부
    /// </summary>
    public bool IsStunned => _remainingTime > 0f;

    /// <summary>
    /// 스턴 적용 또는 남은 시간 연장
    /// </summary>
    /// <param name="duration">스턴 지속 시간(초)</param>
    public void Apply ( float duration )
    {
        //중복 스턴은 합산하지 않고 더 긴 시간 유지
        _remainingTime = Math.Max( _remainingTime, duration );
    }

    /// <summary>
    /// 스턴 시간 갱신
    /// </summary>
    /// <param name="deltaTime">경과 시간(초)</param>
    public void Update ( float deltaTime )
    {
        _remainingTime = Math.Max( 0f, _remainingTime - deltaTime );
    }

    /// <summary>
    /// 스턴 해제
    /// </summary>
    public void Clear ()
    {
        _remainingTime = 0f;
    }
}
