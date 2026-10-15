
/// <summary>
/// 적 상태 계약 - 진입, 갱신과 종료
/// </summary>
public interface IEnemyState
{
    /// <summary>
    /// 상태 종류
    /// </summary>
    EnemyStateType StateType { get; }

    /// <summary>
    /// 상태 진입 시 초기화
    /// </summary>
    void Enter ();

    /// <summary>
    /// 현재 상태의 행동 갱신
    /// </summary>
    void Update ();

    /// <summary>
    /// 상태 전환 전 정리
    /// </summary>
    void Exit ();
}
