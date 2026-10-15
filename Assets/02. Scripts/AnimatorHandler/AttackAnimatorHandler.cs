using UnityEngine;

/// <summary>
/// 공격 캐릭터 애니메이션 - 이동과 공격 표시
/// </summary>
public class AttackAnimatorHandler : AnimatorHandler
{
    static int _attackId = Animator.StringToHash( "Attack" );        //공격 ID
    static int _attackStateId = Animator.StringToHash( "Attack" );  //공격 상태 ID

    /// <summary>
    /// 공격 명중 시점 이벤트
    /// </summary>
    public event System.Action OnAttackHit;

    /// <summary>
    /// 포획 대상 장착 시점 이벤트
    /// </summary>
    public event System.Action OnCaptureAttach;

    /// <summary>
    /// 클립의 명중 요청 전달
    /// </summary>
    public void AttackHit ()
    {
        OnAttackHit?.Invoke( );
    }

    /// <summary>
    /// 클립의 포획 장착 요청 전달
    /// </summary>
    public void CaptureAttach ()
    {
        OnCaptureAttach?.Invoke( );
    }

    /// <summary>
    /// 현재 Animator 상태가 공격 상태인지 여부
    /// </summary>
    public bool HasEnteredAttack =>
        TargetAnimator.GetCurrentAnimatorStateInfo( 0 ).shortNameHash == _attackStateId;

    /// <summary>
    /// 공격 상태 재생 또는 공격 상태로 전환 중인지 여부
    /// </summary>
    public bool IsAttackPlaying
    {
        get
        {
            AnimatorStateInfo current = TargetAnimator.GetCurrentAnimatorStateInfo( 0 );
            if ( current.shortNameHash == _attackStateId )
                return true;

            return TargetAnimator.IsInTransition( 0 ) &&
                TargetAnimator.GetNextAnimatorStateInfo( 0 ).shortNameHash == _attackStateId;
        }
    }


    /// <summary>
    /// 아직 재생되지 않은 공격 요청 취소
    /// </summary>
    public void CancelAttackRequest ()
    {
        TargetAnimator.ResetTrigger( _attackId );
    }


    /// <summary>
    /// 공격 동작 시작
    /// </summary>
    public void PlayAttack ()
    {
        if ( TargetAnimator == null )
            return;

        TargetAnimator.SetTrigger( _attackId );
    }

    /// <summary>
    /// 이동과 대기 중인 공격 요청 초기화
    /// </summary>
    public override void ResetAnimation ()
    {
        base.ResetAnimation( );

        if ( TargetAnimator == null )
            return;

        TargetAnimator.ResetTrigger( _attackId );
    }
#if UNITY_EDITOR
    /// <summary>
    /// 공격 애니메이션 재생과 전환 상태 조회
    /// </summary>
    /// <returns>상태 해시, 재생 진행률과 전환 여부</returns>
    public string GetAttackDebugDetails ()
    {
        AnimatorStateInfo current = TargetAnimator.GetCurrentAnimatorStateInfo( 0 );
        bool transitioning = TargetAnimator.IsInTransition( 0 );
        int next = transitioning ? TargetAnimator.GetNextAnimatorStateInfo( 0 ).shortNameHash : 0;
        return $"공격 재생: {IsAttackPlaying} / 공격 상태 진입: {HasEnteredAttack} / 현재 상태 해시: {current.shortNameHash} / " +
            $"재생 진행률: {current.normalizedTime:F3} / 전환 중: {transitioning} / 다음 상태 해시: {next}";
    }
#endif

}
