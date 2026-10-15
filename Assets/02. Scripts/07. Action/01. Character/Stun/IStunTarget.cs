
/// <summary>
/// 스턴을 적용할 수 있는 대상
/// </summary>
public interface IStunTarget
{
    /// <summary>
    /// 지정 시간 동안 스턴 적용
    /// </summary>
    /// <param name="duration">스턴 지속 시간(초)</param>
    void TakeStun ( float duration );
}
