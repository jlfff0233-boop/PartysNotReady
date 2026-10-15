
/// <summary>
/// 잠복 타이머 - 활동 대기 시간 관리
/// </summary>
public class HideTimer
{
    float _remainingTime;   //남은 잠복 시간

    /// <summary>
    /// 잠복 시간 종료 여부
    /// </summary>
    public bool IsReady => _remainingTime <= 0f;

#if UNITY_EDITOR
    /// <summary>
    /// 남은 잠복 시간
    /// </summary>
    public float DebugRemainingTime => _remainingTime;
#endif

    /// <summary>
    /// 잠복 대기 시작
    /// </summary>
    /// <param name="duration">대기 시간(초)</param>
    public void Begin ( float duration )
    {
        _remainingTime = duration;
    }

    /// <summary>
    /// 잠복 시간 갱신
    /// </summary>
    /// <param name="deltaTime">경과 시간(초)</param>
    public void Update ( float deltaTime )
    {
        if ( IsReady == true )
            return;

        _remainingTime -= deltaTime;

        if ( _remainingTime < 0f )
            _remainingTime = 0f;
    }
}
