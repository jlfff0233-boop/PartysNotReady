using UnityEngine;

/// <summary>
/// 플레이어 애니메이션 - 이동과 점프 및 피포획 표시
/// </summary>
public class PlayerAnimatorHandler : AnimatorHandler
{
    static int _jumpId = Animator.StringToHash( "Jump" );        //점프 ID
    static int _capturedId = Animator.StringToHash( "IsCaptured" );        //피포획 ID


    /// <summary>
    /// 점프 동작 시작
    /// </summary>
    public void PlayJump ()
    {
        if ( CanApplyAnimation( ) == false )
            return;

        TargetAnimator.SetTrigger( _jumpId );
    }

    /// <summary>
    /// 피포획 상태 적용
    /// </summary>
    /// <param name="isCaptured">포획된 상태 여부</param>
    public void SetCaptured ( bool isCaptured )
    {
        if ( CanApplyAnimation( ) == false )
            return;

        TargetAnimator.SetBool( _capturedId, isCaptured );
    }

    /// <summary>
    /// 이동과 피포획 표시 및 대기 중인 점프 요청 초기화
    /// </summary>
    public override void ResetAnimation ()
    {
        base.ResetAnimation( );

        if ( CanApplyAnimation( ) == false )
            return;

        TargetAnimator.SetBool( _capturedId, false );
        TargetAnimator.ResetTrigger( _jumpId );
    }
}
