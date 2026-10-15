using UnityEngine;

/// <summary>
/// 공격 클립 이벤트 전달 - Animator의 시점 알림을 핸들러로 연결
/// </summary>
[RequireComponent( typeof( Animator ) )]
public class AttackAnimationEventRelay : MonoBehaviour
{
    [SerializeField] AttackAnimatorHandler _handler;     //공격 알림을 전달할 핸들러

    /// <summary>
    /// 클립의 명중 시점 전달
    /// </summary>
    public void AttackHit ()
    {
        _handler.AttackHit( );
    }

    /// <summary>
    /// 클립의 포획 장착 시점 전달
    /// </summary>
    public void CaptureAttach ()
    {
        _handler.CaptureAttach( );
    }
}
